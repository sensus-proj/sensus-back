# Tareas 054 — Intereses del usuario

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
| T054-01 | Contrato OpenAPI de `GET` y `PUT /api/v1/users/me/interests` | Docs | `FR-054-01`, `FR-054-02` | — | Hecho |
| T054-02 | Entidad `UserInterests` con invariante (catálogo `OfferCategory`, sin duplicados, máximo 5) y pruebas unitarias | Domain | `FR-054-03`, `FR-054-04` | T054-01 | Hecho |
| T054-03 | `GetMyInterestsQuery`, `ReplaceMyInterestsCommand`, validador y DTO con prueba de mapeo | Application | `FR-054-01`, `FR-054-02`, `FR-054-08` | T054-02 | Hecho |
| T054-04 | `MongoUserInterestsRepository` con índice único por `userId` | Infrastructure | `FR-054-02` | T054-03 | Hecho |
| T054-05 | Purga de `user_interests` en la eliminación de cuenta con prueba de regresión | Infrastructure | `FR-054-06` | T054-04 | Hecho |
| T054-06 | Endpoints con `RegisteredUser`, `read-heavy`, `sensitive-write` y `ProblemDetails` | Api | `FR-054-05`, `FR-054-07` | T054-04 | Hecho |
| T054-07 | Integración con Testcontainers: éxito, vacía, `400`, `401`, `403`, idempotencia | Pruebas | `FR-054-03`, `FR-054-05` | T054-06 | Hecho |
