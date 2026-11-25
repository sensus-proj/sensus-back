# Tareas 038 — Favoritos de ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completada |
| Versión | 2.1.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T038-01 | Actualizar y validar el contrato OpenAPI del spec (`POST /api/v1/users/favorites/{id}`, 200/401/403/404/429) | Api | `FR-038-01`, `FR-038-06` | — | Completada (`specs/038-favoritos/contracts/openapi.yaml`, `OpenApiContractTests.cs`) |
| T038-02 | Implementar: entidad `OfferFavorite` y contrato `IOfferFavoriteRepository` en Domain | Domain | `FR-038-01`, `FR-038-04` | T038-01 | Completada (`OfferFavorite.cs`, `IOfferFavoriteRepository.cs`) |
| T038-03 | Implementar: `ToggleOfferFavoriteCommand`, DTO y handler (401/403/404 e invalidación de caché) en Application | Application | `FR-038-01`, `FR-038-02`, `FR-038-03`, `FR-038-05` | T038-02 | Completada (`ToggleOfferFavoriteCommand.cs`, `FavoriteToggleResultDto`) |
| T038-04 | Implementar: `OfferFavoriteDocument`, índices y `MongoOfferFavoriteRepository` en Infrastructure | Infrastructure | `FR-038-02`, `FR-038-04` | T038-03 | Completada (`OfferFavoriteDocument.cs`, `MongoOfferFavoriteRepository.cs`, `MongoDbContext.cs` — `ix_offer_favorites_user`) |
| T038-05 | Implementar: endpoint `POST /api/v1/users/favorites/{id}` y política `FavoritesPolicy` (120/hora) en Api | Api | `FR-038-01`, `FR-038-06` | T038-04 | Completada (`UserEndpoints.cs`, `SecurityExtensions.cs`, `RateLimitingSettings.cs`, `appsettings.json`) |
| T038-06 | Implementar: pruebas unitarias en Application.UnitTests | Pruebas | `FR-038-01`, `FR-038-02`, `FR-038-03`, `FR-038-05` | T038-05 | Completada (`tests/Sensus.Application.UnitTests/Offers/ToggleOfferFavoriteCommandHandlerTests.cs` — 5 superadas) |
| T038-07 | Implementar: pruebas de integración en IntegrationTests | Pruebas | `FR-038-01`, `FR-038-03`, `FR-038-06` | T038-06 | Completada (`tests/Sensus.IntegrationTests/Users/FavoriteEndpointsTests.cs` — 5 superadas; `OpenApiContractTests.cs`) |
| T038-08 | Integrar la señal de favoritos en `MongoUserAffinityReader` (spec 042) | Infrastructure | `FR-038-05`, `FR-042-04` | T038-04 | Completada (`MongoUserAffinityReader.cs` — peso 2.0; `IUserAffinityReader.cs`) |
| T038-09 | Actualizar `checklist.md`, `specs/README.md`, `requirements.md` y `memory.md` | Docs | `FR-038-05` | T038-08 | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- Revisión 2026-11-23: la spec estaba marcada como «Pendiente» pese a existir código en cuatro capas; se implementó el endpoint, la política de tasa, la distinción 401/403 y la señal de afinidad consumida por la spec 042.
