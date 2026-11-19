# Tareas 052 — Sesión de invitado

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Parcial (retro-spec) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T052-01 | Documentar el comportamiento existente de `POST /auth/guest` | Docs | `FR-052-01` | — | Hecho |
| T052-02 | Contrato OpenAPI del endpoint | Docs | `FR-052-02` | T052-01 | Hecho |
| T052-03 | Mover la vigencia del token de invitado a configuración y alinearla al Principio VIII (≤ 15 min con renovación), con prueba | Infrastructure | `FR-052-06` | T052-02 | Pendiente (fecha objetivo 2026-12-18, junto con la brecha G-06) |

## Notas de ejecución

- El resto de los requisitos ya está implementado y cubierto por las pruebas listadas en la spec §11.
