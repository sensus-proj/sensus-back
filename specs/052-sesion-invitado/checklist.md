# Checklist 052 — Sesión de invitado

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Retro-spec del código existente con requisitos y criterios medibles
- [x] Dependencias identificadas: 002, 013

## B. Específicos de este spec

- [x] El invitado no genera registros persistentes
- [x] El radio se recorta en el servidor
- [x] El invitado no accede a operaciones de registrado
- [x] La vigencia del token sale de configuración y cumple el Principio VIII (T052-03: `JwtSettings:GuestExpiryMinutes = 15`)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [x] Pruebas de integración y E2E existentes
- [x] Constitution Check en PASS (brecha G-06 resuelta al externalizar y limitar vigencia de invitado a ≤ 15 min)

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |
| Pruebas | `GuestSessionEndpointsTests.cs`, `JwtSettingsValidatorTests.cs`, `GuestModeE2EJourneyTests.cs` | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Retro-spec creada | Comportamiento leído del código en producción; brecha de vigencia registrada |
| 2026-11-21 | Beto (Sprint 5) | CERRADA | `GuestExpiryMinutes = 15` movido a configuración con validación `[1..15]` y verificado en pruebas. |
| 2026-11-26 | Claude | CERRADA | `FR-052-06` marcado Implementado en `spec.md` (estaba Pendiente pese al cierre del 2026-11-21): `JwtSettings.GuestExpiryMinutes = 15`, `JwtSettingsValidator` admite solo 1..15 y `JwtTokenGenerator` lo aplica; `GuestSessionEndpointsTests` verifica 2000 m y TTL ≤ 15 min. `FR-052-03` verificado en código (`NearbyOffersRadiusPolicy` recorta al invitado con `Math.Min(requested, 2)`, `NearbyOffersRadiusPolicyTests`) y en producción el 2026-11-25: invitado con `radiusKm=10` → 200 con radio efectivo de 2 km. |
