# Tareas 043 — Alternativas a una oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado (`NFR-043-01` en brecha `G-13`) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T043-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-043-01` | — | Completada |
| T043-02 | Implementar: `GetOfferAlternativesQuery` y proyección (`OfferAlternativeItemDto`) | Application | `FR-043-01` | T043-01 | Completada |
| T043-03 | Implementar: Consulta por categoría + `2dsphere` + rango de precio final (`<= 120 %`) | Infrastructure | `FR-043-01` | T043-02 | Completada |
| T043-04 | Implementar: Endpoint `GET /api/v1/offers/{id:guid}/alternatives` | Api | `FR-043-01` | T043-03 | Completada |
| T043-05 | Implementar el filtro por categoría y precio final comparable (`OfferAlternativesPolicy`) | Domain y Application | `FR-043-04` | T043-04 | Completada |
| T043-06 | Crear el índice `ix_offers_category_status_price` y verificar su uso | Infrastructure | `FR-043-01` | T043-05 | Completada |
| T043-07 | Validar el límite `[1, 10]` (5 por defecto) y el `404` uniforme | Application y Api | `FR-043-07` | T043-06 | Completada |
| T043-08 | Implementar las pruebas unitarias, HTTP y contra MongoDB real | Pruebas | `FR-043-01` | T043-07 | Completada |
| T043-09 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-043-01` | T043-08 | Completada |
| T043-10 | Registrar evidencias del Constitution Check | Docs | `FR-043-01` | T043-09 | Completada |
| T043-11 | Validador con lista de permitidos, límite de tasa `read-heavy` y `404` uniforme, con pruebas de `400`, `401` y `404` | Application y Api | `FR-043-08`, `FR-043-09`, `FR-043-10` | T043-10 | Completada |
| T043-12 | Acceso de invitado con distancia recortada a 2 km y prueba de integración | Application | `FR-043-11` | T043-11 | Completada |

## Notas de ejecución

- Decisión aprobada (2026-11-20): precio comparable = `finalPrice` (`discountedPrice`) de la alternativa <= 120 % del `finalPrice` de la oferta original.
- Latencia `NFR-043-01` (p95 < 500 ms) queda registrada en brecha `G-13` (Apéndice F de `constitution.md` v2.4.0).
