# Tareas 041 — Búsqueda de ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado (`NFR-041-01` en brecha `G-13`) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T041-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-041-01` | — | Completada |
| T041-02 | Implementar: `SearchOffersQuery`, cálculo de puntaje y DTO (`OfferSearchItemDto`) | Application | `FR-041-01` | T041-01 | Completada |
| T041-03 | Implementar: Índice de texto en español (`txt_offers_spanish`), índice de prefijo (`ix_offers_title_normalized`) y consulta híbrida geoespacial | Infrastructure | `FR-041-01` | T041-02 | Completada |
| T041-04 | Implementar: Endpoint `GET /api/v1/offers/search` antes de `/{id:guid}` | Api | `FR-041-01` | T041-03 | Completada |
| T041-05 | Definir la función pura `OfferSearchRanker`, `OfferTextNormalizer` y `OfferSearchWeights` | Domain | `FR-041-02` | T041-04 | Completada |
| T041-06 | Crear `txt_offers_spanish` (`default_language: "spanish"`) e `ix_offers_title_normalized` y verificar su uso en MongoDB real | Infrastructure | `FR-041-01` | T041-05 | Completada |
| T041-07 | Implementar el fallback determinista a ofertas cercanas cuando `q` esté vacía | Application | `FR-041-05` | T041-06 | Completada |
| T041-08 | Implementar pruebas unitarias, HTTP (`400`/`401`/`200`/`429`) e integración contra MongoDB real | Pruebas | `FR-041-01` | T041-07 | Completada |
| T041-09 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-041-01` | T041-08 | Completada |
| T041-10 | Registrar evidencias del Constitution Check | Docs | `FR-041-01` | T041-09 | Completada |
| T041-11 | Validador con lista de permitidos, límite de tasa `read-heavy` y errores uniformes, con pruebas de `400` y `401` | Application y Api | `FR-041-08`, `FR-041-09`, `FR-041-10` | T041-10 | Completada |
| T041-12 | Acceso de invitado con radio recortado a 2 km y prueba de integración | Application | `FR-041-11` | T041-11 | Completada |

## Notas de ejecución

- Diseño híbrido aprobado (2026-11-20): stemming nativo de MongoDB (`default_language: "spanish"`) para palabras completas + coincidencia por prefijo sobre `titleNormalized` (minúsculas sin acentos) con índice B-tree `ix_offers_title_normalized` para consultas de >= 2 caracteres.
- Latencia `NFR-041-01` (p95 < 600 ms) queda registrada en brecha `G-13` (Apéndice F de `constitution.md` v2.4.0).
