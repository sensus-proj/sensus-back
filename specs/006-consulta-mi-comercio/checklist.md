# Checklist 006 — Consulta de mi comercio

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 006 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 005
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El userId proviene del token (`GetMyComercioQueryHandler.cs`)
- [x] La ruta legacy queda retirada (`SecurityPipelineTests`, sin rutas `/comercio/` en `src/Sensus.Api`)
- [x] La respuesta no expone datos de otros comercios (`GetMyComercioQueryHandlerTests`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`Sensus.ArchitectureTests` 6/6)
- [x] Pruebas unitarias y de integración añadidas y en verde (`GetMyComercioQueryHandlerTests`, `MerchantProfileEndpointsTests` 200/404)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS (`MerchantProfileEndpointsTests`, `SecurityPipelineTests`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (STRIDE en `plan.md` §4 y escaneos CI bloqueantes verdes `ci.yml`/`dast-zap.yml`, `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Comercios/Queries/GetMyComercioQueryHandlerTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Merchants/MerchantProfileEndpointsTests.cs`, `tests/Sensus.IntegrationTests/Security/SecurityPipelineTests.cs` | ☑ |
| Contrato OpenAPI | `specs/006-consulta-mi-comercio/contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Antigravity (Bloque B1) | Completada | Verificado `GET /api/v1/merchants/me` 200/404 (`MerchantProfileEndpointsTests`), retiro `/api/v1/comercio/mi-comercio` 404 (`SecurityPipelineTests`), STRIDE en `plan.md` y CI `4a00d24`. |
