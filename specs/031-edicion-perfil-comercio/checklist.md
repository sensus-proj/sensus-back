# Checklist 031 — Edición del perfil de comercio

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 031 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 005, 006
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El comercio se identifica por el token (`UpdateMerchantProfileCommandHandler.cs:43-50`)
- [x] La ubicación se recalcula en el servidor (`ComercioCoordinateHelper`, `UpdateMerchantProfileCommandHandlerTests`)
- [x] Las ofertas conservan su identificador (`UpdateMerchantLocationAsync` solo actualiza coordenadas)
- [x] El cambio queda auditado (`IModerationAuditRecorder` en `UpdateMerchantProfileCommandHandler.cs:69-90`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`Sensus.ArchitectureTests` 6/6)
- [x] Pruebas unitarias y de integración añadidas y en verde (`UpdateMerchantProfile*`, `MerchantProfileEndpointsTests`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada (`MerchantOnly`)
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS (`UpdateMerchantProfile*` + `MerchantProfileEndpointsTests`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (STRIDE en `plan.md` §4, auditoría append-only y escaneos CI bloqueantes verdes `ci.yml`/`dast-zap.yml`, `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Comercios/Commands/UpdateMerchantProfile*Tests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Merchants/MerchantProfileEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | `specs/031-edicion-perfil-comercio/contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Antigravity (Bloque B1) | Completada | Cerradas `T031-01`..`T031-11`: `MerchantProfileEndpointsTests` ampliado con categoría inválida y coordenadas fuera de rango (400), 403 no comerciante, auditoría `IModerationAuditRecorder` verificada en handler, STRIDE añadido a `plan.md` y CI `4a00d24`. |
