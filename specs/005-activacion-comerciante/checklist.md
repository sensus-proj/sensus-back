# Checklist 005 — Activación del rol comercial

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 005 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 001, 002
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Se valida el catálogo de categorías (`RegisterComercioCommandValidator`)
- [x] Las coordenadas se validan en el servidor (`MerchantProfileEndpointsTests.ActivateMerchant_ShouldReturnBadRequest_WhenCoordinatesOutOfRange`)
- [x] El usuario recibe el rol Merchant (`RegisterComercioCommandHandler`, `ActivateMerchant_ShouldReturnCreated_WithClientCoordinates`)
- [x] El índice único impide duplicados (`MongoUserAndComercioRepositoryTests`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`Sensus.ArchitectureTests` 6/6)
- [x] Pruebas unitarias y de integración añadidas y en verde (`RegisterComercio*`, `MerchantProfileEndpointsTests`, `MongoUserAndComercioRepositoryTests`)
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
- [x] **V. Verificación automatizada** — PASS (`MerchantProfileEndpointsTests`, `MongoUserAndComercioRepositoryTests`, `SecurityPipelineTests`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS (índice `2dsphere` verificado con `explain` y `correlation-id`)
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (STRIDE en `plan.md` §4 y escaneos CI bloqueantes verdes `ci.yml`/`dast-zap.yml`, `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Comercios/` (`RegisterComercio*`) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Merchants/MerchantProfileEndpointsTests.cs`, `tests/Sensus.IntegrationTests/Persistence/MongoUserAndComercioRepositoryTests.cs` | ☑ |
| Contrato OpenAPI | `specs/005-activacion-comerciante/contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Antigravity (Bloque B1) | Completada | Cerrada evidencia de `T005-01`..`T005-08`: ruta legacy retirada verificada (404), conflicto `409 Conflict` probado y Testcontainers MongoDB verificando índices únicos + `2dsphere` con `explain` `GEO_NEAR_2DSPHERE`. |
