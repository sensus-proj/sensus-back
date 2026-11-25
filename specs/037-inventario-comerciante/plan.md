# Plan 037 — Inventario de ofertas del comerciante

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 2.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## 1. Resumen técnico

Permitir que el comerciante consulte el historial paginado de sus ofertas con métricas de apartados, canjes y vistas en tiempo real, filtrable por estado y ordenado por fecha de creación descendente. El filtro, la ordenación y el límite se resuelven en MongoDB; las métricas se obtienen con un conteo batch por página, sin consultas por oferta.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `OfferClaimCounts` (solo totales) y `IOfferClaimRepository.CountByOfferIdsAsync` con implementación por defecto que devuelve un diccionario vacío |
| Application | `GetMerchantOffersQuery` paginada con `GetMerchantOffersQueryValidator`; retorno `PagedResult<MerchantOfferDto>`; `MerchantOfferDto` con `ActiveClaimsCount`, `RedemptionsCount` y `ViewsCount`; mapeo explícito `ToMerchantOfferDto` |
| Infrastructure | `MongoOfferClaimRepository.CountByOfferIdsAsync` con `Filter.In` y proyección de `offerId`/`status`; índices declarados en `MongoDbContext` |
| Api | `GET /api/v1/merchants/my-offers` acepta `page` y `pageSize`, valida el borde y documenta 200/400/401/403 |

## 3. Modelo de datos e índices

- `offers`: índices `ix_offers_merchant_created` `(MerchantId, CreatedAtUtc desc)` y `ix_offers_merchant_status_created` `(MerchantId, Status, CreatedAtUtc desc)`. Cubren FR-037-05 con y sin filtro de estado.
- `offer_claims`: índice `ix_offer_claims_offer_status` `(OfferId, Status)` para el conteo batch de FR-037-03.
- El conteo de vistas usa `Offer.ViewCount`, ya incrementado por `RegisterOfferViewCommandHandler`; no se creaCollection ni se agrega sobre `offer_views` en esta spec.
- La proyección del conteo excluye `userId`, `codeHash` y fechas: la respuesta solo expone totales.

## 4. Seguridad

- El `merchantId` se resuelve desde el usuario autenticado; el cliente no puede enviarlo ni influir en él.
- Autorización `MerchantOnlyPolicy` en el endpoint y comprobación de comercio activo en el handler.
- `status` se valida contra la lista de permitidos `OfferStatus`; `page`/`pageSize` se validan contra `PaginationPolicy` (1–100).
- Errores uniformes `ProblemDetails` con `correlation-id`, sin trazas, nombres de colección ni datos internos.
- Sin secretos nuevos; la cadena de MongoDB se resuelve desde SSM bajo `/sensus/<entorno>/`.
- **Modelo de amenazas (STRIDE) del inventario**

| Amenaza | Vector | Mitigación | Riesgo residual |
|---|---|---|---|
| *Spoofing* | Token manipulado para consultar otro comercio | JWT validado y `merchantId` tomado del usuario autenticado; `MerchantOnlyPolicy` | Bajo |
| *Tampering* | Parámetros `status`, `page` o `pageSize` fuera de rango | Validación `AbstractValidator` + parseo de enum en el handler; 400 con detalle | Bajo |
| *Repudiation* | Consulta de métricas sin rastro | `LoggingBehavior` registra la consulta con `correlation-id`, sin PII ni códigos | Bajo |
| *Information disclosure* | Exposición de usuarios, códigos o hashes de apartado | `OfferClaimCounts` solo totales y proyección que excluye campos sensibles | Muy bajo |
| *DoS* | `pageSize` desproporcionado o paginado profundo | Tope de 100, filtro por `merchantId`, índices declarados y conteo batch acotado al tamaño de página | Medio-bajo |
| *Elevation of privilege* | Usuario sin rol consulte inventario | Autorización por rol `Comerciante` en servidor y 403/401 según autenticación | Bajo |

- **Principio VIII (DevSecOps de nivel fintech):** ASVS 5.0 nivel 2 (nivel 3 en autorización), OWASP API Top 10, validación con lista de permitidos, mínimo privilegio sobre los datos del comercio, errores uniformes, logs sin PII con `correlation-id` y escaneos bloqueantes del pipeline. Sin recursos ni secretos nuevos.

## 5. Observabilidad y rendimiento

- `LoggingBehavior` y `PerformanceBehavior` registran latencia y resultado de `GetMerchantOffersQuery` con `correlation-id`.
- `NFR-037-01`: p95 < 600 ms. Se verifica con 20 solicitudes consecutivas en integración y se vigila con la métrica de latencia de solicitudes existente.
- `NFR-037-02`: un solo `Find` con `Filter.In` para todos los apartados de la página; el total de ofertas se cuenta en la misma consulta paginada.

## 6. Costo AWS

- Sin costo incremental: reutiliza Lambda, API Gateway y MongoDB Atlas existentes, todos dentro de las excepciones vigentes del Free Tier (Apéndice E).
- Sin paquetes NuGet nuevos y sin cambios en IaC; no se requiere `cdk deploy`.

## 7. Estrategia de pruebas

- Unitarias del handler: paginación, filtro `Archived`, aislamiento entre comercios, métricas (vistas/claims/likes), estado inválido y normalización de parámetros.
- Unitarias de validación de borde: `pageSize` 200 y `page` 0 rechazados.
- Integración del endpoint: 200 con métricas, filtro archivado, lista vacía, 400 por tamaño/estado inválido, 401 sin token, 403 sin rol, aislamiento entre comercios y latencia p95 < 600 ms.
- Verificación de cobertura ≥ 80 % en Domain y Application y test de arquitectura en verde.

## 8. Constitution Check

Evaluación del plan vigente.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-037-01` a `FR-037-06` y `NFR-037-01` a `NFR-037-03` con criterios medibles en `spec.md` §6 |
| II | PASS | Cambios acotados a las capas declaradas en §2; repositorio solo en Infrastructure |
| III | PASS | `PagedResult<MerchantOfferDto>`, DTO plano con métricas y contrato OpenAPI v2.0.0; sin entidades ni documentos en la respuesta |
| IV | PASS | `merchantId` del token, rol `Comerciante`, validación de borde, sin PII ni códigos en métricas (§4) |
| V | PASS | Estrategia de pruebas en §7 con 11 pruebas del handler, 5 de validación y 9 de integración |
| VI | PASS | Sin costo incremental, sin paquetes nuevos y sin cambios en IaC (§6) |
| VII | PASS | Paginación 20/100, índices declarados, conteo batch y objetivo de latencia (§3 y §5) |
| VIII | PASS | Modelo STRIDE y controles fintech en §4; sin excepciones nuevas |

Resultado global: **PASS**. Sin excepciones registradas en el Apéndice E de la constitución.

## 9. Riesgos mitigados

- Agregación acotada al lote de la página y con proyección mínima.
- Total calculado en la misma consulta paginada, sin doble recuento independiente.
- El conteo batch se apoya en `Filter.In`; no depende de operadores de agregación no verificados en este entorno.

## 10. Orden de implementación

1. `T037-01` Contrato OpenAPI v2.0.0 validado
2. `T037-02` Consulta paginada y proyección con métricas
3. `T037-03` Conteo batch en repositorio Mongo
4. `T037-04` Endpoint con paginación y errores 400
5. `T037-05` DTO de inventario con métricas
6. `T037-06` Índices de soporte
7. `T037-07` Aislamiento entre comercios
8. `T037-08` Pruebas de integración del flujo completo
9. `T037-09` Actualización de índice de specs y requisitos
10. `T037-10` Evidencias del Constitution Check
