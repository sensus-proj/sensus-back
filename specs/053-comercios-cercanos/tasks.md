# Tareas 053 — Comercios cercanos

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Backend completado |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T053-01 | Documentar el comportamiento existente de `GET /merchants/nearby` | Docs | `FR-053-01` | — | Hecho |
| T053-02 | Contrato OpenAPI del endpoint | Docs | `FR-053-03` | T053-01 | Hecho |
| T053-03 | Validador de coordenadas (`lat` [-90, 90], `lng` [-180, 180]) | Application | `FR-053-04` | T053-02 | Verificada localmente |
| T053-04 | Paginación 20/100 con `PagedResult` y conteo | Application e Infrastructure | `FR-053-05` | T053-02 | Verificada localmente |
| T053-05 | Política de límite de tasa `read-heavy` | Api | `FR-053-06` | T053-02 | Verificada localmente |
| T053-06 | Pruebas de integración: éxito, `400`, `401`, paginación y `429` | Pruebas | `FR-053-04` | T053-03, T053-04, T053-05 | Verificada localmente |
| T053-07 | Parámetro `radiusKm` con `radioMetros` como alias temporal | Api | `FR-053-07` | T053-02 | Verificada localmente |
