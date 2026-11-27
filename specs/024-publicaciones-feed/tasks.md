# Tareas 024 — Publicaciones del feed

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completada |
| Versión | 2.3.0 |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T024-01 | Actualizar y validar el contrato OpenAPI del spec (JSON y `multipart/form-data`, formatos permitidos, 201/400/413) | Api | `FR-024-01`, `FR-024-04` | — | Completada (`specs/024-publicaciones-feed/contracts/openapi.yaml`) |
| T024-02 | Implementar: entidad `FeedPost` con estados, `ImagePublicId` e invariantes de autoría en Domain | Domain | `FR-024-01`, `FR-024-03` | T024-01 | Completada (`FeedPost.cs`, `FeedPostStatus.cs`, `IFeedPostRepository.cs`) |
| T024-03 | Implementar: DTOs, `CreateFeedPostCommand` (moderación síncrona y subida) y `DeleteFeedPostCommand` (purga) en Application | Application | `FR-024-01`, `FR-024-02`, `FR-024-04`, `FR-024-05`, `FR-024-06` | T024-02 | Completada (`FeedPostDtos.cs`, `CreateFeedPostCommand.cs`, `DeleteFeedPostCommand.cs`) |
| T024-04 | Implementar: `FeedPostDocument` (`imagePublicId`), índices, `MongoFeedPostRepository` y lookup de visibilidad en Infrastructure | Infrastructure | `FR-024-03`, `FR-024-07` | T024-03 | Completada (`FeedPostDocument.cs`, `MongoFeedPostRepository.cs`, `MongoFeedPostVisibilityLookup.cs`, `MongoDbContext.cs`, `DependencyInjection.cs`) |
| T024-05 | Implementar: endpoints `POST /api/v1/feed/posts` (JSON y multipart con lista de permitidos y 413) y `DELETE /api/v1/feed/posts/{id}` en Api | Api | `FR-024-01`, `FR-024-04`, `FR-024-05` | T024-04 | Completada (`FeedEndpoints.cs`) |
| T024-06 | Implementar: pruebas unitarias de dominio y aplicación | Pruebas | `FR-024-01`, `FR-024-02`, `FR-024-04`, `FR-024-05` | T024-05 | Completada (`tests/Sensus.Application.UnitTests/Feed/FeedPostsTests.cs` — 12 superadas) |
| T024-07 | Implementar: pruebas de integración del flujo completo, multipart, purga y persistencia real | Pruebas | `FR-024-01`, `FR-024-02`, `FR-024-04`, `FR-024-05`, `FR-024-06`, `FR-024-07` | T024-06 | Completada (`tests/Sensus.IntegrationTests/Feed/FeedPostEndpointsTests.cs` — 11 superadas; `tests/Sensus.IntegrationTests/Persistence/MongoFeedPostVisibilityLookupTests.cs` — 7 superadas) |
| T024-08 | Actualizar `checklist.md`, `specs/README.md`, `requirements.md` y `memory.md` | Docs | `FR-024-04` | T024-07 | Completada |
| T024-09 | Verificar/integrar moderación de solo texto (sin imagen) y moderación conjunta (con imagen) antes de publicar o almacenar multimedia, con cuarentena ante fallos (`constitution.md` v3.0.0) | Application | `FR-024-02` | T024-03, T017-12 | Completada (`CreateFeedPostCommand.cs`, `ModerationTargetType.FeedPost`) |
| T024-10 | Rechazar `imageUrl` externa en JSON y validar `mediaAssetId` aprobado del autor (`IMediaAssetRepository`), más actualización móvil `FeedDtos.kt` | Application / Api / Móvil | `FR-024-08` | T024-08 | Completada |
| T024-11 | Persistir `FeedPostStatus.Quarantined` (202 Accepted) sin subir imagen ante indisponibilidad del moderador, registrar auditoría `FeedPost` y purgar cuarentena vencida (30 d) | Domain / Application / Infrastructure / Api / Pruebas | `FR-024-09` | T024-09 | Completada (`FeedPostsTests`, `FeedPostEndpointsTests`, `MongoFeedSocialIntegrationTests`, `PurgeQuarantinedMediaCommandHandler`) |
| T024-12 | Retirar alias `mediaId` en `FeedEndpoints` con `400 Bad Request` (`Feed.MediaIdRetired`) | Api / Pruebas | `FR-024-10` | T024-10 | Completada (`FeedEndpoints.cs`, `FeedPostEndpointsTests`) |
| T024-13 | Soportar etiqueta opcional de comercio activo (`merchantId`, `merchantName`) en JSON/multipart de `POST /api/v1/feed/posts` | Domain / Application / Infrastructure / Api / Pruebas | `FR-024-11` | T024-05 | Completada (`FeedPost.cs`, `FeedPostDocument.cs`, `CreateFeedPostCommand.cs`, `FeedEndpoints.cs`, `FeedPostEndpointsTests`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- Revisión 2026-11-23: `FR-024-04` se ajustó de `202` a `201/400` porque la implementación ya modera de forma síncrona; la imagen se sube solo tras aprobación y se purga al borrar.