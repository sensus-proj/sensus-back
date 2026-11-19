# Checklist 052 — Sesión de invitado

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Retro-spec del código existente con requisitos y criterios medibles
- [x] Dependencias identificadas: 002, 013

## B. Específicos de este spec

- [x] El invitado no genera registros persistentes
- [x] El radio se recorta en el servidor
- [x] El invitado no accede a operaciones de registrado
- [ ] La vigencia del token sale de configuración y cumple el Principio VIII (T052-03)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [x] Pruebas de integración y E2E existentes
- [ ] Constitution Check en PASS (IV y VIII en FAIL por FR-052-06, registrada como deuda)

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |
| Pruebas | `GuestSessionEndpointsTests.cs`, `GuestModeE2EJourneyTests.cs` | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Retro-spec creada | Comportamiento leído del código en producción; brecha de vigencia registrada |
