# Tareas 037 — Inventario de ofertas del comerciante

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T037-01 | Contrato OpenAPI v2.0.0 con parámetros de paginación, esquema `MerchantOfferDto` y errores 400/401/403 | Api | `FR-037-01` | — | Hecho |
| T037-02 | `GetMerchantOffersQuery` paginada, validador de borde y retorno `PagedResult<MerchantOfferDto>` | Application | `FR-037-01`, `FR-037-04` | T037-01 | Hecho |
| T037-03 | Conteo batch de apartados y canjes con `Filter.In` y proyección mínima | Infrastructure | `FR-037-03`, `NFR-037-02` | T037-02 | Hecho |
| T037-04 | Endpoint `GET /api/v1/merchants/my-offers` con paginación estándar y `ProblemDetails` | Api | `FR-037-02`, `FR-037-04` | T037-03 | Hecho |
| T037-05 | DTO de inventario con `ActiveClaimsCount`, `RedemptionsCount` y `ViewsCount`; mapeo explícito | Application | `FR-037-03` | T037-04 | Hecho |
| T037-06 | Índices `ix_offers_merchant_created`, `ix_offers_merchant_status_created` e `ix_offer_claims_offer_status` | Infrastructure | `FR-037-02`, `FR-037-05` | T037-05 | Hecho |
| T037-07 | Aislamiento entre comercios verificado en unitarias e integración | Api | `FR-037-06` | T037-06 | Hecho |
| T037-08 | Pruebas de integración: métricas, filtro, vacío, 400, 401, 403, aislamiento y latencia | Pruebas | `FR-037-01` | T037-07 | Hecho |
| T037-09 | Actualización de `specs/README.md`, `specs/requirements.md`, `checklist.md` y `memory.md` | Docs | `FR-037-01` | T037-08 | Hecho |
| T037-10 | Evidencias del Constitution Check y cierre de la pregunta de métricas | Docs | `FR-037-01` | T037-09 | Hecho |

## Notas de ejecución

- Cada tarea se cerró actualizando `checklist.md` y `plan.md` cuando cambió el diseño.
- Las métricas son en tiempo real: vistas desde `Offer.ViewCount` y apartados desde un conteo batch por página.
- Los totales se obtienen en la misma consulta paginada; no hay segundo recuento independiente.
- Los requisitos remiten a `spec.md` §4 y §5.
