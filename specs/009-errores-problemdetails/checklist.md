# Checklist 009 — Errores uniformes con ProblemDetails

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 009 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: Ninguna
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Ningún error devuelve stack trace
- [x] Todos los errores incluyen correlationId
- [x] El catálogo de códigos está documentado

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (o no aplica por ser middleware transversal)
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica, cero costo)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y resiliencia** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ Aprobado y completo |
| Plan | [`plan.md`](plan.md) | ☑ Constitution Check PASS |
| Tareas | [`tasks.md`](tasks.md) | ☑ Completadas (T009-01..08) |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ Verdes |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Middlewares/ProblemDetailsUniformityTests.cs` | ☑ 7 pruebas verdes (400, 401, 403, 404, 409, 429, 500) |
| Pruebas de arquitectura | `tests/Sensus.ArchitectureTests/ArchitectureTests.cs` | ☑ Endpoints_ShouldNotInstantiateProblemDetailsDirectly verde |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-23 | Antigravity | PASS | Implementación completa de RFC 7807 uniforme, correlationId en todos los códigos, y catálogo de códigos documentado |
