# Tareas 011 — Contrato OpenAPI

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
| T011-01 | Implementar: Configuración de OpenAPI y anotaciones | Api | `FR-011-01` | — | Completada (`AddSwaggerGen` con esquema Bearer JWT en `/openapi/v1.json`) |
| T011-02 | Implementar: Paso de validación del documento | CI | `FR-011-01` | T011-01 | Completada (`OpenApiContractTests` + `check-specs.mjs`) |
| T011-03 | Unificar la generación de OpenAPI en una sola fuente | Api | `FR-011-01` | T011-02 | Completada (`/openapi/v1.json` como fuente única y Swagger UI apuntando a él) |
| T011-04 | Añadir validación del contrato al pipeline | CI | `FR-011-03` | T011-03 | Completada (`OpenApiContractTests`) |
| T011-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-011-01` | T011-04 | Completada (`OpenApiContractTests`) |
| T011-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-011-01` | T011-05 | Completada |
| T011-07 | Registrar evidencias del Constitution Check | Docs | `FR-011-01` | T011-06 | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
