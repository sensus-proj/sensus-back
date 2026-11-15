# Tareas 046 — Asistente conversacional Sensi

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Pendiente |
| Alineado a | `constitution.md` v1.4.0 · `AGENTS.md` v1.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T046-01 | Definir y validar el contrato OpenAPI del spec | Api | `FR-046-01` | — | Pendiente |
| T046-02 | Implementar: `AiConversation`, `AiMessage`, `BudgetPackage` y `BudgetPackageSelector` | Domain | `FR-046-06` | T046-01 | Pendiente |
| T046-03 | Implementar: interpretación del presupuesto y recuperación determinista de candidatos | Application | `FR-046-04`, `FR-046-05` | T046-02 | Pendiente |
| T046-04 | Implementar: `IGenerativeTextComposer` y la validación de su salida | Application | `FR-046-07` | T046-03 | Pendiente |
| T046-05 | Implementar: adaptador del proveedor con límite de tokens y tiempo de espera | Infrastructure | `FR-046-11`, `FR-046-12`, `FR-046-16` | T046-04 | Pendiente |
| T046-06 | Implementar: repositorios Mongo e índices de conversaciones y mensajes | Infrastructure | `FR-046-10` | T046-02 | Pendiente |
| T046-07 | Implementar: moderación de entrada y salida *fail-closed* | Application | `FR-046-03`, `FR-046-08` | T046-05 | Pendiente |
| T046-08 | Implementar: degradación determinista ante fallo del proveedor | Application | `FR-046-14` | T046-07 | Pendiente |
| T046-09 | Implementar: los tres endpoints con autorización y rechazo de invitados | Api | `FR-046-01`, `FR-046-02` | T046-06, T046-08 | Pendiente |
| T046-10 | Implementar: límites de tasa, límite diario y caché de respuestas equivalentes | Api | `FR-046-11`, `FR-046-17` | T046-09 | Pendiente |
| T046-11 | Implementar: purga de conversaciones en el flujo de la spec 030 | Infrastructure | `FR-046-15` | T046-06 | Pendiente |
| T046-12 | Implementar: métricas, alarma de facturación y logs sin PII | Infrastructure | `FR-046-13` | T046-10 | Pendiente |
| T046-13 | Implementar las pruebas unitarias y de integración del flujo completo | Pruebas | `FR-046-06`, `FR-046-07` | T046-12 | Pendiente |
| T046-14 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-046-01` | T046-13 | Pendiente |
| T046-15 | Registrar evidencias del Constitution Check y de la excepción `EX-001` | Docs | `FR-046-01` | T046-14 | Pendiente |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- Ninguna tarea introduce llamadas al proveedor generativo desde `Sensus.Domain` ni desde
  `Sensus.Application`: el adaptador vive en `Sensus.Infrastructure` (P-II).
- `T046-05` MUST NOT quedar cerrada con la clave del proveedor fuera de AWS Secrets Manager.
