# Plan 057 — Me gusta en ofertas (`OfferLike`)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec alineada a implementación |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Se agrega la entidad `OfferLike` y su puerto `IOfferLikeRepository` en `Sensus.Domain`, el comando CQRS `ToggleOfferLikeCommand` y enriquecimiento batch de `LikesCount`/`IsLikedByMe` en `GetOfferDetailQuery`, `GetNearbyOffersQuery` y `GetMerchantOffersQueryHandler` en `Sensus.Application` (sin tocar `OfferCategory` ni creación/edición), el repositorio `MongoOfferLikeRepository` sobre `offer_likes` en `Sensus.Infrastructure` y la ruta `POST /api/v1/offers/{id:guid}/like` en `OfferEndpoints.cs`.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `OfferLike`, interfaz `IOfferLikeRepository` |
| Application | `ToggleOfferLikeCommand`, `OfferLikeStatusDto`, campos `LikesCount`/`IsLikedByMe` en `OfferDto`, `OfferDetailDto`, `NearbyOfferDto` y consultas de lectura con consulta batch |
| Infrastructure | `OfferLikeDocument`, `MongoOfferLikeRepository`, índices en `MongoDbContext`, registro DI |
| Api | `POST /api/v1/offers/{id:guid}/like` en `OfferEndpoints.cs` |
| Tests | `SocialHandlersTests`, `SocialEndpointsIntegrationTests`, `MongoSocialRepositoriesTests`, `CustomWebApplicationFactory` |

## 3. Modelo de datos e índices

- Colección MongoDB `offer_likes` (`OfferLikeDocument`): `Id` (UUID), `UserId` (UUID), `OfferId` (UUID), `CreatedAtUtc`.
- Índices en `MongoDbContext`:
  1. Único compuesto `(UserId asc, OfferId asc)` para garantizar máximo un like por usuario/oferta.
  2. Compuesto `(OfferId asc, CreatedAtUtc desc)` para conteo y lectura temporal.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Inflado de likes con cuentas invitadas o múltiples likes del mismo usuario | Elevación de privilegios / Manipulación | `RegisteredUserPolicy` + índice único compuesto `(UserId, OfferId)` | Bajo |
| Peticiones automatizadas de toggle en bucle (DoS) | Denegación de servicio | Rate limiting `SensitiveWritePolicy` | Bajo |
| Exposición de identidades que dieron like | Divulgación de información | Solo conteo entero `LikesCount` y booleano propio `IsLikedByMe` | Bajo |

## 5. Observabilidad y rendimiento

- Consultas de lectura usan agregación batch `CountByOfferIdsAsync` y `GetLikedOfferIdsByUserAsync` (evita problema N+1 en `/nearby` y `/my-offers`).
- Invalidación selectiva en Redis/memoria para `OfferCacheKeys.Detail(offer.Id)`.

## 6. Costo AWS

- 0.00 USD incrementales sobre Lambda y MongoDB Atlas.

## 7. Estrategia de pruebas

- Unitarias (`Sensus.Application.UnitTests`): toggle on/off, rechazo oferta archivada/inexistente, conteos batch en consultas.
- Integración (`Sensus.IntegrationTests`): endpoint `POST /api/v1/offers/{id}/like` (200/401/403/404) y persistencia/unicidad real en MongoDB.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec `057-me-gusta-ofertas` con separación clara de Spec 039 |
| II. Clean Architecture | PASS | Sin acoplamiento inverso entre capas |
| III. Calidad y pruebas | PASS | MediatR + `Result<T>` + `OfferLikeStatusDto` |
| IV. Seguridad y moderación | PASS | `RegisteredUserPolicy` y `SensitiveWritePolicy` |
| V. Cobertura y pruebas reales | PASS | Unitarias + integración endpoint + `MongoSocialRepositoriesTests` |
| VI. Costo AWS | PASS | 0 USD incrementales |
| VII. Paginación e índices | PASS | Endpoint puntual + consulta batch con índice único y compuesto |
| VIII. DevSecOps / STRIDE | PASS | STRIDE evaluado; sin cambios en `OfferCategory` |

## 9. Riesgos de implementación

- Respuestas cacheadas en Redis para `GetOfferDetailQuery` pueden reflejar brevemente `IsLikedByMe` previo si no se invalida por usuario, mitigado invalidando `OfferCacheKeys.Detail(offer.Id)` en cada toggle.

## 10. Orden de implementación

1. `T057-01` Documentar `OfferLike`, `ToggleOfferLikeCommand` y enriquecimiento de DTOs
2. `T057-02` Publicar `contracts/openapi.yaml`
3. `T057-03` Referencias `FR-057-*` en código y verificación de nulos en repositorios
4. `T057-04` Pruebas de integración en `SocialEndpointsIntegrationTests` y `MongoSocialRepositoriesTests`
