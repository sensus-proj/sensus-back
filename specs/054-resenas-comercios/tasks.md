# Tasks 054 — Reseñas y calificaciones de comercios

| ID | Requisito | Descripción | Estado |
|---|---|---|---|
| `T054-01` | `FR-054-01..06` | Entidades `StoreReview`/`StoreReviewReply`, handlers CQRS, `MongoStoreReviewRepository` y `StoreReviewEndpoints` | `[x]` Completado |
| `T054-02` | `FR-054-01..04` | Contrato OpenAPI 3.1 en `contracts/openapi.yaml` | `[x]` Completado |
| `T054-03` | `FR-054-06` | Referencias `FR-054-*` en código y verificación de `AverageRating`/`ReviewCount` reales en `ComercioDto` | `[x]` Completado |
| `T054-04` | `NFR-054-01..03` | Pruebas de integración en `SocialEndpointsIntegrationTests` y persistencia en `MongoSocialRepositoriesTests` (verificación final en runner Linux de CI) | `[x]` Completado (compilado; CI pendiente) |
| `T054-05` | `FR-054-07` | Agregar paginación `PagedResult` a `GetStoreReviewsQuery` (deuda retro-spec) | `[ ]` Pendiente |
