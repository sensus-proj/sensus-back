# Checklist 036 — Ciclo de vida de la oferta

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 036 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 034, 035
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Cada transición está probada en sus límites
- [x] La visibilidad cambia de inmediato
- [x] Las ofertas republicadas pasan por moderación de texto (`RepublishOfferCommandHandler`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml`)
- [x] IaC actualizada y sin cambios destructivos pendientes
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización y rate limiting `sensitive-write` verificados
- [x] Constitution Check en PASS

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS (`ContentModerationHelper` en `RepublishOffer`, `AdminOnly` en `expire-check`, `SensitiveWritePolicy` en `/republish`)
- [x] **V. Verificación automatizada** — PASS (`OfferLifecycleHistoryTests`, `OfferLifecycleEndpointsTests`, `ExpireCheckAuthorizationTests`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS (`OfferStatusTransition` persistido en `offer_status_history`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/OfferLifecycleHistoryTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/OfferLifecycleEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | Máquina de estados inicial implementada. |
| 2026-11-21 | Beto (Sprint 5) | IMPLEMENTADO | Añadido contrato `openapi.yaml`, historial persistido en `offer_status_history`, rate limit `sensitive-write` en `/republish` y pruebas completas. |
