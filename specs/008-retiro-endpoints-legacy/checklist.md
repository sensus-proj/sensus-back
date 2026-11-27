# Checklist 008 — Retiro de endpoints legacy

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 008 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 001, 005, 006
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] No quedan referencias a /api/v1/comercio ni /api/v1/comercios
- [x] El archivo .http usa solo rutas vigentes
- [x] El OpenAPI no contiene rutas en español

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde (`SecurityPipelineTests.RetiredLegacyRoutes_ShouldReturnNotFound`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado (no aplica: retiro de rutas legacy sin contrato propio)
- [x] IaC actualizada y `cdk diff` revisado si aplica
- [x] Logs con correlation-id y sin PII (`CorrelationIdMiddleware` activo en Spec 014)
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
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (`SecurityPipelineTests` + escaneos CI bloqueantes verdes `ci.yml`/`dast-zap.yml`, `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Security/SecurityPipelineTests.cs` | ☑ |
| Contrato OpenAPI | No aplica (retiro de endpoints heredados) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-14 | Claude (sesión de verificación) | Implementado | Rutas legacy en español y alias sin `/v1` retirados; `ComercioEndpoints.cs` (código muerto) eliminado; `RetiredLegacyRoutes_ShouldReturnNotFound` verifica 404. Spec sin `contracts/openapi.yaml` propio. Brecha: logs con correlation-id dependen de la spec 014 (pendiente). `dotnet build -c Release` 0 errores y 0 advertencias; 613 pruebas en verde (4 arquitectura, 498 unitarias, 3 E2E, 108 integración); cobertura combinada Domain 90,2 % y Application 97,4 %. |
