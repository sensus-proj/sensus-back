# Tareas 031 — Edición del perfil de comercio

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
| T031-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-031-01` | — | Completada (`specs/031-edicion-perfil-comercio/contracts/openapi.yaml`; `OpenApiContractTests`) |
| T031-02 | Implementar: Invariantes de Merchant y recálculo de ubicación | Domain | `FR-031-01` | T031-01 | Completada (`Comercio.UpdateDetails`; `ComercioTests`) |
| T031-03 | Implementar: UpdateMerchantProfileCommand y validador | Application | `FR-031-01` | T031-02 | Completada (`UpdateMerchantProfileCommandHandler.cs`, `UpdateMerchantProfileCommandValidator.cs`) |
| T031-04 | Implementar: Actualización atómica y recálculo de geohash | Infrastructure | `FR-031-01` | T031-03 | Completada (`MongoComercioRepository.UpdateAsync`, `ComercioDocument`) |
| T031-05 | Implementar: Endpoint PUT /api/v1/merchants/profile | Api | `FR-031-01` | T031-04 | Completada (`MerchantEndpoints.cs:64-73`) |
| T031-06 | Implementar el recálculo de location y geohash | Domain | `FR-031-02` | T031-05 | Completada (`UpdateMerchantProfileCommandHandlerTests`, `ComercioTests`) |
| T031-07 | Validar categoría y coordenadas | Application | `FR-031-03` | T031-06 | Completada (`UpdateMerchantProfileCommandValidatorTests`, `MerchantProfileEndpointsTests` 400 categoría/coordenadas/CP) |
| T031-08 | Probar el rechazo de edición sobre comercios ajenos | Api | `FR-031-04` | T031-07 | Completada (`MerchantProfileEndpointsTests` 403 consumidor/invitado y alcance estricto por JWT) |
| T031-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-031-01` | T031-08 | Completada (`MerchantProfileEndpointsTests`) |
| T031-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-031-01` | T031-09 | Completada |
| T031-11 | Registrar evidencias del Constitution Check | Docs | `FR-031-01` | T031-10 | Completada (`plan.md` §4 STRIDE + CI `4a00d24`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
