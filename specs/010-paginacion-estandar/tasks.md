# Tareas 010 — Paginación estándar

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T010-01 | Implementar: Modelo PagedResult y validadores de paginación | Application | `FR-010-01` | — | Completada (`PagedResult<T>`, `PaginationPolicy`) |
| T010-02 | Implementar: Binding de parámetros y respuesta uniforme | Api | `FR-010-01` | T010-01 | Completada |
| T010-03 | Implementar y probar PagedResult | Application | `FR-010-01` | T010-02 | Completada (`PaginationPolicyTests`, `FeedCommentsTests`) |
| T010-04 | Aplicar el contrato en los endpoints existentes de colección | Api | `FR-010-04` | T010-03 | Completada (paginación aplicada en comentarios `025`, reseñas `T056-05` compatible con móvil, ofertas cercanas/búsqueda/feed; endpoints legacy de arreglo raíz documentados por compatibilidad móvil) |
| T010-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-010-01` | T010-04 | Completada (`FeedCommentEndpointsTests`, `OfferFeedEndpointsTests`, `OfferDiscoveryEndpointsTests`) |
| T010-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-010-01` | T010-05 | Completada |
| T010-07 | Registrar evidencias del Constitution Check | Docs | `FR-010-01` | T010-06 | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
