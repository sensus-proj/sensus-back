# Tareas 003 — Perfil del usuario autenticado

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
| T003-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-003-01` | — | Completada (`specs/003-perfil-usuario/contracts/openapi.yaml` v1.2.0; `OpenApiContractTests`) |
| T003-02 | Cubrir 200 y 401 en pruebas de integración | Api | `FR-003-03` | T003-01 | Completada (`ProfileEndpointsTests`) |
| T003-03 | Probar que el DTO no contiene campos sensibles | Application | `FR-003-02` | T003-02 | Completada (`GetProfileQueryHandlerTests`, `UserProfileDto`) |
| T003-04 | Modelar la inmutabilidad de correo y roles | Domain | `FR-003-05` | T003-03 | Completada (`User.UpdateProfile` solo modifica nombre y avatar; `User.cs`) |
| T003-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-003-01` | T003-04 | Completada (`ProfileEndpointsTests`, `MongoMediaAssetRepositoryTests`) |
| T003-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-003-01` | T003-05 | Completada |
| T003-07 | Registrar evidencias del Constitution Check | Docs | `FR-003-01` | T003-06 | Completada (`plan.md` §4 STRIDE + CI `4a00d24`) |
| T003-08 | Documentar `POST /users/photo-signature` (retro-spec) y su contrato | Docs | `FR-003-07` | — | Hecho |
| T003-09 | Validar `fotoPerfilUrl` contra host exacto `res.cloudinary.com` y `<CloudName>` configurado en `UpdateProfileCommandValidator` | Application | `FR-003-06` | T003-08 | Completada (`UpdateProfileCommandValidator.cs`, `CloudinaryAccountSettings.cs`, `UpdateProfileCommandValidatorTests` 12/12) |
| T003-10 | Exigir `MediaAsset` `Approved` del propio usuario (`IsApprovedOwnedUrlAsync` e índice `ix_media_assets_owner_secure_url`) o `null`/vacío en `UpdateProfileCommandHandler` | Domain / Application / Infrastructure | `FR-003-08`, `FR-003-10` | T003-08 | Completada (`UpdateProfileCommandHandler.cs`, `MongoMediaAssetRepository.cs`, `MongoMediaAssetRepositoryTests`, `ProfileEndpointsTests`) |
| T003-11 | Retirar `POST /api/v1/users/photo-signature` con `410 Gone` (`Media.SignatureRetired`), eliminar `GeneratePhotoSignatureCommand` y conservar límite de tasa `SensitiveWritePolicy` | Api / Application | `FR-003-07`, `FR-003-09` | T003-08 | Completada (superada por el retiro con `410 Gone` (`FR-003-07`); `UserEndpoints.cs`, `ProfileEndpointsTests`, `ConsumerE2EJourneyTests`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
