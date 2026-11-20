# Checklist 041 — Búsqueda de ofertas

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 041 está aprobada y sin preguntas bloqueantes (preguntas de ruta, invitado 2 km y stemming español + prefijo resueltas el 2026-11-20)
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 007, 010, 011, 013, 052
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El ranking es determinista (`OfferSearchRanker`, `NFR-041-03`)
- [x] Los pesos están externalizados (`OfferSearchSettings` en `appsettings.json`)
- [x] Las ofertas no visibles nunca aparecen (`Published`, vigente, `Stock > 0`, `merchant.activo == true`)
- [x] La métrica de relevancia se mide en pruebas (`OfferSearchTests` `NFR-041-02` >= 90 % y `MongoOfferDiscoveryIntegrationTests`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde (incluyendo MongoDB 8 real)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml`)
- [x] IaC sin cambios requeridos (sin recursos AWS nuevos; Free Tier respetado)
- [x] Logs con correlation-id y sin PII (`ISensitiveRequest`, sin `q` ni coordenadas en logs)
- [x] Revisión de seguridad: sin secretos, autorización verificada (`GuestOrRegisteredPolicy`, recorte invitado 2 km, `ReadHeavyPolicy`)
- [x] Constitution Check en PASS o brecha registrada (`NFR-041-01` carga p95 en brecha `G-13`)

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
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/Discovery/OfferSearchTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/OfferDiscoveryEndpointsTests.cs`, `Persistence/MongoOfferDiscoveryIntegrationTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-20 | Ingeniería Sensus (con asistente) | Implementado (`T041-01`..`T041-12`) | Implementada búsqueda híbrida (`txt_offers_spanish` con `default_language: "spanish"` + `ix_offers_title_normalized` para prefijos >= 2 chars), `OfferTextNormalizer`, `OfferSearchRanker`, `SearchOffersQuery`, `OfferSearchSettings`, recorte de invitado a 2 km con `GuestRadiusPolicy`, `GET /api/v1/offers/search` (`ReadHeavyPolicy`) y pruebas unitarias, HTTP (`400`/`401`/`200`/`429`) y contra MongoDB 8 real. Pendiente: carga p95 (`G-13`). |
