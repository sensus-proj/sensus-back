# Tareas 039 — Me gusta en publicaciones

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Backend completado |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T039-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-039-01` | — | Completada. `contracts/openapi.yaml` v1.1.0 con esquemas de petición, respuesta y errores. |
| T039-02 | Implementar: Regla de me gusta único por usuario | Domain | `FR-039-01` | T039-01 | Completada. `FeedPostLike` y `IFeedPostLikeRepository`. |
| T039-03 | Implementar: ToggleLikeCommand y cálculo del conteo | Application | `FR-039-01` | T039-02 | Completada. `SetFeedPostLikeCommand` con intención explícita `liked`; conteo acotado a cero. |
| T039-04 | Implementar: Índice único postId + userId y contador atómico | Infrastructure | `FR-039-01` | T039-03 | Completada. `MongoFeedPostLikeRepository` e índice `ux_feed_post_likes_post_user`. Conteo derivado de registros únicos (decisión en `plan.md` §3). |
| T039-05 | Implementar: Endpoint POST /api/v1/feed/posts/{id}/like | Api | `FR-039-01` | T039-04 | Completada. `FeedEndpoints`, política `post-likes`. |
| T039-06 | Modelar la unicidad del me gusta | Domain | `FR-039-04` | T039-05 | Completada |
| T039-07 | Implementar el índice único y el contador atómico | Infrastructure | `FR-039-05` | T039-06 | Completada. Inserción duplicada concurrente resuelta como "ya existía". |
| T039-08 | Probar la alternancia y el 404 | Api | `FR-039-03` | T039-07 | Completada. `FeedPostLikeEndpointsTests` (200, 400, 401, 403, 404, 429). |
| T039-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-039-01` | T039-08 | Parcial. Escritas en `MongoFeedSocialIntegrationTests` (unicidad, idempotencia, concurrencia `CA-039-04`); pendientes de ejecutar con Docker o en CI. |
| T039-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-039-01` | T039-09 | Completada |
| T039-11 | Registrar evidencias del Constitution Check | Docs | `FR-039-01` | T039-10 | Parcial. Principio VIII pendiente de los escaneos bloqueantes de CI. |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
