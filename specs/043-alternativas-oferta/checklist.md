# Checklist 043 — Alternativas a una oferta

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 043 está aprobada y sin preguntas bloqueantes (ruta `/api/v1/offers/{id}/alternatives`, invitado 2 km y `finalPrice <= 120 %` resueltos el 2026-11-20)
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 013, 032, 041, 052
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La oferta original nunca aparece (`FR-043-03`)
- [x] El límite `[1, 10]` (5 por defecto) está validado (`FR-043-07`, `FR-043-08`)
- [x] Solo se devuelven ofertas visibles (`Published`, vigente, `Stock > 0`, `merchant.activo == true`)
- [x] La consulta usa índices declarados (`ix_offers_category_status_price` + `2dsphere`, verificados en MongoDB real)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde (incluyendo MongoDB 8 real)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml`)
- [x] IaC sin cambios requeridos (sin recursos AWS nuevos; Free Tier respetado)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada (`404` uniforme, recorte invitado 2 km, `ReadHeavyPolicy`)
- [x] Constitution Check en PASS o brecha registrada (`NFR-043-01` carga p95 en brecha `G-13`)

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS (p95 en brecha `G-13`)
- [x] **VIII. Seguridad de nivel fintech** — PASS (STRIDE en el plan)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/Discovery/OfferAlternativesTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/OfferDiscoveryEndpointsTests.cs`, `Persistence/MongoOfferDiscoveryIntegrationTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-20 | Ingeniería Sensus (con asistente) | Implementado (`T043-01`..`T043-12`) | Implementada política `OfferAlternativesPolicy` (`finalPrice <= 120 %`, radio 10 km registrado / 2 km invitado), `GetOfferAlternativesQuery`, índice `ix_offers_category_status_price` + `$geoNear`, `GET /api/v1/offers/{id:guid}/alternatives` con `404` uniforme (`OfferAlternatives.NotFound`), `ReadHeavyPolicy` y pruebas unitarias, HTTP (`400`/`401`/`404`/`200`/`429`) y contra MongoDB 8 real. Pendiente: carga p95 (`G-13`). |
