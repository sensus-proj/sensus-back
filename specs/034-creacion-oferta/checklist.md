# Checklist 034 — Creación de oferta

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 034 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 005, 017, 022
- [x] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Ninguna oferta se expone sin veredicto (`PendingModeration` y `Rejected` ocultas del feed cercano, búsqueda y detalle público salvo al propietario)
- [x] El comercio proviene del token
- [x] El precio final se calcula en el servidor
- [x] El borrador se lista en el inventario privado, se oculta en el detalle público y no genera avisos (`FR-034-04`)
- [x] Al crear una oferta publicada con imagen se exige el activo aprobado del titular y la coincidencia con el contexto moderado (`T034-14`)
- [x] El límite por comercio está probado (`T034-18`: 33/33 unitarias A4, 7/7 endpoint y 1/1 Testcontainers MongoDB)
- [x] La categoría de producto es obligatoria al publicar (detrás de `OfferCategory:RequireStrictValidation=false` hasta publicar móvil T15.15) y la migración idempotente está implementada (`OfferCategoryMigrationService`, `T034-13`, `T034-14`)
- [x] La migración se ejecuta automáticamente al arrancar y conserva su evidencia ante arranques concurrentes (`T034-15`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml` v1.3.0 con `201`, `202` y `409`)
- [x] IaC actualizada y `cdk diff` revisado si aplica (N/A — worker en proceso con EventBridge/Lambda existente sin nuevos recursos AWS)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [ ] **IV. Seguridad y privacidad** — Falta moderar el flujo de ofertas sin imagen y resolver la cuarentena previa a Cloudinary. Las ofertas con imagen verifican aprobación, titular y huella del contexto.
- [ ] **V. Verificación automatizada** — El contrato actual tiene pruebas HTTP. Faltan los casos de moderación pendiente, límite activo y publicación segura.
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☐ |
| Plan | [`plan.md`](plan.md) | ☐ |
| Tareas | [`tasks.md`](tasks.md) | ☐ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☐ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☐ |

## F. Registro

- [ ] Para habilitar las alertas de `FR-044-08`, persistir evidencia de moderación conjunta vinculada a la versión y contenido de la oferta. La ruta de creación actual puede guardar `Published` sin esa evidencia; ningún aviso debe usar el estado como sustituto del veredicto (`FR-034-03`, `NFR-034-02`).

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `POST /api/v1/offers` con invariantes en `Offer.Create`. Faltan Draft, límite de 20 activas y respuesta 202. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-20 | Antigravity | PARCIAL (FR-034-08/09 cerrados) | `OfferCategory` + `OfferCategoryCatalog` en Domain; `OfferCategorySettings.RequireStrictValidation` (`false` por defecto hasta publicar móvil T15.15) en `CreateOfferCommandValidator`/`Handler`/`Offer.Create`; `OfferCategoryMigrationService` idempotente (`offer_category_v1` en `schema_migrations`). Cubierto por `OfferCategoryCatalogTests`, `OfferTests`, `CreateOfferCommandValidatorTests`, `CreateOfferCommandHandlerTests`, `OfferCategoryEndpointsTests` y `OfferCategoryMigrationServiceTests`. |
| 2026-11-20 | Rodrigo Sánchez (con asistente) | T034-15 cerrada | `OfferCategoryMigrationHostedService` ejecuta la migración una vez por arranque (tolerante a fallos) y el marcador pasa a *insert-once* para conservar la evidencia ante arranques concurrentes. `OfferCategoryMigrationHostedServiceTests` (3) y prueba de concurrencia contra MongoDB real (8/8 repeticiones); suite 1326/1326 en la corrida final; cobertura Domain 89.78 % y Application 94.61 %. |
| 2026-11-22 | Codex | Parcial (FR-034-03/04 verificados) | OpenAPI documenta el contrato actual. `OfferTests` y `CreateOfferEndpointsTests` verifican borrador privado, inventario, falta de aviso, transiciones bloqueadas y huella de imagen moderada con el texto. |
| 2026-11-26 | Ejecutor nocturno (con asistente) | T034-18 cerrada (`FR-034-05`) | Límite configurable `Offers:MaxActivePerMerchant=20` aplicado en creación (`!SaveAsDraft`), reanudación a `Published` y republicación; índice `ix_offers_merchant_status_ends_at`; OpenAPI `409 Conflict`. `CreateOfferCommandHandlerTests`, `ChangeOfferStatusCommandHandlerTests`, `RepublishOfferCommandHandlerTests` y `OfferLimitSettingsTests` (33/33), `CreateOfferEndpointsTests` (7/7) y `MongoOfferActiveCountIntegrationTests` Testcontainers (1/1) en verde. |
| 2026-11-26 | Antigravity | T034-19 cerrada (`FR-034-07`) | Respuesta `202 Accepted` (`OfferStatus.PendingModeration`) cuando el moderador falla o expira en `POST /api/v1/offers`; ocultamiento en detalle público salvo propietario, cercanía, búsqueda y notificaciones; worker `OfferModerationRetryBackgroundService` + `ProcessPendingOfferModerationCommand` con índice `ix_offers_pending_moderation_retry`; OpenAPI `1.3.0`. |
