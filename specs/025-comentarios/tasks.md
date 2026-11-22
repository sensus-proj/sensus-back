# Tareas 025 — Comentarios del feed

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T025-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-025-01` | — | Completada |
| T025-02 | Implementar: Entidad Comment | Domain | `FR-025-01` | T025-01 | Completada (`FeedComment`) |
| T025-03 | Implementar: Comandos y consultas paginadas | Application | `FR-025-01` | T025-02 | Completada (`CreateFeedCommentCommand`, `GetPostCommentsQuery`) |
| T025-04 | Implementar: Persistencia con índice por postId y fecha | Infrastructure | `FR-025-01` | T025-03 | Completada (`MongoFeedCommentRepository`, `MongoFeedPostVisibilityLookup`) |
| T025-05 | Implementar: Endpoints de comentarios | Api | `FR-025-01` | T025-04 | Completada (`FeedEndpoints`: `POST`/`GET /api/v1/feed/posts/{id}/comments`) |
| T025-06 | Modelar Comment | Domain | `FR-025-01` | T025-05 | Completada |
| T025-07 | Crear índice compuesto postId + createdAt | Infrastructure | `FR-025-03` | T025-06 | Completada (`ix_feed_comments_post_created_at`) |
| T025-08 | Probar el 404 en publicaciones no visibles | Api | `FR-025-04` | T025-07 | Completada (`FeedCommentsTests`, `FeedCommentEndpointsTests`) |
| T025-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-025-01` | T025-08 | Completada (`FeedCommentEndpointsTests`) |
| T025-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-025-01` | T025-09 | Completada |
| T025-11 | Registrar evidencias del Constitution Check | Docs | `FR-025-01` | T025-10 | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
