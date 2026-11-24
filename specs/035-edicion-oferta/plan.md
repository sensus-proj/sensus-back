# Plan 035 — Edición de oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Permitir que el comerciante corrija una oferta existente sin romper apartados activos ni saltarse la moderación cuando cambia el contenido visible, mediante control de concurrencia optimista (versión) y re-moderación fail-closed con auditoría inmutable.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Reglas de edición y versión de la oferta (`Offer.Update`), validación de `OfferCategory` |
| Application | `UpdateOfferCommand`, `UpdateOfferCommandValidator`, `UpdateOfferCommandHandler` con re-moderación (`EvaluateAsync` + `RecordAsync` fail-closed) |
| Infrastructure | `MongoOfferRepository.UpdateWithVersionCheckAsync` atómico con filtro de versión |
| Api | Endpoint `PUT /api/v1/offers/{id}` con autorización de comerciante y política `SensitiveWritePolicy` |

## 3. Modelo de datos e índices

- Colección `offers` con campo `version` indexado en transiciones atómicas; `offer_claims` consultados para el stock reservado (`GetActiveClaimCountAsync`).

## 4. Seguridad

- Autorización por propietario (comercio activo del usuario autenticado).
- Re-moderación obligatoria y fail-closed del contenido visible (`EvaluateAsync` + `RecordAsync` en `UpdateOfferCommandHandler`).
- Validación de `OfferCategory` en edición (`FR-035-08`) controlada por `OfferCategory:RequireStrictValidation` (`false` por defecto hasta publicar móvil T15.15; cuando es `true`, rechaza con `400` limpiar categoría en ofertas publicadas o usar un valor fuera del catálogo).
- STRIDE: Tampering (versión optimista previene sobrescrituras), Elevation of Privilege (valida ownership y rol comerciante en servidor).

## 5. Observabilidad y rendimiento

- Métricas de ediciones, conflictos y re-moderaciones.
- Inclusión de correlation-id en respuestas y logs estructurados.

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de reglas de stock y categoría en `OfferTests.cs`.
- Integración en `OfferEditEndpointsTests.cs`: 200 con versión incrementada, 401, 403, 404, 409 versión obsoleta, 409 stock menor a apartados activos, 400 stock inválido, moderación fail-closed y auditoría fail-closed.

## 8. Constitution Check

Evaluación integral tras la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-035-01` a `FR-035-08` cubiertos con criterios de aceptación medibles |
| II | PASS | Capas limpias: Domain y Application independientes de Mongo y ASP.NET |
| III | PASS | CQRS estricto: `UpdateOfferCommand` devuelve `OfferDto` sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad: re-moderación fail-closed (FR-035-03, FR-035-07) y auditoría inmutable (Spec 021) |
| V | PASS | 9 pruebas de integración en `OfferEditEndpointsTests.cs` y pruebas unitarias en verde |
| VI | PASS | Costo cero verificado en §6 |
| VII | PASS | Concurrencia optimista por versión y consultas indexadas sin escaneo de colección |
| VIII | PASS | OWASP ASVS L2/L3: autorización en servidor, validación con lista de permitidos, auditoría inmutable de solo adición |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Una edición concurrente puede sobrescribir cambios legítimos
- Reducir stock puede invalidar apartados activos

## 10. Orden de implementación

1. `T035-01` Actualizar y validar el contrato OpenAPI del spec
2. `T035-02` Implementar: Reglas de edición y versión de la oferta
3. `T035-03` Implementar: UpdateOfferCommand y validador
4. `T035-04` Implementar: Actualización con filtro de versión y re-moderación
5. `T035-05` Implementar: Endpoint PUT /api/v1/offers/{id}
6. `T035-06` Modelar la versión y las reglas de edición
7. `T035-07` Calcular el stock reservado antes de validar
8. `T035-08` Aplicar el filtro de versión en la actualización
9. `T035-09` Implementar las pruebas de integración del flujo completo
10. `T035-10` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
11. `T035-11` Registrar evidencias del Constitution Check
