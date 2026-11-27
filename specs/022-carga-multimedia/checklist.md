# Checklist 022 — Carga de multimedia

Alineado a `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 022 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 017, 013
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La subida en Cloudinary se realiza con firma segura SHA-256
- [x] El tipo se valida por firma, no por extensión
- [x] El límite de tamaño está probado
- [x] La imagen se modera antes de exponerse

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde (`UploadMediaCommandHandlerTests`, `MediaEndpointsTests`, `MongoMediaAssetRepositoryTests`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica; SSM Cloudinary)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada (`type=authenticated` + `410` `/users/photo-signature`)
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (moderación previa a Cloudinary, `authenticated` delivery y CI `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| — | — | — | — |
| 2026-11-26 | Antigravity | T022-12 completada (H2) | `UploadMediaCommandHandler` modera bytes antes de `IMediaStorage.UploadImageAsync` (sin subida en `Quarantined`/`Rejected`), `CloudinaryMediaStorage` firma y sube con `type=authenticated`, `MediaEndpoints.UploadMedia` aplica `SensitiveWritePolicy`, móvil `movil_Rodrigo` (`MediaRepositoryImpl.kt`, `RetrofitRepositories.kt`, `SensusContainer.kt`) usa `/api/v1/media/upload` sin fallback directo a Cloudinary; plan de migración de activos públicos documentado en `plan.md`. |
| 2026-11-26 | Ejecutor cierre specs | T022-13 completada (`FR-022-08`) | Retirado `/api/v1/users/photo-signature` (`410 Gone`, `Media.SignatureRetired`), eliminado `GeneratePhotoSignatureCommand` y añadido `IsApprovedOwnedUrlAsync` en `MongoMediaAssetRepository` (`ix_media_assets_owner_secure_url`, Testcontainers). |
