# Tareas 006 — Consulta de mi comercio

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T006-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-006-01` | — | Completada (`specs/006-consulta-mi-comercio/contracts/openapi.yaml`; `OpenApiContractTests`) |
| T006-02 | Retirar el endpoint legacy /api/v1/comercio/mi-comercio | Api | `FR-006-01` | T006-01 | Completada (retirado por Spec 008; `SecurityPipelineTests.RetiredLegacyRoutes_ShouldReturnNotFound`) |
| T006-03 | Probar el caso 404 | Application | `FR-006-02` | T006-02 | Completada (`GetMyComercioQueryHandlerTests` + `MerchantProfileEndpointsTests.GetMyMerchant_ShouldReturnNotFound_WhenMerchantDoesNotExist`) |
| T006-04 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-006-01` | T006-03 | Completada (`MerchantProfileEndpointsTests` 200/404, `ProblemDetailsUniformityTests`) |
| T006-05 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-006-01` | T006-04 | Completada |
| T006-06 | Registrar evidencias del Constitution Check | Docs | `FR-006-01` | T006-05 | Completada (`plan.md` §4 STRIDE + CI `4a00d24`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
