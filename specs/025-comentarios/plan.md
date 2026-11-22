# Plan 025 — Comentarios del feed

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Permitir comentar publicaciones visibles del feed con moderación textual preventiva (*fail-closed*) y listar los comentarios paginados en orden cronológico ascendente mediante `PagedResult<FeedCommentDto>`. La visibilidad de la publicación padre se consulta mediante el puerto `IFeedPostVisibilityLookup`, desacoplando `025` de la implementación completa de `024`.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `FeedComment` (máx. 300 chars, creación/rehidratación y revisión) e interfaz `IFeedCommentRepository` |
| Application | `CreateCommentCommand` (+ validador FluentValidation), `GetPostCommentsQuery` (+ validador paginación), `FeedCommentDto`, `IFeedPostVisibilityLookup` |
| Infrastructure | `FeedCommentDocument`, `MongoFeedCommentRepository` (índice compuesto `PostId asc + CreatedAtUtc asc`), `MongoFeedPostVisibilityLookup` sobre `feed_posts`, registro DI |
| Api | Endpoints `POST /api/v1/feed/posts/{id:guid}/comments` (`201`) y `GET /api/v1/feed/posts/{id:guid}/comments` (`200`) en `FeedEndpoints.cs` |
| Tests | `FeedCommentHandlersTests`, `FeedCommentsEndpointsTests` y `MongoFeedAndReportRepositoriesTests` |

## 3. Modelo de datos e índices

- Colección `feed_comments` (`FeedCommentDocument`): `Id`, `PostId`, `AuthorUserId`, `AuthorName`, `AuthorPhotoUrl`, `Content`, `NeedsReview`, `CreatedAtUtc`.
- Índice compuesto en `MongoDbContext`: `(PostId asc, CreatedAtUtc asc)` (`ix_feed_comments_post_created`).

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Publicación de groserías o contenido abusivo en comentarios | Manipulación | `ProfanityFilter` + `ITextModerationService` (*fail-closed*) antes de persistir (`FR-025-02`) | Bajo |
| Suplantación de autoría en payload | Suplantación | `authorUserId` y nombre resueltos exclusivamente desde claims JWT + `IUserRepository` | Bajo |
| Comentar sobre publicaciones ocultas, eliminadas o en revisión | Elevación / Bypass | Verificación previa en `IFeedPostVisibilityLookup` devolviendo `404 NotFound` (`FR-025-04`) | Bajo |
| Extracción masiva o abuso de escritura | Denegación de servicio | `RegisteredUserPolicy`, paginación 20/100 (`FR-010-02`) y rate limiting `ReadHeavyPolicy` / `SensitiveWritePolicy` | Bajo |
| Exposición de correo o PII del autor | Divulgación de información | `FeedCommentDto` expone solo `AuthorUserId`, `AuthorName` y `AuthorPhotoUrl`; logs sin texto ni PII | Bajo |

## 5. Observabilidad y rendimiento

- Consulta paginada `Skip`/`Limit` respaldada por índice `(PostId, CreatedAtUtc asc)` evitando full-scan.
- Logs estructurados con `correlation-id` sin texto del comentario ni PII.

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway HTTP API dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias (`Sensus.Application.UnitTests`): invariantes de `FeedComment`, límite 300 chars, rechazo por groserías/moderación fail-closed, 404 en post inexistente/oculto y paginación 20/100 ascendente.
- Integración (`Sensus.IntegrationTests`): `POST`/`GET /api/v1/feed/posts/{id}/comments` (201, 200 paginado, 400, 401, 403 invitado, 404) y persistencia real en MongoDB.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | `FR-025-01`..`FR-025-06` y `NFR-025-01`..`02` vinculados en código |
| II. Clean Architecture | PASS | Reglas en Domain, CQRS en Application, Mongo en Infrastructure |
| III. CQRS y contratos | PASS | `CreateCommentCommand`, `GetPostCommentsQuery`, `PagedResult<FeedCommentDto>` |
| IV. Seguridad y moderación | PASS | `RegisteredUserPolicy`, `ContentModerationHelper` *fail-closed* |
| V. Verificación automatizada | PASS | Cobertura ≥ 80 % en Domain/Application + integración |
| VI. Costo AWS | PASS | 0.00 USD incrementales |
| VII. Paginación e índices | PASS | `PagedResult` (20/100) + índice compuesto `(PostId, CreatedAtUtc)` |
| VIII. DevSecOps / STRIDE | PASS | STRIDE evaluado; límites de tamaño y sin secretos |
