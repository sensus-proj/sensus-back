# Tareas 005 — Activación del rol comercial

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
| T005-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-005-01` | — | Completada (`specs/005-activacion-comerciante/contracts/openapi.yaml`; `OpenApiContractTests`) |
| T005-02 | Documentar invariantes de Merchant y ubicación | Domain | `FR-005-02` | T005-01 | Completada (`Comercio.cs`; `ComercioTests`) |
| T005-03 | Retirar el endpoint legacy /api/v1/comercio/registro | Api | `FR-005-01` | T005-02 | Completada (retirado por Spec 008; sin coincidencias en `src/Sensus.Api`; `SecurityPipelineTests.RetiredLegacyPostComercioRegistro_ShouldReturnNotFound`) |
| T005-04 | Probar el conflicto de comercio duplicado | Application | `FR-005-03` | T005-03 | Completada (`RegisterComercioCommandHandlerTests`, `MerchantProfileEndpointsTests.ActivateMerchant_ShouldReturnConflict_WhenUserAlreadyHasComercio`) |
| T005-05 | Verificar el índice 2dsphere con explain | Infrastructure | `FR-005-06` | T005-04 | Completada (`MongoDbContext.cs:304-312`; `MongoUserAndComercioRepositoryTests.Comercios_HasUniqueUsuarioIndex_AndUses2dSphereInExplain` Testcontainers `GEO_NEAR_2DSPHERE`) |
| T005-06 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-005-01` | T005-05 | Completada (`MerchantProfileEndpointsTests` 15/15, `MongoUserAndComercioRepositoryTests` 2/2) |
| T005-07 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-005-01` | T005-06 | Completada |
| T005-08 | Registrar evidencias del Constitution Check | Docs | `FR-005-01` | T005-07 | Completada (`plan.md` §4 STRIDE + CI `4a00d24`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
