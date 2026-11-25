# Tareas 030 — Eliminación de cuenta y datos

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Escindida |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T030-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-030-01` | — | Escindida → 060/061 (contrato existente verificado, sin cambios; `204` conservado) |
| T030-02 | Implementar: Estado Deleted y reglas de retención | Domain | `FR-030-01` | T030-01 | Escindida → 060 (T060-01 a T060-04) |
| T030-03 | Implementar: DeleteAccountCommand y caso de uso de purga | Application | `FR-030-01` | T030-02 | Escindida → 060/061 (baja y cierre de contenido) |
| T030-04 | Implementar: Purga programada y anonimización en MongoDB | Infrastructure | `FR-030-01` | T030-03 | Escindida → 060 (purga) y 061 (anonimización) |
| T030-05 | Implementar: Endpoint DELETE /api/v1/users/account | Api | `FR-030-01` | T030-04 | Escindida → 060 (204 verificado en integración y E2E) |
| T030-06 | Modelar el estado Deleted y la retención | Domain | `FR-030-02` | T030-05 | Escindida → 060 (T060-01) |
| T030-07 | Implementar la purga programada con evidencia | Infrastructure | `FR-030-04` | T030-06 | Escindida → 060 (T060-09 a T060-12) |
| T030-08 | Anonimizar contenido público en lugar de borrarlo | Application | `FR-030-05` | T030-07 | Escindida → 061 (T061-01, T061-02, T061-08) |
| T030-09 | Resolver apartados activos antes de desactivar ofertas | Application | `FR-030-06` | T030-08 | Escindida → 061 (T061-04, T061-05, T061-08) |
| T030-10 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-030-01` | T030-09 | Escindida → 060/061 (10 pruebas del filtro de baja/login, 3 E2E) |
| T030-11 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-030-01` | T030-10 | Completada (cierre documental 030/060/061) |
| T030-12 | Registrar evidencias del Constitution Check | Docs | `FR-030-01` | T030-11 | Completada (checklists 060 y 061 con gates reales) |
| T030-13 | Borrar `user_interests` y `offer_interactions` al eliminar la cuenta, con prueba de regresión (CA-030-06) | Infrastructure | `FR-030-08` | T054-04, T055-03 | Completada (2026-11-20) |

## Notas de ejecución

- El ID 030 es estable: no se reutiliza ni se renumera. El trabajo restante se ejecutó bajo las specs 060 y 061.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V); la evidencia vive en los checklists de 060 y 061.
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- La colección `sanctions` no se modifica: la copia anonimizada convive con ella y expira a 90 días (Spec 061).
