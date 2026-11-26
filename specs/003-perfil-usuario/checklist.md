# Checklist 003 — Perfil del usuario autenticado

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 003 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 002, 022
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La respuesta no contiene passwordHash (`UserProfileDto`, `ProfileEndpointsTests`)
- [x] El endpoint está marcado como protegido en OpenAPI (`specs/003-perfil-usuario/contracts/openapi.yaml`)
- [x] El correo no se puede modificar desde el perfil (`User.UpdateProfile`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`Sensus.ArchitectureTests` 6/6)
- [x] Pruebas unitarias y de integración añadidas y en verde (`UpdateProfile*` 20/20, `ProfileEndpointsTests`, `MongoMediaAssetRepositoryTests`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado (`v1.2.0`)
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada (`410` en `/users/photo-signature`, foto exigida `Approved` en `MediaAssets`)
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (STRIDE en `plan.md` §4 y escaneos CI bloqueantes verdes `ci.yml`/`dast-zap.yml`, `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Users/` (`UpdateProfile*` 20/20, `GetProfileQueryHandlerTests`) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Users/ProfileEndpointsTests.cs`, `tests/Sensus.IntegrationTests/Persistence/MongoMediaAssetRepositoryTests.cs` | ☑ |
| Contrato OpenAPI | `specs/003-perfil-usuario/contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Ejecutor cierre specs | T003-09..T003-11 completadas (`FR-003-06`..`FR-003-10`) | Endurecido `UpdateProfileCommandValidator` (`https://res.cloudinary.com/<CloudName>/`), validación `IMediaAssetRepository.IsApprovedOwnedUrlAsync` con índice `ix_media_assets_owner_secure_url` (Testcontainers `MongoMediaAssetRepositoryTests`), retiro `410 Gone` en `/api/v1/users/photo-signature` y eliminación de `GeneratePhotoSignatureCommand`. |
