# Checklist 033 — Apartado de oferta con código QR

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 033 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 007, 032, 037
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El stock nunca queda negativo
- [x] Un código solo se canjea una vez
- [x] El canje valida la propiedad del comercio
- [x] Los apartados vencidos liberan stock

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica (sin cambios de infraestructura: Lambda y API Gateway ya declarados)
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
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/` (OfferClaimTests, ClaimOffer, RedeemClaim, ExpireClaims) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/OfferClaimEndpointsTests.cs`, `Security/HmacClaimCodeServiceTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-13 | Equipo Sensus | PASS | `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, 337 unitarias y 78 de integración en verde. Firma HMAC con `FixedTimeEquals`; secreto por configuración. |
