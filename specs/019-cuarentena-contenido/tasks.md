# Tareas 019 — Cuarentena de contenido

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T019-01 | Implementar: Estado del contenido y transiciones permitidas | Domain | `FR-019-01` | — | Hecho |
| T019-02 | Implementar: Caso de uso de cuarentena y liberación | Application | `FR-019-01` | T019-01 | Hecho |
| T019-03 | Implementar: Almacenamiento privado y purga programada | Infrastructure | `FR-019-01` | T019-02 | Hecho |
| T019-04 | Modelar estados y transiciones | Domain | `FR-019-01` | T019-03 | Hecho |
| T019-05 | Implementar purga por expiración | Infrastructure | `FR-019-03` | T019-04 | Hecho |
| T019-06 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-019-01` | T019-05 | Hecho |
| T019-07 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-019-01` | T019-06 | Hecho |
| T019-08 | Registrar evidencias del Constitution Check | Docs | `FR-019-01` | T019-07 | Hecho |
| T019-09 | Endpoint de liberación `POST /api/v1/media/{id}/release` (Administrador) | Api | `FR-019-04` | T019-02 | Hecho |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
