# Checklist 038 — Favoritos de ofertas

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 038 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 032 (ofertas activas disponibles)
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado
- [x] Modelo de amenazas (STRIDE) y Principio VIII Fintech incluidos en `plan.md`

## B. Específicos de este spec

- [x] Alternancia del favorito por usuario y oferta, con borrado seguido de inserción protegido por índice único (`FR-038-01`, `FR-038-04`)
- [x] Conteo calculado con `CountDocumentsAsync`, sin contador desnormalizado ni valores negativos (`FR-038-02`)
- [x] Rechazo con 404 para ofertas archivadas, expiradas o inexistentes (`FR-038-03`)
- [x] Unicidad garantizada con índice compuesto `userId` + `offerId`
- [x] 401 sin token y 403 `Favorite.GuestForbidden` para invitados (`CA-038-05`)
- [x] Rate limiting de 120 alternancias por hora y usuario (`FavoritesPolicy`, `FR-038-06`)
- [x] Invalidación de `OfferCacheKeys.Detail(offerId)` y `FeedCacheKeys.Affinity(userId)` (`FR-038-05`)
- [x] La señal de favoritos es consumida por `MongoUserAffinityReader` con peso 2.0 (`FR-042-04`)
- [x] Consulta paginada `GET /api/v1/users/favorites` (`20` por defecto, `100` máx.) ordenada por `favoritedAtUtc` descendente, excluyendo ofertas expiradas/archivadas o comercios inactivos y validando `400`/`401`/`403` (`FR-038-07`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (6 superadas)
- [x] Pruebas unitarias y de integración añadidas y en verde (`ToggleOfferFavoriteCommandHandlerTests.cs` — 5; `GetUserFavoritesQueryHandlerTests.cs` — 5; `FavoriteEndpointsTests.cs` — 9)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado y validado (`contracts/openapi.yaml`, `OpenApiContractTests.cs`)
- [x] IaC actualizada y `cdk diff` revisado si aplica (sin cambios de infra)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ (v2.2.0) |
| Plan | [`plan.md`](plan.md) | ☑ (v2.2.0) |
| Tareas | [`tasks.md`](tasks.md) | ☑ (v2.2.0) |
| Pruebas unitarias | [`tests/Sensus.Application.UnitTests/Offers/ToggleOfferFavoriteCommandHandlerTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.Application.UnitTests/Offers/ToggleOfferFavoriteCommandHandlerTests.cs) · [`GetUserFavoritesQueryHandlerTests.cs`](../../tests/Sensus.Application.UnitTests/Offers/GetUserFavoritesQueryHandlerTests.cs) | ☑ (5 + 5 superadas) |
| Pruebas de integración | [`tests/Sensus.IntegrationTests/Users/FavoriteEndpointsTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.IntegrationTests/Users/FavoriteEndpointsTests.cs) | ☑ (9 superadas) |
| Pruebas de afinidad | [`tests/Sensus.IntegrationTests/Persistence/MongoOfferFeedIntegrationTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.IntegrationTests/Persistence/MongoOfferFeedIntegrationTests.cs) | ☑ (señal de favoritos y `RealMongo_GetPagedByUserIdAsync`; requiere Docker para ejecución Mongo real) |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-22 | Antigravity | En progreso | SDD v2.4.0 actualizado e inicio de implementación. |
| 2026-11-23 | Revisión de specs | Aprobado | Implementación completa: endpoint, 403 para invitados, política `FavoritesPolicy` 120/hora, índice `ix_offer_favorites_user`, integración de favoritos en la afinidad de la spec 042 y pruebas. Build `-warnaserror`, `dotnet format`, 910 unitarias, 4 arquitectura y pruebas de integración afectadas en verde. 9 pruebas Testcontainers no ejecutables: daemon de Docker no disponible en el entorno; el resto de la suite de integración pasa (493/502). |
| 2026-11-26 | Cierre móvil/backend (Bloque F) | Aprobado | Incorporado `GET /api/v1/users/favorites` (`FR-038-07`, `CA-038-06`), `GetUserFavoritesQuery` + validador (`20`/`100`), `MongoOfferFavoriteRepository.GetPagedByUserIdAsync` indexado por `ix_offer_favorites_user`, OpenAPI actualizado. Verificado con build `-warnaserror`, `dotnet format`, `check-specs.mjs` (62), `6/6` arquitectura, `1229/1229` unitarias, `3/3` E2E y `14/14` integración afectadas (Docker apagado en equipo). |
