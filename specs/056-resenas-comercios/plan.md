# Plan 056 — Reseñas y calificaciones de comercios (`StoreReview`)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Completo |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Se introduce el agregado `StoreReview` (con entidad hija `StoreReviewReply`) en `Sensus.Domain`, los comandos y consulta CQRS (`CreateStoreReviewCommand`, `ReplyStoreReviewCommand`, `ToggleStoreReviewLikeCommand`, `GetStoreReviewsQuery`) en `Sensus.Application`, el repositorio `MongoStoreReviewRepository` sobre la colección `store_reviews` en `Sensus.Infrastructure` y los endpoints REST bajo `/api/v1/merchants` en `StoreReviewEndpoints.cs`. Además, `GetMyComercioQueryHandler` y `GetNearbyComerciosQueryHandler` calculan el promedio y conteo real desde `IStoreReviewRepository`.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidades `StoreReview`, `StoreReviewReply`, interfaz `IStoreReviewRepository`, record `MerchantRatingSummary` |
| Application | `CreateStoreReviewCommand`, `ReplyStoreReviewCommand`, `ToggleStoreReviewLikeCommand`, `GetStoreReviewsQuery`, `StoreReviewDtos`, `StoreReviewMappingExtensions`, actualización de `GetMyComercioQueryHandler`, `GetNearbyComerciosQuery` y `ComercioDto` |
| Infrastructure | `StoreReviewDocument`, `StoreReviewReplySubDocument`, `MongoStoreReviewRepository`, colección e índice en `MongoDbContext`, registro DI |
| Api | `StoreReviewEndpoints.cs` mapeado en `Program.cs` |
| Tests | `SocialHandlersTests`, `SocialEndpointsIntegrationTests`, `MongoSocialRepositoriesTests`, actualización de `CustomWebApplicationFactory` |

## 3. Modelo de datos e índices

- Colección MongoDB `store_reviews` (`StoreReviewDocument`): `Id` (UUID), `MerchantId` (UUID), `AuthorUserId` (UUID), `AuthorName`, `AuthorPhotoUrl`, `Rating` (1..5), `Comment`, `CreatedAtUtc`, `LikedByUserIds` (`List<Guid>`), `Replies` (`List<StoreReviewReplySubDocument>`).
- Índice compuesto creado en `MongoDbContext`: `(MerchantId asc, CreatedAtUtc desc)`.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Publicación de groserías, insultos o XSS en reseñas/respuestas | Manipulación | `ProfanityFilter` + `ITextModerationService` (*fail-closed*) y límite de 600/500 caracteres | Bajo |
| Invitado intenta calificar, responder o dar like | Elevación de privilegios | Política `RegisteredUserPolicy` en todos los `POST` y verificación `_currentUserService.IsGuest` | Bajo |
| Spam masivo de reseñas/likes (DoS / abuso) | Denegación de servicio | Rate limiting `SensitiveWritePolicy` en escrituras y `ReadHeavyPolicy` en lecturas | Medio (sin límite 1 reseña/usuario, asumible en MVP) |
| Exposición de correo o PII del autor | Divulgación de información | `StoreReviewDto` solo expone `AuthorUserId`, `AuthorName` y `AuthorPhotoUrl` | Bajo |

## 5. Observabilidad y rendimiento

- Índice por `MerchantId` + `CreatedAtUtc desc` evita escaneos completos.
- `CreateStoreReviewCommandHandler` invalida caché del perfil comercial tras cada nueva reseña.

## 6. Costo AWS

- 0.00 USD incrementales: reutiliza MongoDB Atlas y la función Lambda HTTP API existente.

## 7. Estrategia de pruebas

- Unitarias (`Sensus.Application.UnitTests`): validación de rango 1..5, rechazo por groserías, cálculo de promedio/desglose Play Store, alternancia de like y respuestas.
- Integración (`Sensus.IntegrationTests`): endpoints `GET/POST /reviews`, `/like`, `/replies` con códigos 200/400/401/403/404 y persistencia en `store_reviews`.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec `056-resenas-comercios` con requisitos `FR-056-*` / `NFR-056-*` |
| II. Clean Architecture | PASS | `Sensus.ArchitectureTests` sin dependencias prohibidas |
| III. Calidad y pruebas | PASS | MediatR + `Result<T>` + DTOs explícitos; sin advertencias |
| IV. Seguridad y moderación | PASS | `RegisteredUserPolicy`, `SensitiveWritePolicy`, moderación fail-closed |
| V. Cobertura y pruebas reales | PASS | Unitarias + integración endpoints + MongoDB real (`MONGO_CONNECTION_STRING`) |
| VI. Costo AWS | PASS | 0 USD adicionales |
| VII. Paginación e índices | PASS | `FR-056-07` implementado: paginación en BD (`Skip`/`Limit`/`CountDocumentsAsync`), agregación de estrellas (`$match+$group`) e índice compuesto `(MerchantId, CreatedAtUtc)` |
| VIII. DevSecOps / STRIDE | PASS | Matriz STRIDE evaluada y validaciones de longitud/rango |

## 9. Riesgos de implementación

- Comercios con cientos de reseñas requerirán implementar `FR-056-07` (`PagedResult`) en una iteración siguiente sin romper el contrato del resumen de cabecera.

## 10. Orden de implementación

1. `T056-01` Documentar `StoreReview`, `StoreReviewReply` y `StoreReviewEndpoints`
2. `T056-02` Publicar `contracts/openapi.yaml`
3. `T056-03` Trazabilidad `FR-056-*` en handlers/entidades y validar `AverageRating` real
4. `T056-04` Pruebas de integración de endpoints y MongoDB real para `store_reviews`
5. `T056-05` Paginación en BD `PagedResult` en `GET /api/v1/merchants/{merchantId}/reviews` (`FR-056-07`) y agregación MongoDB `$match+$group`
