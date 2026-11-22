# Checklist 053 — Comercios cercanos

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Retro-spec del código existente con requisitos y criterios medibles
- [x] Dependencias identificadas: 005, 010, 013, 052

## B. Específicos de este spec

- [x] Solo comercios activos, ordenados por cercanía
- [x] Radio recortado en el servidor para invitados
- [ ] Coordenadas validadas (T053-03)
- [ ] Resultado paginado 20/100 (T053-04)
- [ ] Límite de tasa (T053-05)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [ ] Pruebas de integración del endpoint (T053-06)
- [ ] Constitution Check en PASS (IV, V, VII y VIII en FAIL, registradas como deuda)

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Retro-spec creada | Comportamiento leído del código; 4 brechas registradas |
