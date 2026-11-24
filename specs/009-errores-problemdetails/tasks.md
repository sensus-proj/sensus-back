# Tareas 009 — Errores uniformes con ProblemDetails

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completada |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado | Evidencia |
|---|---|---|---|---|---|---|
| T009-01 | Implementar: GlobalExceptionHandler y middleware de correlación | Api | `FR-009-01` | — | Completada | `GlobalExceptionHandler.cs`, `CorrelationIdMiddleware.cs`, probado en `ProblemDetailsUniformityTests.Error500_UnhandledException_ReturnsUniformProblemDetailsWithoutStackTrace` |
| T009-02 | Implementar: Excepciones de dominio mapeadas a códigos | Application | `FR-009-01` | T009-01 | Completada | `DomainException.cs`, `ValidationException.cs`, `NotFoundException.cs`, mapeados en `GlobalExceptionHandler.cs` |
| T009-03 | Implementar: Sin dependencias de presentación | Domain | `FR-009-01` | T009-02 | Completada | `Sensus.Domain/Common/Error.cs` y `ErrorType.cs` puros, sin acoplamiento a ASP.NET ni librerías de presentación |
| T009-04 | Unificar el mapeo de Result y excepciones a ProblemDetails | Api | `FR-009-01` | T009-03 | Completada | `ResultExtensions.ToHttpResult()`, `ResultProblemDetailsHttpResult`, probado en `ArchitectureTests.Endpoints_ShouldNotInstantiateProblemDetailsDirectly` |
| T009-05 | Añadir correlationId a todas las respuestas de error | Api | `FR-009-02` | T009-04 | Completada | `ResultExtensions.cs:55`, `GlobalExceptionHandler.cs:74`, `SecurityExtensions.cs:68`, verificado en `ProblemDetailsUniformityTests` (400, 401, 403, 404, 409, 429, 500) |
| T009-06 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-009-01` | T009-05 | Completada | `tests/Sensus.IntegrationTests/Middlewares/ProblemDetailsUniformityTests.cs` (7/7 pruebas verdes) |
| T009-07 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-009-01` | T009-06 | Completada | `spec.md` con catálogo de códigos `Error.Code -> status`, `plan.md`, `checklist.md`, `specs/README.md` |
| T009-08 | Registrar evidencias del Constitution Check | Docs | `FR-009-01` | T009-07 | Completada | `checklist.md` y `plan.md` con Constitution Check PASS en los 8 principios |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
