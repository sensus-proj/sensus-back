# Plan técnico — Spec 046: Asistente agéntico Sensi IA (7 capas)

| Campo | Valor |
|---|---|
| Spec | [spec.md](spec.md) (`046`, v2.0.0) |
| Estado del plan | Listo para implementación |
| Versión | 2.0.0 |
| Constitution Check | PASS CON OBSERVACIONES (`EX-001`) |
| Alineado a | `constitution.md` v2.1.0 · `AGENTS.md` v2.1.0 · [ADR 0012](../../docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md) |

---

## 1. Resumen de arquitectura (7 capas en Clean Architecture)

La solución integra a **Sensi IA** sin agregar contenedores ni instancias AWS fuera de la Lambda existente (`SensusInfraStack`, `.NET 10`, `512 MB`, timeout `29 s` en Lambda y API Gateway v2). Toda la ejecución ocurre *in-process* manteniendo estrictamente la dirección de dependencias `Api → Application → Domain ← Infrastructure`:

```
HTTP Request (JWT Bearer)
  │
  ▼ [Sensus.Api — Endpoint + Auth + Rate Limiting]
  │   Valida JWT (Specs 002/004), bloquea Invitado (403) y mapea scopes por RoleType
  │
  ▼ [Sensus.Application — SendAiMessageCommandHandler (Orquestación de 7 capas)]
  │
  ├──(5A) Guardrail de entrada determinista: Anti-Prompt-Injection / Jailbreak (0 tokens LLM)
  ├──(5B) Guardrail de entrada moderación: `OpenAiSettings:Sensi:ModerationModel` = `omni-moderation-latest` (fail-closed)
  ├──(6A) Model Router previo con afinidad sticky en AiConversation.AssignedModel
  ├──(6B) Caché distribuida opcional (Spec 048) para dudas generales FAQ sin PII
  ├──(1+2+6C) Prompt Caching Builder:
  │      ├── Bloque Estático (≥ 1,024 tokens): Tools permitidas + Developer/System Prompt + <contexto>Markdown vX.Y.Z</contexto>
  │      └── Bloque Dinámico: contexto efímero de sesión + ventana historial + prompt actual
  │
  ├──(3) Bucle agéntico acotado (máx. 3 rondas, deadline ≤ 25 s):
  │      OpenAI Chat API ⇄ In-Process MCP Tool Dispatcher (Sensus.Infrastructure)
  │                          ├── Valida scopes JWT en cada invocación
  │                          ├── Ejecuta solo lectura (Offers/Account/Claims/Merchant)
  │                          ├── Sanea campos de texto recuperados (anti-inyección indirecta)
  │                          └── Backend valida vigencia, stock > 0 y recalcula SUM <= presupuesto
  │
  ├──(5C) Guardrail de salida: Moderación fail-closed + filtro anti-fuga de Hard Rules + PII/Secret Redactor
  └──(7)  Persistencia y Telemetría:
         ├── MongoDB: ai_conversations, ai_messages (redactados), ai_trace_logs (redactados + TTL)
         └── CloudWatch Logs: JSON estructurado sanitizado (correlation-id, hashes, tokens, latencias)
```

---

## 2. Diseño por capa

### 2.1 `Sensus.Domain` (sin dependencias externas)
- **Entidades y agregados**:
  - `AiConversation`: `Id`, `OwnerUserId`, `OwnerRole`, `Status` (`Active | Archived`), `AssignedModel` (`string`), `RoutingTier` (`Fast | Reasoning`), `AffinityLockedAtUtc`, `MarkdownVersion`, `CreatedAtUtc`, `UpdatedAtUtc`, `MessageCount`. Invariantes: solo el `OwnerUserId` puede leer, continuar o eliminar; `AssignedModel` no puede quedar vacío una vez asignado.
  - `AiMessage`: `Id`, `ConversationId`, `OwnerUserId`, `Role` (`User | Assistant | SystemNotice`), `RedactedContent`, `Degraded`, `RecommendedPackage` (value object opcional), `OffersCitedIds`, `ToolsInvoked`, `CreatedAtUtc`.
  - `AiTraceLog`: `Id`, `ConversationId`, `OwnerUserId`, `CorrelationId`, `MarkdownVersion`, `MarkdownHashSha256`, `RoutingTier`, `ModelAliasInternal`, `RedactedPrompt`, `RedactedResponse`, `ToolsInvokedSummary`, `PromptTokens`, `CachedTokens`, `CompletionTokens`, `TotalLatencyMs`, `CreatedAtUtc`, `ExpiresAtUtc`.
- **Value Objects / Enums / Invariantes**:
  - `AiRoutingTier` (`Fast`, `Reasoning`), `AiScope` (`ai:chat`, `offers:read`, `account:read`, `claims:read`, `merchant:read`), `RecommendedPackage` y `BudgetCalculator` (invariante `TotalPriceMxn <= BudgetMxn`, rechazo de presupuesto `<= 0` y descarte estricto de ítems sin stock/vigencia o ajenos al conjunto verificado).

### 2.2 `Sensus.Application` (casos de uso CQRS, DTOs, puertos y reglas de flujo)
- **Comandos y consultas**:
  - `CreateOrResumeConversationCommand` / `Handler` (`POST /api/v1/ai/conversations`).
  - `GetConversationQuery` / `Handler` (`GET /api/v1/ai/conversations/{conversationId}`, paginación `1..100`, default `20`).
  - `DeleteConversationCommand` / `Handler` (`DELETE /api/v1/ai/conversations/{conversationId}`, cascada sobre `ai_conversations`, `ai_messages` y `ai_trace_logs`).
  - `SendAiMessageCommand` / `Handler` (`POST /api/v1/ai/messages`).
- **Puertos declarados en Application**:
  - `IAiConversationRepository`, `IAiMessageRepository`, `IAiTraceLogRepository` (incluye método de purga total por usuario llamado por eliminación de cuenta en Spec 030).
  - `IScopeAuthorizationMapper` (mapea `ClaimsPrincipal` / `RoleType` a `IReadOnlySet<AiScope>` sin leer roles del cliente).
  - `IKnowledgeBaseProvider` (carga Markdown versionado, entrega `MarkdownVersion` + `MarkdownHashSha256`).
  - `IPromptInjectionDetector`, `IPiiRedactor`, `IHardRuleLeakDetector`.
  - `IContentModerationGateway` (reutiliza/extiende contrato OpenAI de Spec 017 para texto síncrono fail-closed).
  - `IAiModelRouter` (clasifica turno 1 o evalúa regla de escalamiento explícito manteniendo afinidad *sticky*).
  - `IAiChatGateway` (invoca OpenAI Chat API con payload en 2 bloques estricto y parámetros `store: false`).
  - `IMcpToolRegistry` y `IMcpToolExecutor` (despacha herramientas solo lectura autorizadas por scopes).
  - `IAiBudgetGuard` (controla gasto acumulado `EX-001`, cuota diaria por usuario y activa degradación determinista).
  - `IDeterministicAiFallbackService` (construye respuesta/paquete determinista sin IA generativa cuando chat falla o se agota presupuesto).
  - `IDistributedCacheService` (integración opcional `Spec 048` para preguntas FAQ puras sin contexto personal).
- **Integración con eliminación de cuenta (Spec 030)**: ampliar el puerto/caso de uso de baja de cuenta para invocar `PurgeUserAiDataAsync(userId, ct)` antes de borrar el perfil.

### 2.3 `Sensus.Infrastructure` (adaptadores externos y persistencia)
- **Base de conocimiento Markdown**: recurso embebido versionado (p. ej. `Knowledge/sensi-app-guide.v1.0.0.md`) con verificación de hash SHA-256 al arranque y tamaño calibrado (`≥ 1,024` tokens sumado a system prompt y tools para garantizar *Prompt Caching*).
- **Servidor / Adaptador MCP *in-process***:
  - Implementa catálogo cerrado de 5 herramientas de solo lectura expuestas simultáneamente bajo esquema MCP interno y `tools` de OpenAI Function Calling:
    1. `search_offers(query, category?, maxBudgetMxn?, lat?, lon?, radiusKm?, onlyOpenNow?)` → consulta `IOfferReadRepository` (Specs 007/041) limitando `radiusKm` entre `1` y `5`, `stock > 0`, comercio activo y máx. 15 resultados sanitizados.
    2. `get_offer_detail(offerId)` → detalle vigente de oferta, comercio, horario y métodos de pago aceptados.
    3. `get_my_account()` → alias/nombre de pila, ciudad/estado general, rol activo y preferencias de notificación de `callerUserId` (nunca email completo, teléfono, hash ni `security_stamp`).
    4. `get_my_claims(status?, limit?)` → hasta 10 apartados QR recientes del `callerUserId` (folio/código de oferta, estado y expiración, sin tokens internos).
    5. `get_my_merchant_offers(limit?)` → requiere scope `merchant:read` (`RoleType.Comerciante`); consulta exclusivamente ofertas cuyo `merchantOwnerId == callerUserId`.
  - Cada herramienta pasa por `ScopeAuthorizedMcpDispatcher`: verifica `requiredScope ∈ callerScopes`, valida argumentos JSON mediante esquema estricto, aplica timeout individual (`≤ 2 s`) y ejecuta sanitización anti-inyección indirecta sobre descripciones/títulos antes de regresar el JSON al modelo.
- **Cliente OpenAI Chat & Moderation (`OpenAiChatGateway`, reutilizando `OpenAiSettings`)**:
  - `HttpClient` tipado sobre HTTPS TLS `≥ 1.2`, API key resuelta desde AWS Secrets Manager o SSM Parameter Store (`/sensus/{env}/openai-api-key`).
  - Timeout por llamada al modelo calibrado dentro del presupuesto global (`≤ 12 s` por ronda, deadline total IA `≤ 25 s` antes del timeout de `29 s` de Lambda/API Gateway).
  - Payload con `store: false`, orden determinista de `tools` (ordenadas por nombre), mensaje `developer`/`system` estático + `<contexto>...</contexto>` y bloque dinámico separado.
- **Guardrails & Redacción**:
  - `RegexAndStructuralInjectionDetector`: detecta intentos de sobrescritura de rol, etiquetas pseudo-XML (`</contexto>`, `<system>`), solicitudes de volcado de instrucciones/keys y patrones multilingües de *jailbreak*.
  - `PatternPiiAndSecretRedactor`: redacta emails, teléfonos MX/E.164, tarjetas Luhn, RFC/CURP y tokens (`Bearer `, `eyJ...`, `sk-...`, `AKIA...`).
  - `HardRuleLeakDetector`: verifica que la respuesta final no mencione `OpenAI`, `GPT-4`, `omni-moderation`, nombres de modelo configurados ni fragmentos del prompt de sistema.
- **Persistencia MongoDB**: repositorios e índices para `ai_conversations`, `ai_messages` y `ai_trace_logs`.
- **Telemetría CloudWatch**: métricas EMF (`AiTurnLatencyMs`, `AiCachedTokensRatio`, `AiEstimatedCostUsd`, `AiGuardrailBlockedCount`, `AiDegradedFallbackCount`) y alerta de facturación / presupuesto al `80 %` (`0.80 USD`).

### 2.4 `Sensus.Api` (Endpoints, Middleware y Composición)
- Grupo `/api/v1/ai` con política `RegisteredUserPolicy` (o política equivalente que acepte `Consumidor`, `Comerciante`, `Administrador` y devuelva `403` explícito para `Invitado` + `401` sin JWT o token revocado).
- Rate limiting específico de Spec 013 (`AiChatPolicy`) sobre `POST /api/v1/ai/messages`.
- Errores estandarizados mediante `ProblemDetails` con `correlationId`, sin filtrar trazas, nombres de colecciones ni detalles de proveedor IA.
- Registro DI exclusivo en `Sensus.Api/Program.cs`.

---

## 3. Modelo de datos e índices (MongoDB)

| Colección | Campos principales | Índices declarados | TTL / Purga |
|---|---|---|---|
| `ai_conversations` | `_id`, `ownerUserId`, `ownerRole`, `status`, `routingTier`, `assignedModel`, `markdownVersion`, `lastScreenContext`, `messageCount`, `createdAtUtc`, `updatedAtUtc` | `{ ownerUserId: 1, status: 1, updatedAtUtc: -1 }` | Purga inmediata en `DELETE /api/v1/ai/conversations/{id}` y Spec 030 |
| `ai_messages` | `_id`, `conversationId`, `ownerUserId`, `role`, `redactedContent`, `recommendedPackage`, `offersCitedIds`, `toolsInvoked`, `degraded`, `createdAtUtc` | `{ conversationId: 1, createdAtUtc: -1 }`, `{ ownerUserId: 1, createdAtUtc: -1 }` | Purga en cascada por `conversationId` u `ownerUserId` |
| `ai_trace_logs` | `_id`, `conversationId`, `ownerUserId`, `correlationId`, `markdownVersion`, `markdownHash`, `routingTier`, `cacheHit`, `promptTokens`, `cachedTokens`, `completionTokens`, `redactedPrompt`, `redactedResponse`, `toolExecutions`, `latencyMs`, `estimatedCostUsd`, `createdAtUtc`, `expiresAtUtc` | `{ conversationId: 1, createdAtUtc: -1 }`, `{ ownerUserId: 1, createdAtUtc: -1 }`, `{ expiresAtUtc: 1 }` (TTL `expireAfterSeconds: 0`) | Retención configurable (p. ej. `30` días) + purga en cascada inmediata ante `DELETE` o Spec 030 |
| `ai_budget_usage` (o contador mensual atómico + caché Redis) | `_id` (`yyyy-MM`), `estimatedSpendUsd`, `alert80Sent`, `updatedAtUtc` | `_id` único por mes | Auditoría ligera sin PII |

---

## 4. Optimización (*Prompt Caching* + *Model Router*) y presupuesto (`EX-001`)

1. **Prefijo cacheable determinista (Bloque 1 Estático)**:
   - Orden fijo en cada llamada:
     1. Arreglo `tools` ordenado alfabéticamente según el subconjunto de herramientas permitido para el rol (`Consumidor` vs `Comerciante`).
     2. Mensaje `developer` (o `system`) único conteniendo: identidad oficial Sensus, *Hard Rules*, reglas de formato/paquetes y documento Markdown íntegro envuelto en `<contexto version="X.Y.Z">...</contexto>`.
   - Al mantener invariable ese prefijo (`≥ 1,024` tokens) por rol y versión de Markdown, OpenAI reutiliza la caché de prefijo a costo reducido de tokens en caché.
2. **Bloque Dinámico separado (Bloque 2)**:
   - Se envía después del bloque estático como mensaje contextual efímero + últimos `N` turnos (configurable, defecto `6`) + mensaje actual del usuario. Nunca se insertan datos variables (hora actual al segundo, `userId` interno ni coordenadas en el Bloque 1).
3. **Model Router con afinidad *sticky***:
   - En el turno 1 de la conversación, `DeterministicComplexityClassifier` evalúa longitud, intención (pregunta FAQ / estado de cuenta vs armado de despensa multi-oferta, recetas combinadas o multi-filtro):
     - Intención simple → `Fast` (modelo rápido/económico en `OpenAiSettings:Sensi:FastModel`, por defecto `gpt-4o-mini`).
     - Intención multi-paso / receta / paquete complejo → `Reasoning` (modelo avanzado en `OpenAiSettings:Sensi:ReasoningModel`, por defecto `gpt-4o`).
   - El modelo seleccionado se guarda en `AiConversation.AssignedModel` y se reutiliza en los siguientes turnos (**afinidad *sticky***) para no romper la clave de caché del prefijo.
   - Solo si una regla explícita configurable detecta necesidad insatisfecha en nivel `Fast` (ej. > 2 llamadas encadenadas requeridas) se promueve explícitamente la sesión a `Reasoning` registrando evento `routerEscalation`.
4. **Control de gasto `EX-001` (`5.00 USD/mes`)**:
   - Límite máximo de tokens de entrada (`maxInputChars: 1500`), salida (`maxCompletionTokens: 500`), mensajes diarios por persona (configurable, defecto `20/día`), presupuesto mensual estricto `5.00 USD/mes`, alerta al `80 %` (`4.00 USD`) y degradación determinista al `100 %`.
   - **Estimación de costo por turno** (precios de referencia por millón de tokens, a verificar en la página de precios de OpenAI antes de implementar: modelo *fast* tipo `gpt-4o-mini` 0.15 entrada / 0.075 en caché / 0.60 salida; modelo *reasoning* tipo `gpt-4o` 2.50 / 1.25 / 10.00). Supuestos de un turno típico: bloque estático ≈ 3,000 tokens en caché, ≈ 1,900 tokens no cacheados entre dos llamadas (una ronda de herramientas) y ≈ 350 de salida. La moderación (`omni-moderation-latest`) no tiene costo.

     | Escenario | *Fast* | *Reasoning* |
     |---|---|---|
     | Turno típico (1 ronda de herramientas, caché caliente) | ≈ 0.001 USD | ≈ 0.016 USD |
     | Peor caso (3 rondas, 500 de salida, caché fría) | ≈ 0.003 USD | ≈ 0.05 USD |
     | Turnos típicos que caben en 5.00 USD | ≈ 5,000 | ≈ 300 |

   - **Decisión derivada**: con el techo de `5.00 USD/mes` el nivel *reasoning* queda **habilitado** (`OpenAiSettings:ReasoningTierEnabled = true`) con un sub-techo configurable de `3.00 USD/mes` (`OpenAiSettings:ReasoningMonthlyBudgetUsd`): al alcanzarlo, el router asigna *fast* a las conversaciones nuevas durante el resto del mes, de modo que el nivel *reasoning* nunca agote el presupuesto de *fast* (al menos ≈ 2,000 turnos *fast* garantizados). Las *Evals* con modelo usan solo *fast* (≈ 60 casos ≈ 0.06 USD por ejecución) y cuentan dentro del mismo techo, con un máximo configurable de `4` ejecuciones al mes.
   - Costo adicional AWS estimado: **0.00 USD/mes** (reutiliza Lambda, API Gateway, MongoDB Atlas M0, Redis existente de Spec 048, SSM Parameter Store y CloudWatch Logs dentro de Free Tier).

---

## 5. Estrategia de pruebas y puerta de *Evals*

| Tipo de prueba | Proyecto / Ubicación | Qué valida |
|---|---|---|
| **Unitarias** | `tests/Sensus.Application.UnitTests` | Invariantes de `AiConversation`, `BudgetCalculator` (`SUM <= presupuesto`, rechazo `<= 0`), mapeo de scopes por `RoleType`, detector anti-inyección, `PatternPiiAndSecretRedactor`, `HardRuleLeakDetector`, orden determinista del payload en 2 bloques (`Bloque Estático ≥ 1,024` tokens), clasificador del router con afinidad *sticky* |
| **Integración (MongoDB + MCP + Auth + Guardrails)** | `tests/Sensus.IntegrationTests` | CRUD completo (`POST`/`GET`/`DELETE`), rechazo `401`/`403` para `Invitado` y `Consumidor` sobre tool comercial, índices TTL de `ai_trace_logs`, cascada al borrar conversación y al eliminar cuenta (Spec 030), dobles HTTP para OpenAI (timeout, 5xx, respuesta malformada, falla fail-closed de moderación, degradación determinista) |
| **Arquitectura** | `tests/Sensus.ArchitectureTests` | Sin referencias de `Domain`/`Application` hacia `Infrastructure` o SDKs externos |
| ***Evals* deterministas** | `dotnet test`, bloqueantes por PR (Spec 047) | Sin red: `HardRuleLeakDetector`, redactor de PII, filtro de inyección, recálculo `SUM(precio_oferta) <= presupuesto` y estructura de dos bloques del payload; umbral `100 %` |
| ***Evals* con modelo** | Job manual o programado previo a release (Spec 047) | Proveedor real con `OPENAI_API_KEY` de GitHub Secrets y solo el modelo *fast*: precisión sobre Markdown (envíos/pagos/QR), tono, invocación de herramientas y resistencia a *Prompt Injection*/*Jailbreak*; umbrales de `NFR-046-08`. Fuera de la suite de pruebas por `constitution.md` v2.0.0 |

---

## 6. Constitution Check

| Principio / Sección | Estado | Evidencia / Condición |
|---|---|---|
| **I — SDD primero** | PASS | Spec, plan, tasks, checklist y OpenAPI actualizados antes de codificar |
| **II — Clean Architecture y CQRS** | PASS | MCP *in-process* detrás de puertos en `Application`, compuesto en `Program.cs` |
| **III — Seguridad por diseño** | PASS | JWT obligatorio, herencia de *scopes* por rol, moderación *fail-closed*, anti-inyección y redacción PII/credenciales |
| **IV — Privacidad y secretos** | PASS | Keys en Secrets Manager / SSM; CloudWatch solo metadatos y hashes sin PII/prompts; MongoDB audit redactado con TTL y purga en `DELETE` y Spec 030 (`constitution.md` v2.0.0 P-IV) |
| **V — Rendimiento y paginación** | PASS | Historial paginado `20/100`, índices MongoDB explícitos, *Prompt Caching* y deadline `≤ 25 s` |
| **VI — AWS Free Tier y costo mínimo** | PASS CON OBSERVACIONES (`EX-001`) | `0.00 USD` en cómputo AWS (MCP *in-process* en Lambda existente); costo variable OpenAI acotado bajo excepción vigente `EX-001` (`5.00 USD/mes` con alarma 80 % y degradación determinista) |
| **VII — Calidad, pruebas y DevSecOps** | PASS | Cobertura ≥ 80 % en Domain/Application + *Evals* deterministas bloqueantes por PR y *Evals* con modelo previas a release (Spec 047) |
| **§2.1–§2.5 — Stack y restricciones** | PASS | Enmendado en `constitution.md` v2.0.0 (MAJOR) y [ADR 0012](../../docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md) |

---

## 7. Secuencia de implementación ordenada

1. Dominio y puertos (`AiConversation`, `AiMessage`, `AiTraceLog`, `AiScope`, `RecommendedPackage`, interfaces de `Application`) → `T046-01` .. `T046-03`.
2. Documento Markdown versionado y *Prompt Builder* estricto en 2 bloques (`<contexto>`) → `T046-04` .. `T046-05`.
3. Capa de autorización (`IScopeAuthorizationMapper`) y adaptador MCP *in-process* de 5 herramientas de solo lectura + recálculo backend → `T046-06` .. `T046-08`.
4. Guardrails duales (OpenAI Moderation API *fail-closed*, detector anti-*Prompt Injection*/*Jailbreak*, detector de fuga de *Hard Rules*, redactor PII/credenciales) → `T046-09` .. `T046-11`.
5. *Model Router* con afinidad *sticky*, control de presupuesto `EX-001`, caché distribuida opcional y degradación determinista → `T046-12` .. `T046-14`.
6. Persistencia MongoDB (índices TTL y purga en cascada en `DELETE` y Spec 030), trazabilidad CloudWatch sanitizada y cliente OpenAI TLS `≥ 1.2` + `store: false` → `T046-15` .. `T046-17`.
7. Endpoints `/api/v1/ai/*` con Rate Limiting, composición en `Program.cs`, manifiesto inicial de *Evals* y batería completa de pruebas → `T046-18` .. `T046-22`.
