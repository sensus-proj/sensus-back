# Checklist 042 — Feed de promociones por intereses

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Alcance confirmado por el responsable: feed de promociones separado del social, sin invitados, ruta `/api/v1/feed/offers`
- [x] Requisitos y criterios de aceptación medibles
- [x] Modelo de amenazas STRIDE en el plan
- [x] Spec 054 (intereses) implementada o con su puerto disponible
- [x] Spec 055 (interacciones) con su puerto disponible
- [ ] Aviso de privacidad actualizado con la personalización del feed (landing y FT-23 — FT-23 se implementa en móvil Paso 8)

## B. Específicos de este spec

- [x] Puntaje con pesos configurables y versionados (T042-02, T042-10)
- [x] Diversidad y exploración deterministas (T042-02)
- [x] Paginación estable con instantánea en Redis (T042-05)
- [x] Invitado recibe `403` (T042-06)
- [x] Impresiones emitidas con `rankingVersion` (T042-07)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [x] Pruebas unitarias e integración en verde; cobertura ≥ 80 % en Domain y Application
- [x] Test de arquitectura en verde
- [x] Logs con `correlation-id` y sin PII
- [ ] NFR-042-01 a NFR-042-07 verificados (NFR-042-03..07 verificados con fixture §7.1; NFR-042-01/02 de latencia p95 quedan en brecha `G-13`)
- [x] Constitution Check en PASS tras la implementación
- [x] `memory.md` actualizado

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Unitarias | `tests/Sensus.Application.UnitTests/Offers/Feed/OfferFeedRankerTests.cs` | ☑ |
| Integración y Fixture §7.1 | `tests/Sensus.IntegrationTests/Fixtures/OfferFeed/OfferFeedDeterministicFixture.cs`, `Offers/OfferFeedEndpointsTests.cs`, `Persistence/MongoOfferFeedIntegrationTests.cs` | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-20 | Rodrigo Sánchez (con asistente) | Spec reescrita a v2.0.0 | De «Recomendaciones» (20 ofertas) a feed de promociones paginado por intereses; alineada a v2.4.0 |
| 2026-11-20 | Ingeniería Sensus (con asistente) | Implementado (`T042-01`..`T042-11`) | `OfferFeedRanker` + `FeedRankingWeights` (v `feed-v1`, pesos `0.40/0.25/0.20/0.10/0.05` en `FeedRankingSettings`), `GetOfferFeedQuery` (`ISensitiveRequest`), `MongoOfferRepository.GetFeedCandidatesAsync`/`GetFeedCandidatesByIdsAsync`, `MongoUserAffinityReader` (caché 10 min), `RedisFeedSnapshotStore` (TTL 10 min con degradación a `snapshotId = null`), `StructuredFeedMetricsPublisher`, `GET /api/v1/feed/offers` (`RegisteredUser`, `read-heavy`, `429`), emisión de impresiones a 055 y *fixture* §7.1 (300 visibles, 30 excluidas). Probado en local contra MongoDB 7.0 y Redis 7 reales (`MongoOfferFeedIntegrationTests`) y suite completa (`1,252/1,252`; cobertura `91.00 %` Domain / `96.46 %` Application). Pendientes externos: carga p95 (`G-13`) y aviso FT-23/landing. |
