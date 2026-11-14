# Tareas 015 — Métricas y alarmas

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
| T015-01 | Implementar: Definición de alarmas y panel en CDK | Infraestructura | `FR-015-01` | — | Hecho |
| T015-02 | Implementar: Emisión de métricas de aplicación | Api | `FR-015-01` | T015-01 | Hecho |
| T015-03 | Definir alarmas en CDK | Infraestructura | `FR-015-02` | T015-02 | Hecho |
| T015-04 | Emitir métricas de aplicación | Api | `FR-015-01` | T015-03 | Hecho |
| T015-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-015-01` | T015-04 | Hecho |
| T015-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-015-01` | T015-05 | Hecho |
| T015-07 | Registrar evidencias del Constitution Check | Docs | `FR-015-01` | T015-06 | Hecho |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
