# Plan 046 — Asistente conversacional Sensi

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS CON OBSERVACIONES |
| Alineado a | `constitution.md` v1.4.0 · `AGENTS.md` v1.2.0 |

## 1. Resumen técnico

Exponer un asistente conversacional en dos mitades. La **recuperación** de ofertas candidatas y el
**cálculo** del paquete por presupuesto ocurren de forma determinista en `Sensus.Application` y
`Sensus.Domain`, reutilizando los índices `2dsphere` de la spec 007 y las reglas de visibilidad ya
definidas. La **redacción** la produce el proveedor generativo, que recibe el mensaje y el conjunto
cerrado de candidatos, y cuya salida se valida antes de devolverse: se descartan los identificadores
ajenos al conjunto y se recalcula el total. Entrada y salida pasan por moderación *fail-closed*.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `AiConversation`, `AiMessage`, `BudgetPackage` y el servicio puro `BudgetPackageSelector` |
| Application | `StartConversationCommand`, `SendAssistantMessageCommand`, `GetConversationQuery`, interpretación del presupuesto, recuperación de candidatos, validación de la salida del proveedor e interfaz `IGenerativeTextComposer` |
| Infrastructure | Adaptador `OpenAiTextComposer`, repositorios Mongo de conversaciones y mensajes, lectura de la clave desde Secrets Manager y caché de respuestas equivalentes |
| Api | `POST /api/v1/ai/conversations`, `POST /api/v1/ai/messages`, `GET /api/v1/ai/conversations/{id}` |

El proveedor generativo queda tras `IGenerativeTextComposer`: sustituirlo solo afecta a
`Sensus.Infrastructure` (P-II, §2.1 de la constitución).

## 3. Modelo de datos e índices

- `ai_conversations`: `userId`, `createdAt`, `updatedAt`. Índice `{ userId: 1, updatedAt: -1 }`.
- `ai_messages`: `conversationId`, `role`, `text`, `packageItems`, `createdAt`. Índice
  `{ conversationId: 1, createdAt: 1 }`.
- `packageItems` guarda identificadores de oferta e importes al momento de la respuesta; no duplica el
  catálogo.
- Purga por `userId` en el flujo de la spec 030.
- Caché de respuestas equivalentes con TTL configurable mediante `ICacheService` (spec 048).

## 4. Seguridad

- Clave del proveedor en AWS Secrets Manager; nunca en el repositorio, en `appsettings` ni en el cliente.
- Entrada y salida moderadas con el contrato de §2.4, *fail-closed*.
- El proveedor no recibe credenciales, no accede a MongoDB y no ejecuta herramientas (§2.2).
- El prompt MUST NOT incluir PII: ni correo, ni nombre, ni identificadores de la persona; la ubicación
  viaja con la precisión mínima necesaria.
- Logs sin contenido de mensajes ni texto generado; solo metadatos y `correlation-id`.
- Sesión invitada rechazada antes de consumir cuota.
- Entrenamiento del proveedor sobre los datos desactivado por configuración de cuenta.

## 5. Observabilidad y rendimiento

- Métricas de latencia de extremo a extremo y por proveedor, tasa de degradación determinista, tokens
  consumidos, mensajes por persona y aciertos de caché.
- Alarma de facturación al 80 % del techo mensual (`NFR-046-02`).
- Contador de identificadores descartados por no pertenecer al conjunto de candidatos: su crecimiento
  sostenido indica deriva del prompt.
- Control de tasa según spec 013 y límite diario por persona.

## 6. Costo AWS

Lambda, API Gateway y MongoDB sin costo incremental dentro del Free Tier. El proveedor generativo tiene
costo variable y constituye una **excepción registrada a P-VI** (`EX-001`, Apéndice E de la
constitución), acotada por techo de gasto mensual de 1.00 USD, límite de tokens por solicitud, límite de
mensajes por persona y día, caché y alarma de facturación.

## 7. Estrategia de pruebas

- Unitarias de `BudgetPackageSelector`: suma ≤ presupuesto, preferencia por mayor aprovechamiento del
  monto, estabilidad del orden y comportamiento con conjunto vacío.
- Unitarias de la interpretación del presupuesto: montos válidos, ausentes, no positivos y ambiguos.
- Unitarias de la validación de salida: identificadores ajenos descartados y total recalculado.
- Integración con proveedor simulado: respuesta válida, respuesta con alucinación, timeout, cuota
  agotada y respuesta inválida.
- Integración de moderación *fail-closed* en entrada y en salida.
- Integración de exclusión de ofertas no visibles y de purga por eliminación de cuenta.
- Integración de límites: 429 por tasa y por límite diario.
- Ninguna prueba evalúa la redacción del proveedor, que no es determinista.

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-046-01` … `FR-046-17` y `NFR-046-01` … `NFR-046-07` con criterios medibles en §6 del spec |
| II | PASS | Cambios acotados a las capas de §2; proveedor tras `IGenerativeTextComposer` |
| III | PASS | Comandos, consultas y DTOs separados; sin exponer entidades de dominio |
| IV | PASS | Moderación de entrada y salida, Secrets Manager, sin PII en prompt ni en logs (§4) |
| V | PASS | Estrategia de pruebas en §7, con el proveedor simulado |
| VI | PASS CON OBSERVACIONES | Costo variable del proveedor; excepción `EX-001` del Apéndice E con techo, límites y alarma (§6) |
| VII | PASS | Índices, caché, paginación del historial y métricas en §3 y §5 |

Resultado global: **PASS CON OBSERVACIONES**

> Excepción registrada: `EX-001` (P-VI) en el Apéndice E de la constitución.

## 9. Riesgos de implementación

- El techo de gasto puede agotarse antes del cierre de la spec y dejar la capacidad en degradación
  permanente
- La interpretación del presupuesto en lenguaje natural es la pieza más frágil del flujo
- La latencia sumada de moderación de entrada, generación y moderación de salida puede acercarse al
  tiempo de espera máximo
- El caché puede devolver un paquete con ofertas recién agotadas; el TTL debe ser corto

## 10. Orden de implementación

1. `T046-01` Definir y validar el contrato OpenAPI del spec
2. `T046-02` Implementar: `AiConversation`, `AiMessage`, `BudgetPackage` y `BudgetPackageSelector`
3. `T046-03` Implementar: interpretación del presupuesto y recuperación determinista de candidatos
4. `T046-04` Implementar: `IGenerativeTextComposer` y la validación de su salida
5. `T046-05` Implementar: adaptador del proveedor con límite de tokens y tiempo de espera
6. `T046-06` Implementar: repositorios Mongo e índices de conversaciones y mensajes
7. `T046-07` Implementar: moderación de entrada y salida *fail-closed*
8. `T046-08` Implementar: degradación determinista ante fallo del proveedor
9. `T046-09` Implementar: los tres endpoints con autorización y rechazo de invitados
10. `T046-10` Implementar: límites de tasa, límite diario y caché de respuestas equivalentes
11. `T046-11` Implementar: purga de conversaciones en el flujo de la spec 030
12. `T046-12` Implementar: métricas, alarma de facturación y logs sin PII
13. `T046-13` Implementar las pruebas unitarias y de integración del flujo completo
14. `T046-14` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
15. `T046-15` Registrar evidencias del Constitution Check y de la excepción `EX-001`
