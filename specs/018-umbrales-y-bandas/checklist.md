# Checklist 018 — Umbrales y bandas de decisión

Alineado a `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 018 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 017
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Los límites de banda están probados en sus valores exactos (baja < 0.35, intermedia 0.35-0.75, alta >= 0.75)
- [x] El umbral aplicado queda registrado en la decisión y configuración externa

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde (226 pruebas totales en verde)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica
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

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Media/Commands/ModerateImageCommandHandlerTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Services/OpenAiImageModerationServiceTests.cs` | ☑ |
| Pruebas de endpoint | `tests/Sensus.IntegrationTests/Endpoints/MediaEndpointsTests.cs` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-12 | Antigravity AI | PASS | Implementación de evaluación multimodal y bandas de decisión con category_scores externos. 226 pruebas superadas. |
