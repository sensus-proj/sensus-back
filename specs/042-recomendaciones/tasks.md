# Tareas 042 — Feed de promociones por intereses

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Pendiente |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T042-01 | Contrato OpenAPI de `GET /api/v1/feed/offers` | Docs | `FR-042-01` | — | Hecho |
| T042-02 | `FeedRankingWeights` validado y `OfferFeedRanker` (puntaje, desempate, diversidad, exploración determinista) con pruebas unitarias | Domain | `FR-042-02`, `FR-042-06`, `FR-042-07`, `NFR-042-06` | T042-01 | Pendiente |
| T042-03 | `GetOfferFeedQuery`, validador con lista de permitidos y DTOs con prueba de mapeo | Application | `FR-042-01`, `FR-042-13` | T042-02 | Pendiente |
| T042-04 | Lectura de intereses (054) y agregación de afinidad por categoría desde vistas y apartados, con caché de 10 min | Infrastructure | `FR-042-03`, `FR-042-04`, `FR-042-12` | T042-03, 054 | Pendiente |
| T042-05 | `RedisFeedSnapshotStore` con TTL configurable y degradación sin Redis | Infrastructure | `FR-042-08` | T042-03 | Pendiente |
| T042-06 | Endpoint con `RegisteredUser`, `read-heavy` y `ProblemDetails`; fallback sin intereses | Api | `FR-042-05`, `FR-042-09`, `FR-042-10`, `FR-042-14` | T042-04, T042-05 | Pendiente |
| T042-07 | Emitir impresiones de cada página servida con `rankingVersion` (puerto de la spec 055) | Application | `FR-042-11` | T042-06, 055 | Pendiente |
| T042-08 | Integración con Testcontainers: éxito, `400`, `401`, `403`, paginación estable, instantánea vencida, exclusiones y degradación | Pruebas | `FR-042-05`, `FR-042-08`, `FR-042-10` | T042-06 | Pendiente |
| T042-09 | Métricas EMF de latencia, caché y relajación de reglas; verificación de NFR-042-01 a 05 | Observabilidad | `NFR-042-01`, `NFR-042-02`, `NFR-042-04`, `NFR-042-05` | T042-07 | Pendiente |
| T042-10 | Documentar los pesos iniciales y su versión en configuración por entorno | Docs | `NFR-042-07` | T042-02 | Pendiente |
| T042-11 | *Fixture* determinista de relevancia y diversidad (plan §7.1) con sus aserciones | Pruebas | `NFR-042-03`, `NFR-042-04`, `NFR-042-05`, `NFR-042-06` | T042-06 | Pendiente |
