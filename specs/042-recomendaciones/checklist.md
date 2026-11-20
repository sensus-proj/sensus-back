# Checklist 042 — Feed de promociones por intereses

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Alcance confirmado por el responsable: feed de promociones separado del social, sin invitados, ruta `/api/v1/feed/offers`
- [x] Requisitos y criterios de aceptación medibles
- [x] Modelo de amenazas STRIDE en el plan
- [ ] Spec 054 (intereses) implementada o con su puerto disponible
- [ ] Spec 055 (interacciones) con su puerto disponible
- [ ] Aviso de privacidad actualizado con la personalización del feed (landing y FT-23)

## B. Específicos de este spec

- [ ] Puntaje con pesos configurables y versionados (T042-02, T042-10)
- [ ] Diversidad y exploración deterministas (T042-02)
- [ ] Paginación estable con instantánea en Redis (T042-05)
- [ ] Invitado recibe `403` (T042-06)
- [ ] Impresiones emitidas con `rankingVersion` (T042-07)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [ ] Pruebas unitarias e integración en verde; cobertura ≥ 80 % en Domain y Application
- [ ] Test de arquitectura en verde
- [ ] Logs con `correlation-id` y sin PII
- [ ] NFR-042-01 a NFR-042-07 verificados
- [ ] Constitution Check en PASS tras la implementación
- [ ] `memory.md` actualizado

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-20 | Rodrigo Sánchez (con asistente) | Spec reescrita a v2.0.0 | De «Recomendaciones» (20 ofertas) a feed de promociones paginado por intereses; alineada a v2.4.0 |
