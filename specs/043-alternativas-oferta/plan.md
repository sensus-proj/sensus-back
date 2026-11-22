# Plan 043 — Alternativas a una oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | En ejecución (v1.2.0) |
| Versión | 1.2.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Sugerir ofertas visibles similares a una oferta consultada —de la misma categoría `OfferCategory`, dentro de un radio geográfico máximo (10 km para usuarios registrados, 2 km para invitados) y con precio final comparable (<= 120 % del `finalPrice` de la oferta original)— para evitar que el usuario abandone la búsqueda.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `OfferAlternativeCandidate`, `OfferAlternativesPolicy`, `IOfferRepository.GetAlternativesAsync` |
| Application | `OfferAlternativesSettings`, `GetOfferAlternativesQuery`, `GetOfferAlternativesQueryValidator`, `GetOfferAlternativesQueryHandler`, `OfferAlternativeItemDto` |
| Infrastructure | Índice `ix_offers_category_status_price` (`category`, `status`, `discountedPrice`, `endsAt`) en `MongoDbContext` y agregación con `$geoNear` + `$lookup` en `MongoOfferRepository` |
| Api | Endpoint `GET /api/v1/offers/{id:guid}/alternatives` (`GuestOrRegisteredPolicy`, `ReadHeavyPolicy`) |

## 3. Modelo de datos e índices

- **Índices en `offers`**:
  - `ix_offers_category_status_price`: `(category, status, discountedPrice, endsAt)` para prefiltrar categoría, estado `Published`, techo de precio final (`discountedPrice <= maxFinalPrice`) y vigencia.
  - `2dsphere` sobre `ubicacion` para calcular distancia con `$geoNear` respecto a la ubicación de la oferta origen (o de su comercio activo).
- **Cálculo de precio comparable (`FR-043-04`)**: `maxFinalPrice = original.DiscountedPrice * ComparablePriceRatio` (donde `ComparablePriceRatio = 1.20m` configurable en `OfferAlternativesSettings`).
- **Visibilidad y origen (`FR-043-06`, `FR-043-10`)**: si la oferta original no existe, no está publicada, expiró, no tiene stock, carece de ubicación o su comercio no está activo, se devuelve el mismo `404 ProblemDetails`.

## 4. Seguridad

- Solo ofertas visibles (`Published`, `EndsAt > nowUtc`, `Stock > 0`, `merchant.activo == true`).
- Sin exponer datos internos del comercio (`merchantName` y `merchantId` únicamente).
- Validación con lista de permitidos: `id` GUID no vacío y `limit` en `[1, 10]` (5 por defecto).
- Invitado (`FR-043-11`): distancia máxima recortada a `2 km` (`2000 m`) mediante `GuestRadiusPolicy`; registrados hasta `10 km` (`10000 m`).
- Límite de tasa (`FR-043-09`): `ReadHeavyPolicy`.
- Ruta como subrecurso `/api/v1/offers/{id:guid}/alternatives` (ADR 0008).

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Enumerar ofertas ocultas probando identificadores | Divulgación de información | `404` idéntico (`OfferAlternatives.NotFound`) para oferta inexistente, no visible o de comercio inactivo (`FR-043-10`) | Bajo |
| Barrido del catálogo encadenando alternativas | Divulgación de información | `read-heavy`, máximo 10 resultados y radio máximo 10 km (2 km invitados) | Bajo |
| Identificador malformado que provoca errores del motor | Denegación de servicio | Restricción `{id:guid}` en ruta y validador explícito | Bajo |
| Exposición de datos internos del comercio | Divulgación de información | Proyección explícita a `OfferAlternativeItemDto` | Bajo |

## 5. Observabilidad y rendimiento

- Logs estructurados con `correlation-id`, resultado (encontrada vs vacía/404), límite efectivo y latencia en ms (sin coordenadas ni PII). Métricas en Free Tier (`10/10`) sin nuevas métricas CloudWatch personalizadas.
- p95 < 500 ms queda sujeto a pruebas de carga (`G-13`).

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de `OfferAlternativesPolicy`, cálculo del 120 % sobre `finalPrice`, orden por cercanía/vigencia y validador `limit [1, 10]`.
- Integración HTTP: `200` (hasta 5 por defecto, lista vacía), `400` (`limit=20`, GUID vacío), `401` sin token, `404` idéntico (inexistente vs pausada/expirada/sin stock/comercio inactivo), invitado recortado a 2 km y `429`.
- Integración contra MongoDB real (`MONGO_CONNECTION_STRING`): verificación del índice `ix_offers_category_status_price`, exclusión de la oferta original, techo del 120 % del precio final y recorte a 2 km/10 km.

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-043-01`..`11` y `NFR-043-01`..`02` verificables; pregunta de precio final resuelta |
| II | PASS | Cambios acotados a Domain, Application, Infrastructure y Api |
| III | PASS | `IReadOnlyList<OfferAlternativeItemDto>`, sin entidades ni documentos expuestos |
| IV | PASS | `404` uniforme, recorte invitado 2 km y filtros de visibilidad |
| V | PASS | Unitarias, HTTP e integración contra MongoDB real |
| VI | PASS | Sin recursos AWS nuevos; Free Tier respetado |
| VII | PASS | Índice `ix_offers_category_status_price` + `2dsphere`; p95 en `G-13` |
| VIII | PASS | STRIDE en §4, allowlist, errores uniformes y `read-heavy` |

> Brecha conocida aplicable al p95 en carga real: `G-13` (Apéndice F).

## 9. Orden de implementación

1. `T043-01` Actualizar y validar el contrato OpenAPI (`contracts/openapi.yaml`)
2. `T043-02`/`T043-05` Domain y Application (`OfferAlternativesPolicy`, `GetOfferAlternativesQuery`, `OfferAlternativeItemDto`, filtro categoría + 120 % precio final)
3. `T043-03`/`T043-06` Infrastructure: índice `ix_offers_category_status_price` en `MongoDbContext` y consulta `$geoNear` en `MongoOfferRepository`
4. `T043-04`/`T043-07`/`T043-11`/`T043-12` Endpoint `GET /api/v1/offers/{id:guid}/alternatives`, `ReadHeavyPolicy`, `404` uniforme e invitado 2 km
5. `T043-08` Pruebas unitarias, HTTP y contra MongoDB real
6. `T043-09`/`T043-10` Actualizar `specs/README.md`, `requirements.md` y `checklist.md`
