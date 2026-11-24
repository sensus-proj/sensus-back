# Tareas 035 — Edición de oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completada |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado | Evidencia |
|---|---|---|---|---|---|---|
| T035-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-035-01` | — | Completada | `specs/035-edicion-oferta/contracts/openapi.yaml` actualizado con 200/400/401/403/404/409/422 según `OfferEndpoints.cs:56-86` |
| T035-02 | Implementar: Reglas de edición y versión de la oferta | Domain | `FR-035-01` | T035-01 | Completada | `Offer.Update` en `Offer.cs`, probado en `OfferTests.cs` |
| T035-03 | Implementar: UpdateOfferCommand y validador | Application | `FR-035-01` | T035-02 | Completada | `UpdateOfferCommand.cs`, `UpdateOfferCommandValidator.cs`, probado en `UpdateOfferCommandValidatorTests.cs` |
| T035-04 | Implementar: Actualización con filtro de versión y re-moderación | Infrastructure | `FR-035-01` | T035-03 | Completada | `UpdateOfferCommandHandler.cs:100-140` re-modera con fail-closed (`EvaluateAsync` + `RecordAsync`) y `MongoOfferRepository.UpdateWithVersionCheckAsync` |
| T035-05 | Implementar: Endpoint PUT /api/v1/offers/{id} | Api | `FR-035-01` | T035-04 | Completada | `OfferEndpoints.cs:56-86` con `MerchantOnlyPolicy` |
| T035-06 | Modelar la versión y las reglas de edición | Domain | `FR-035-05` | T035-05 | Completada | Campo `Version` en `Offer.cs` con incremento en cada actualización |
| T035-07 | Calcular el stock reservado antes de validar | Application | `FR-035-04` | T035-06 | Completada | `OfferClaimRepository.GetActiveClaimCountAsync`, probado en `OfferEditEndpointsTests.EditOffer_StockBelowActiveClaims_Returns409Conflict` |
| T035-08 | Aplicar el filtro de versión en la actualización | Infrastructure | `FR-035-05` | T035-07 | Completada | `MongoOfferRepository.UpdateWithVersionCheckAsync` con filtro atómico de versión |
| T035-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-035-01` | T035-08 | Completada | `tests/Sensus.IntegrationTests/Offers/OfferEditEndpointsTests.cs` (9/9 pruebas verdes cubriendo 200, 401, 403, 404, 409, 400, moderación fail-closed y auditoría fail-closed) |
| T035-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-035-01` | T035-09 | Completada | `specs/README.md`, `requirements.md`, `checklist.md` sin casillas abiertas |
| T035-11 | Registrar evidencias del Constitution Check | Docs | `FR-035-01` | T035-10 | Completada | `plan.md` y `checklist.md` con Constitution Check PASS en los 8 principios |
| T035-12 | Validar `OfferCategory` en la edición con pruebas de `400` | Application | `FR-035-08` | T035-11 | Completada | `UpdateOfferCommandValidatorTests` y `OfferCategoryEndpointsTests` |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
