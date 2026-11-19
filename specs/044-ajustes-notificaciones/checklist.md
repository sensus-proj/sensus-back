# Checklist 044 — Ajustes de notificaciones

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 044 está aprobada y sin preguntas bloqueantes
- [ ] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 027, 003
- [ ] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Las preferencias se respetan en cada envío
- [x] El radio está validado
- [x] Los valores por defecto están documentados
- [x] Solo el titular lee y edita sus ajustes

## C. Antes de entregar (Definition of Done)

- [ ] Código en la capa correcta y test de arquitectura en verde
- [ ] Pruebas unitarias y de integración añadidas y en verde
- [ ] Cobertura ≥ 80 % en Domain y Application
- [ ] Contrato OpenAPI actualizado
- [ ] IaC actualizada y `cdk diff` revisado si aplica
- [ ] Logs con correlation-id y sin PII
- [ ] Revisión de seguridad: sin secretos, autorización verificada
- [ ] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [ ] **I. Especificación y trazabilidad** — PASS
- [ ] **II. Clean Architecture** — PASS
- [ ] **III. CQRS y contratos** — PASS
- [ ] **IV. Seguridad y privacidad** — PASS
- [ ] **V. Verificación automatizada** — PASS
- [ ] **VI. Costo cero e IaC** — PASS
- [ ] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

- [x] Alta, reemplazo y borrado de la ubicación consentida e índices 2dsphere y TTL verificados para `FR-044-07` en MongoDB real.
- [x] Oferta con texto aprobado, radio inclusivo y cero avisos para ofertas sin veredicto `Approved` en `FR-044-08` (alcance de CA-044-06 del 2026-11-19).

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| — | — | — | — |
| 2026-11-18 | Codex | Parcial | API de ubicación verificada para sesión registrada, invitado, anónimo y coordenadas inválidas. MongoDB real verifica reemplazo, exclusión de actualizaciones obsoletas, borrado e índices 2dsphere y TTL. Disparador de ofertas pendiente de `FR-034-03`. |
| 2026-11-19 | Claude | Parcial | `FR-044-08` implementado con el alcance acordado: moderación de título y descripción *fail-closed*, `$geoNear` sobre 2dsphere con vigencia, filtros de preferencias y radio inclusivo, exclusión del comercio y desactivación de tokens rechazados. 7 unitarias nuevas (`NearbyOfferNotifierTests` y `CreateOfferCommandHandlerTests`) y 2 de integración con MongoDB real. Suites: 649 unitarias, 276 de integración y 4 de arquitectura en verde; build `-warnaserror` correcto. Entrega FCM física pendiente de la credencial en SSM. La imagen de la oferta sigue sin moderar (brecha de la spec 034). |
