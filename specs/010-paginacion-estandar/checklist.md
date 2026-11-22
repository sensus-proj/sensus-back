# Checklist 010 — Paginación estándar

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 010 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 009
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Endpoints nuevos y listados principales paginados (`PagedResult<T>` / `PaginationPolicy`, `GetPostCommentsQuery`, `GetStoreReviewsQuery` T056-05, búsqueda/feed/cercanas; endpoints legacy de arreglo raíz documentados por compatibilidad con el cliente móvil)
- [x] Los límites están validados (`Page >= 1`, `PageSize` entre `1` y `100`)
- [x] Los índices de las consultas están declarados en `MongoDbContext`

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
- [x] IaC sin cambios requeridos
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS

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
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Common/PaginationPolicyTests.cs`, `FeedCommentsTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Feed/FeedCommentEndpointsTests.cs`, `OfferFeedEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | `/openapi/v1.json` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-21 | Beto (Sprint 5) | IMPLEMENTADO | Incorporada política `PaginationPolicy` (`1..100`), paginación en `GetPostCommentsQuery` y `GetStoreReviewsQuery` (T056-05 compatible con Android). |
