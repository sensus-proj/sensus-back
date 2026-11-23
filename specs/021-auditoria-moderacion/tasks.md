# Tareas 021 — Auditoría de moderación

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completada |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T021-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-021-01` | — | Completada |
| T021-02 | Implementar: Modelo de registro de auditoría | Domain | `FR-021-01` | T021-01 | Completada |
| T021-03 | Implementar: Consulta paginada | Application | `FR-021-01` | T021-02 | Completada |
| T021-04 | Implementar: Colección append-only | Infrastructure | `FR-021-01` | T021-03 | Completada |
| T021-05 | Implementar: Endpoint administrativo | Api | `FR-021-01` | T021-04 | Completada |
| T021-06 | Crear colección append-only con índices | Infrastructure | `FR-021-01` | T021-05 | Completada |
| T021-07 | Restringir el endpoint al rol Admin | Api | `FR-021-03` | T021-06 | Completada |
| T021-08 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-021-01` | T021-07 | Completada |
| T021-09 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-021-01` | T021-08 | Completada |
| T021-10 | Registrar evidencias del Constitution Check | Docs | `FR-021-01` | T021-09 | Completada |

## Notas de ejecución

- Todas las tareas fueron completadas con pruebas automatizadas verdes y cobertura > 90%.
- Repositorio append-only implementado sin métodos ni endpoints de actualización o borrado manual.
- OpenAPI v1.1.0 especifica el endpoint `GET /api/v1/admin/moderation/audit`.
