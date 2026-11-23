# Checklist 035 — Edición de oferta

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 035 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 034, 033
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El propietario se valida en el servidor
- [x] La versión evita sobrescrituras
- [x] El contenido cambiado vuelve a moderarse (fail-closed con auditoría inmutable)
- [x] El stock respeta los apartados activos

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
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
| Spec | [`spec.md`](spec.md) | ☑ Completo |
| Plan | [`plan.md`](plan.md) | ☑ Constitution Check PASS |
| Tareas | [`tasks.md`](tasks.md) | ☑ Completadas (T035-01..12) |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/` | ☑ Verdes (`OfferTests.cs`, `UpdateOfferCommandHandlerTests.cs`, `UpdateOfferCommandValidatorTests.cs`) |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/OfferEditEndpointsTests.cs` | ☑ 9 pruebas verdes (200, 401, 403, 404, 409 versión obsoleta, 409 stock menor a apartados, 400 stock inválido, moderación fail-closed y auditoría fail-closed) |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ Actualizado con 200/400/401/403/404/409/422 |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `PUT /api/v1/offers/{id}` con 403 por propietario, 409 por versión y stock ≥ apartados activos. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-20 | Antigravity | PARCIAL (FR-035-08 cerrado) | `UpdateOfferCommandValidator`, `UpdateOfferCommandHandler` y `Offer.Update` validan `OfferCategory` detrás de `OfferCategory:RequireStrictValidation` (`false` por defecto hasta publicar móvil T15.15). Pruebas en `OfferTests`, `UpdateOfferCommandValidatorTests`, `UpdateOfferCommandHandlerTests` y `OfferCategoryEndpointsTests`. |
| 2026-11-23 | Antigravity | PASS (100 %) | Cierre completo de spec 035. Verificación de re-moderación fail-closed (`EvaluateAsync` + `RecordAsync`), contrato OpenAPI alineado, y suite completa de pruebas de integración en `OfferEditEndpointsTests.cs` (9/9 verdes). |
