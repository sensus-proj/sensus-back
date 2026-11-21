# Tareas 036 — Ciclo de vida de la oferta

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
| T036-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-036-01` | — | Completada (`contracts/openapi.yaml`) |
| T036-02 | Implementar: Máquina de estados de Offer y transiciones válidas | Domain | `FR-036-01` | T036-01 | Completada |
| T036-03 | Implementar: ChangeOfferStatusCommand y validador | Application | `FR-036-01` | T036-02 | Completada |
| T036-04 | Implementar: Actualización atómica y registro de auditoría | Infrastructure | `FR-036-01` | T036-03 | Completada (`MongoOfferStatusHistoryRepository`) |
| T036-05 | Implementar: Endpoint PUT /api/v1/offers/{id}/status | Api | `FR-036-01` | T036-04 | Completada |
| T036-06 | Implementar la máquina de estados | Domain | `FR-036-01` | T036-05 | Completada |
| T036-07 | Validar apartados activos antes de archivar | Application | `FR-036-04` | T036-06 | Completada |
| T036-08 | Persistir el historial de estados | Infrastructure | `FR-036-06` | T036-07 | Completada (`OfferStatusTransition`, `offer_status_history`) |
| T036-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-036-01` | T036-08 | Completada (`OfferLifecycleEndpointsTests`, `OfferLifecycleHistoryTests`) |
| T036-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-036-01` | T036-09 | Completada |
| T036-11 | Registrar evidencias del Constitution Check | Docs | `FR-036-01` | T036-10 | Completada |
| T036-12 | Documentar `republish` y `expire-check` (retro-spec) y sus contratos | Docs | `FR-036-07` | — | Hecho |
| T036-13 | Proteger `expire-check` con `AdminOnly` (o invocación interna) y límite de tasa; prueba de `401`/`403` | Api | `FR-036-09` | T036-12 | Hecho (`ExpireCheckAuthorizationTests`) |
| T036-14 | Límite de tasa `sensitive-write` en `republish` | Api | `FR-036-10` | T036-12 | Completada (`SensitiveWritePolicy` en `/republish`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
