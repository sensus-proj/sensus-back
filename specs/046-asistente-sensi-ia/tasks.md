# Tareas — Spec 046: Asistente agéntico Sensi IA (7 capas)

| Campo | Valor |
|---|---|
| Spec | [spec.md](spec.md) (`046`, v2.0.0) |
| Plan | [plan.md](plan.md) |
| Estado | Parcial |
| Alineado a | `constitution.md` v2.1.0 · `AGENTS.md` v2.1.0 · [ADR 0012](../../docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md) |

---

## Lista de tareas atómicas

| ID | Descripción | Capa | Requisito | Dep. | Estado |
|---|---|---|---|---|---|
| `T046-01` | Definir entidades e invariantes en `Sensus.Domain` (`AiConversation`, `AiMessage`, `AiTraceLog`, `AiRoutingTier`, `AiScope`, `RecommendedPackage`, `BudgetCalculator`) | Domain | `FR-046-01`, `FR-046-07` | — | Completada |
| `T046-02` | Definir puertos de `Application` (repositorios IA, router, cache, MCP registry/executor, moderación, detector inyección, redactor PII/secretos, *HardRuleLeakDetector*, guardia `EX-001`, fallback determinista) | Application | `FR-046-02`, `FR-046-06` | `T046-01` | Completada |
| `T046-03` | Definir DTOs y contratos CQRS (`CreateOrResumeConversationCommand`, `GetConversationQuery`, `DeleteConversationCommand`, `SendAiMessageCommand`) | Application | `FR-046-01` | `T046-02` | Completada |
| `T046-04` | Redactar y versionar el documento Markdown de conocimiento completo de Sensus (`semver` + hash SHA-256, flujos QR, radio 1–5 km, LFPDPPP) y su cargador `IKnowledgeBaseProvider` | Infrastructure | `FR-046-05` | `T046-02` | Completada |
| `T046-05` | Implementar `TwoBlockPromptBuilder` garantizando Bloque Estático (`tools` ordenadas + identidad oficial Sensus/*Hard Rules* + `<contexto>` Markdown `≥ 1,024` tokens) separado del Bloque Dinámico | Application | `FR-046-03`, `FR-046-04`, `FR-046-11`, `NFR-046-03` | `T046-02`, `T046-04` | Completada |
| `T046-06` | Implementar `JwtScopeAuthorizationMapper` derivando scopes por `RoleType` (`Consumidor` vs `Comerciante`, rechazo `403` para `Invitado`) | Infrastructure | `FR-046-02` | `T046-02` | Completada |
| `T046-07` | Implementar adaptador MCP *in-process* y catálogo de 5 herramientas *read-only* (`search_offers`, `get_offer_detail`, `get_my_account`, `get_my_claims`, `get_my_merchant_offers`) con validación de scopes y saneamiento anti-inyección indirecta | Infrastructure | `FR-046-06`, `FR-046-09` | `T046-06` | Completada |
| `T046-08` | Implementar orquestación del bucle agéntico (`≤ 3` rondas, `≤ 25 s`), validación de presupuesto, filtro de vigencia/`stock > 0`, descarte de `offerId` inexistentes y recálculo `SUM <= presupuesto` en backend | Application | `FR-046-06`, `FR-046-07`, `NFR-046-01`, `NFR-046-04` | `T046-05`, `T046-07` | Completada |
| `T046-09` | Implementar guardrail previo de moderación de entrada/salida con OpenAI `omni-moderation-latest` en modo *fail-closed* antes y después del modelo de chat | Infrastructure | `FR-046-08`, `FR-046-10`, `NFR-046-05` | `T046-02` | Completada |
| `T046-10` | Implementar detector anti-*Prompt Injection*/*Jailbreak* (`RegexAndStructuralInjectionDetector`) en entrada y *HardRuleLeakDetector* en salida | Infrastructure | `FR-046-04`, `FR-046-09`, `NFR-046-05` | `T046-02` | Completada |
| `T046-11` | Implementar `PatternPiiAndSecretRedactor` para enmascarar PII y credenciales en respuestas entregadas y trazas persistidas | Infrastructure | `FR-046-10`, `FR-046-15`, `NFR-046-06` | `T046-02` | Completada |
| `T046-12` | Implementar *Model Router* previo (`DeterministicComplexityClassifier`) con afinidad *sticky* persistida en `AiConversation.AssignedModel` y regla de escalamiento configurable | Application / Infrastructure | `FR-046-12`, `NFR-046-03` | `T046-03` | Completada |
| `T046-13` | Implementar `AiBudgetGuard` (`EX-001`, techo `5.00 USD/mes`, sub-techo del nivel *reasoning* en `3.00 USD`, alerta `80 %`, cuota diaria por usuario) y caché distribuida opcional para FAQ equivalentes (Spec 048) | Infrastructure | `FR-046-13`, `NFR-046-02` | `T046-02` | Completada |
| `T046-14` | Implementar `DeterministicAiFallbackService` para degradación determinista controlada tras fallo/timeout/cuota de OpenAI Chat | Application | `FR-046-13`, `NFR-046-07` | `T046-08`, `T046-13` | Completada |
| `T046-15` | Implementar `OpenAiChatGateway` sobre TLS `≥ 1.2`, API Key en Secrets Manager/SSM y `store: false`, respetando payload de 2 bloques | Infrastructure | `FR-046-11`, `FR-046-16` | `T046-05`, `T046-13` | Parcial |
| `T046-16` | Implementar repositorios MongoDB (`ai_conversations`, `ai_messages`, `ai_trace_logs` con índice TTL), cascada en `DELETE` y enlace con eliminación de cuenta (Spec 030) | Infrastructure / Application | `FR-046-01`, `FR-046-15` | `T046-01`, `T046-11` | Completada |
| `T046-17` | Implementar emisión de logs estructurados sanitizados y métricas EMF hacia CloudWatch (`correlation-id`, hashes, versión Markdown, tokens cacheados, cero PII/prompts en claro) | Infrastructure | `FR-046-14`, `NFR-046-06` | `T046-08` | Completada |
| `T046-18` | Implementar endpoints `/api/v1/ai/conversations` (`POST`), `/api/v1/ai/conversations/{conversationId}` (`GET`, `DELETE`), `/api/v1/ai/messages` (`POST`), políticas JWT y Rate Limiting (Spec 013) + composición en `Program.cs` | Api | `FR-046-01`, `FR-046-02`, `FR-046-13` | `T046-08`, `T046-16` | Completada |
| `T046-19` | Crear manifiesto y batería inicial versionada de *Evals* (precisión Markdown, uso de tools/presupuesto, tono, inyecciones, *Hard Rules* y redacción PII) separada en *Evals* deterministas (`dotnet test`, gate por PR) y *Evals* con modelo (job previo a release), consumida por Spec 047 | Docs / Tests | `FR-046-17`, `NFR-046-05`, `NFR-046-08` | `T046-04`, `T046-05` | Completada |
| `T046-20` | Escribir pruebas unitarias de dominio, *Prompt Builder*, *Model Router* *sticky*, cálculo de presupuesto, anti-inyección, redacción PII y *Hard Rules* (`≥ 80 %` cobertura) | Tests | `FR-046-04`, `FR-046-07`, `FR-046-09`, `FR-046-10`, `FR-046-11`, `FR-046-12` | `T046-08`, `T046-14` | Completada |
| `T046-21` | Escribir pruebas de integración (Testcontainers MongoDB + HTTP mocks de OpenAI para fallo/timeout/malformado/moderación, CRUD + `DELETE` cascada, autorización `Invitado`/roles, ausencia de PII en logs) | Tests | `FR-046-01`, `FR-046-02`, `FR-046-08`, `FR-046-14`, `FR-046-15`, `FR-046-16` | `T046-18` | Completada |
| `T046-22` | Validar `dotnet test`, cobertura ≥ 80 %, pruebas de arquitectura, ejecución de *Evals* y conformidad OpenAPI | Tests / Docs | `FR-046-17`, `NFR-046-01`, `NFR-046-08` | `T046-19`, `T046-20`, `T046-21` | Completada |
| `T046-24` | Agregar `OpenAiSettings:Sensi:Temperature` (`[Range(0.0, 1.0)]`, por defecto `0.0`) y enviarla en cada llamada de `OpenAiChatGateway`; prueba con doble HTTP que verifica el valor enviado | Infrastructure / Application | `FR-046-19` | T046-15 | Completada (`OpenAiChatGatewayTests`, `SensiSettingsValidatorTests`) |
| `T046-23` | Completar ofertas citadas con datos del repositorio (`CitedOfferEnricher`, `MaxCitedOfferCards`, `CitedOfferDto` ampliado) y cubrirlo con pruebas | Application | `FR-046-18` | T046-06 | Completada |

---

## Notas de ejecución y estado parcial

- **`T046-13` (Completada)**: `AiBudgetGuard` opera sobre DynamoDB (Spec 049, techo `5.00 USD/mes`, sub-techo *reasoning* `3.00 USD`, alerta `80 %`, cuota diaria por usuario) integrado con la caché distribuida de FAQ equivalentes (`AiCacheKeys` con normalización Unicode + SHA-256, TTL `AiFaq = 60` min en `Redis:TtlByEntityMinutes`, lookup/store seguro tras guardrails y sin historial/ubicación/herramientas/PII en `SendAiMessageCommandHandler`; pruebas `AiCacheKeysTests` y `SendAiMessageCommandHandlerTests`).
- **`T046-15` (Parcial)**: `OpenAiChatGateway` respeta TLS ≥ 1.2, payload de 2 bloques y `store: false`; la lectura de secretos desde SSM Parameter Store (`/sensus/<env>/*` al arrancar, fail-closed) ya está integrada en código vía `FR-045-11` (Spec 045), pendiente únicamente validar la presencia del parámetro en AWS.
- **`T046-17` (Completada)**: Logs estructurados con `X-Correlation-Id` sin PII ni prompts en claro y métricas CloudWatch EMF mediante `IAiMetricsPublisher` / `EmbeddedAiMetricsPublisher` en namespace `Sensus/Api` (4 métricas sin dimensiones: `AiTurnLatency`, `AiCachedTokens`, `AiEstimatedCostMicroUsd`, `AiFallbackOrBlocked`, total 9/10 en nivel gratuito; pruebas `AiMetricsTests`).
- **`T046-19` (Completada)**: Manifiesto de Evals v2.0.0 (`MDL-001`..`MDL-003`), runner `.github/scripts/run-ai-evals.mjs` con evaluación real por modelo vía OpenAI (fail-closed sin `OPENAI_API_KEY`, sin puntuación hardcodeada), detector determinista de fugas, verificador XML `--trx` de `DET-001`..`DET-005`, sanitización de PII/secretos y 6 pruebas `node:test` en `.github/scripts/run-ai-evals.test.mjs`.
- **`T046-21` (Completada)**: Corrección de `forceNew = true` archivando la conversación activa previa en `CreateOrResumeConversationCommandHandler` (`AiConversationHandlersTests`), pruebas `AiEndpointsTests`, `AiMetricsTests` y persistencia real en `MongoAiRepositoriesTests` (round-trips siempre activos + 5 pruebas contra MongoDB real habilitado en `ci.yml` mediante `MONGO_CONNECTION_STRING`).
- **Pendientes operativos en vivo (mantienen el estado global en `Parcial`)**: prueba de carga `NFR-046-01` (`p95 < 8 s`, brecha `G-13`), medición de tasa `≥ 70 %` de *Prompt Caching* (`NFR-046-03`) en tráfico real de OpenAI y primera ejecución en vivo de `.github/workflows/ai-evals.yml` con `OPENAI_API_KEY` en GitHub Secrets.
