# Plan 038 — Favoritos de ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 2.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Permitir que un usuario autenticado marque o desmarque (*toggle*) una oferta visible como favorita mediante `POST /api/v1/users/favorites/{id}`, garantizando unicidad con un índice compuesto, calculando el conteo desde la colección y alimentando la afinidad por categoría que consume el ranking de la spec 042.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `OfferFavorite` con invariantes y contrato `IOfferFavoriteRepository`. No se añade `Offer.FavoriteCount` (no existe en el dominio). |
| Application | `ToggleOfferFavoriteCommand`, DTO `FavoriteToggleResultDto` y handler que valida la visibilidad de la oferta (404 si archivada), distingue 401 de 403 para invitados e invalida `OfferCacheKeys.Detail(offerId)` y `FeedCacheKeys.Affinity(userId)` |
| Infrastructure | `OfferFavoriteDocument`, `MongoOfferFavoriteRepository`, índices en `MongoDbContext` (`userId`/`offerId` único, `offerId`, `userId` + `createdAtUtc`) y `MongoUserAffinityReader` con señal de favoritos (peso 2.0) |
| Api | Endpoint `POST /api/v1/users/favorites/{id:guid}` en `UserEndpoints.cs` con `FavoritesPolicy` (120/hora) y `RegisteredUserPolicy` |

## 3. Modelo de datos e índices

- Colección: `offer_favorites`.
- Índices:
  - `ux_offer_favorites_user_offer`: `{ userId: 1, offerId: 1 }` (único, garantiza un solo favorito por usuario y oferta ante concurrencia)
  - `ix_offer_favorites_offer`: `{ offerId: 1 }` (conteo rápido por oferta)
  - `ix_offer_favorites_user`: `{ userId: 1, createdAtUtc: -1 }` (lectura de afinidad del usuario)

## 4. Modelo de amenazas (STRIDE) y Seguridad

| Amenaza (STRIDE) | Mitigación implementada |
|---|---|
| **S**poofing | El `userId` se obtiene estrictamente de los claims del JWT verificado en el servidor, ignorando cualquier identificador enviado por el cliente. |
| **T**ampering | Alternancia protegida por índice único y captura de `MongoWriteException`/DuplicateKey. El conteo se calcula con `CountDocumentsAsync`, sin contadores desnormalizados que puedan quedar negativos. |
| **R**epudiation | Registro de marca temporal `createdAtUtc` inmutable al agregar un favorito. |
| **I**nformation Disclosure | No se expone la lista de otros usuarios que marcaron la oferta como favorita. Solo se devuelve el estado del usuario solicitante y el total acumulado. |
| **D**enial of Service | Política `FavoritesPolicy` (120 alternancias por usuario y hora, configurada en `RateLimiting:Favorites`), limitando el abuso sin afectar al `SensitiveWritePolicy` global. |
| **E**levation of Privilege | `RegisteredUserPolicy` rechaza a invitados con 403 `Favorite.GuestForbidden`; sin token se devuelve 401 `Favorite.Unauthorized`. |

## 5. Observabilidad y rendimiento

- Operación de alternancia con latencia objetivo p95 < 300 ms (`NFR-038-01`).
- Invalidación de caché en Redis para el detalle de la oferta (`OfferCacheKeys.Detail(offerId)`) y la afinidad del usuario (`FeedCacheKeys.Affinity(userId)`).
- Logs estructurados con `correlation-id` y sin PII.

## 6. Costo AWS

Sin costo incremental: Lambda .NET 10 y API Gateway dentro del Free Tier. Operaciones en MongoDB Atlas M0 sin costo adicional.

## 7. Estrategia de pruebas

- **Unitarias (`Sensus.Application.UnitTests`)**:
  - Invariantes de `OfferFavorite` (GUIDs no vacíos, fechas válidas).
  - `ToggleOfferFavoriteCommandHandler`: alternancia en ambos sentidos, conteo devuelto, invalidación de ambas claves de caché, 404 ante oferta no visible, 401 sin usuario y 403 para invitado (`ToggleOfferFavoriteCommandHandlerTests.cs`, 5 pruebas).
- **Integración (`Sensus.IntegrationTests`)**:
  - Endpoint `POST /api/v1/users/favorites/{id}`: 200 con `{ favorited: true, favoriteCount }`, 200 con `{ favorited: false, favoriteCount }`, 404 para oferta archivada, 401 sin auth, 403 para invitado (`FavoriteEndpointsTests.cs`, 5 pruebas).
  - Afinidad: un favorito contribuye a la categoría en `MongoUserAffinityReader` (`MongoOfferFeedIntegrationTests.cs`).
  - Contrato OpenAPI verificado en `OpenApiContractTests.cs` (ruta `/api/v1/users/favorites/{id}`).
  - Persistencia real con Testcontainers cuando el daemon de Docker está disponible.

## 8. Constitution Check

Evaluación alineada a `constitution.md` v2.4.0.

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | `FR-038-01` a `FR-038-06` con criterios de aceptación medibles y tareas atómicas. |
| II. Clean Architecture | PASS | Entidades puras en Domain; caso de uso CQRS en Application; persistencia en Infrastructure; endpoint en Api. |
| III. CQRS y contratos | PASS | Comando `ToggleOfferFavoriteCommand`, DTO de respuesta y OpenAPI 3.1. |
| IV. Seguridad y privacidad | PASS | Usuario tomado de JWT; 401 vs 403 diferenciados; sin fuga de identidad de otros usuarios; rate limiting de 120/hora. |
| V. Verificación automatizada | PASS | Pruebas unitarias y de integración cubriendo alternancia, invitados, 404, caché y afinidad. |
| VI. Costo cero e IaC | PASS | Servicios dentro del Free Tier sin infraestructura de pago. |
| VII. Rendimiento y observabilidad | PASS | Índice único compuesto e índice por usuario; invalidación de caché; sin escaneos de colección. |
| VIII. Nivel fintech | PASS | ASVS nivel 2/3 en autorización; STRIDE documentado; validación de borde estricta y errores uniformes. |

## 9. Orden de implementación

1. `T038-01` Actualizar y validar el contrato OpenAPI del spec
2. `T038-02` Implementar: entidad `OfferFavorite` y contrato `IOfferFavoriteRepository` en Domain
3. `T038-03` Implementar: `ToggleOfferFavoriteCommand`, DTO y handler en Application
4. `T038-04` Implementar: `OfferFavoriteDocument`, índices y `MongoOfferFavoriteRepository` en Infrastructure
5. `T038-05` Implementar: endpoint `POST /api/v1/users/favorites/{id}` y política `FavoritesPolicy` en Api
6. `T038-06` Implementar: pruebas unitarias en Application.UnitTests
7. `T038-07` Implementar: pruebas de integración en IntegrationTests
8. `T038-08` Integrar la señal de favoritos en `MongoUserAffinityReader` (spec 042)
9. `T038-09` Actualizar `checklist.md`, `specs/README.md`, `requirements.md` y `memory.md`
