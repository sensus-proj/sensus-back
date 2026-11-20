# Checklist 035 — Edición de oferta

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 035 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 034, 033
- [x] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El propietario se valida en el servidor
- [x] La versión evita sobrescrituras
- [ ] El contenido cambiado vuelve a moderarse
- [x] El stock respeta los apartados activos

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [ ] Pruebas unitarias y de integración añadidas y en verde
- [ ] Cobertura ≥ 80 % en Domain y Application
- [ ] Contrato OpenAPI actualizado
- [ ] IaC actualizada y `cdk diff` revisado si aplica
- [ ] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [ ] **IV. Seguridad y privacidad** — BRECHA: la edición de texto o imagen no vuelve a moderarse (FR-035-03, FR-035-07)
- [ ] **V. Verificación automatizada** — BRECHA: sin pruebas de integración del endpoint
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☐ |
| Plan | [`plan.md`](plan.md) | ☐ |
| Tareas | [`tasks.md`](tasks.md) | ☐ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☐ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☐ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `PUT /api/v1/offers/{id}` con 403 por propietario, 409 por versión y stock ≥ apartados activos. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-20 | Antigravity | PARCIAL (FR-035-08 cerrado) | `UpdateOfferCommandValidator`, `UpdateOfferCommandHandler` y `Offer.Update` validan `OfferCategory` detrás de `OfferCategory:RequireStrictValidation` (`false` por defecto hasta publicar móvil T15.15). Pruebas en `OfferTests`, `UpdateOfferCommandValidatorTests`, `UpdateOfferCommandHandlerTests` y `OfferCategoryEndpointsTests`. |
