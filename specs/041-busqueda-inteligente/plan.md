# Plan 041 — Búsqueda de ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | En ejecución (v1.2.0) |
| Versión | 1.2.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Permitir buscar ofertas visibles por texto combinando coincidencia híbrida en español (stemming nativo de MongoDB con `default_language: "spanish"` para palabras completas y prefijo indexado sobre título normalizado sin acentos para consultas desde 2 caracteres) con cercanía geoespacial y popularidad, mediante un ranking determinista en Domain con pesos configurables.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `OfferTextNormalizer`, `OfferSearchWeights`, `OfferSearchCandidate`, `OfferSearchRanker`, extensión de `IOfferRepository` (`SearchOffersHybridAsync`) |
| Application | `OfferSearchSettings`, `SearchOffersQuery`, `SearchOffersQueryValidator`, `SearchOffersQueryHandler`, `OfferSearchItemDto` (`PagedResult<OfferSearchItemDto>`) |
| Infrastructure | Campo `titleNormalized` + `merchantName` opcional + `categorySearchText` en `OfferDocument`, índice de texto `txt_offers_spanish` (`default_language: "spanish"`), índice `ix_offers_title_normalized` y consulta híbrida geoespacial en `MongoOfferRepository` |
| Api | Endpoint `GET /api/v1/offers/search` registrado antes de `/api/v1/offers/{id:guid}`, política `GuestOrRegisteredPolicy`, `ReadHeavyPolicy` y configuración `OfferSearch` |

## 3. Modelo de datos e índices

1. **`OfferDocument` (`offers`)**:
   - `titleNormalized`: título en minúsculas, normalización Unicode NFD sin marcas diacríticas y espacios colapsados (`OfferTextNormalizer.Normalize`). Se calcula de forma determinista al escribir el documento.
   - `categorySearchText`: código canónico + alias en español normalizado de `OfferCategoryCatalog` (ej. `"Bakery panaderia"`) para garantizar coincidencia de categoría en español (`NFR-041-02`).
   - `merchantName`: nombre comercial denormalizado opcional; durante la consulta `$lookup` con `comercios` (`activo: true`) aporta siempre el nombre oficial vigente del comercio.
2. **Índices declarados en `MongoDbContext`**:
   - **`txt_offers_spanish`** (único índice de texto en `offers`): campos `title` (peso `10`), `merchantName` (peso `6`), `categorySearchText` (peso `5`), `category` (peso `4`), `description` (peso `2`), con `DefaultLanguage = "spanish"`. Si existe un índice de texto legado sin nombre/idioma español, se reemplaza de forma controlada al iniciar.
   - **`ix_offers_title_normalized`**: índice ascendente sobre `titleNormalized` para resolver rangos de prefijo (`^<prefijo_normalizado>` sin bandera `i`, aprovechando el B-tree sin recorrido de colección).
   - **`ubicacion` (`2dsphere`)** + **`(status, endsAt)`**: acotan las búsquedas geoespaciales (`$geoWithin` con `$centerSphere` en búsqueda híbrida y `$geoNear` en *fallback* vacío).
3. **Estrategia de consulta híbrida (consulta >= 2 caracteres)**:
   - Canal A (*stemming* español): consulta sobre `$text` (`txt_offers_spanish`) combinada con `$geoWithin` (`$centerSphere`) y filtro de visibilidad (`Published`, `EndsAt > nowUtc`, `Stock > 0`), proyectando `textScore`.
   - Canal B (prefijo mientras el usuario escribe, >= 2 caracteres): consulta por prefijo exacto `^Regex.Escape(normalizedPrefix)` (sin `i`) sobre `titleNormalized` combinada con `$geoWithin` (`$centerSphere`) y visibilidad, usando `ix_offers_title_normalized`.
   - Fusión y enriquecimiento: ambos conjuntos de IDs acotados se fusionan sin duplicados, se valida `merchant.activo == true` vía `$lookup` a `comercios` (`FR-041-06`) y pasan al ranker puro en Domain.
   - Consulta vacía (`FR-041-05`): *fallback* con `$geoNear` ordenado por cercanía sobre `2dsphere` + `$lookup` a `comercios.activo == true`.

## 4. Ranking determinista y reglas de negocio

- Parámetros en configuración (`OfferSearchSettings`):
  - `Version` (ej. `"search-v1"`), `DefaultRadiusKm = 10`, `MaxRadiusKm = 50`, `GuestMaxRadiusKm = 2`, `MaxCandidates = 200`.
  - `Weights`: `Text = 0.45`, `CategoryMatch = 0.25`, `Proximity = 0.20`, `Popularity = 0.10` (suma 1.00; pesos validados al arrancar).
- Puntaje en `OfferSearchRanker` (Domain):
  - Combina señal textual normalizada (coincidencia exacta de prefijo / token normalizado sobre título y comercio + `textScore`), coincidencia exacta de categoría (`CategoryMatch`, asegurando `NFR-041-02` >= 90 %), cercanía `1 - distance/radius` y popularidad relativa por percentil (`viewCount`).
  - Desempate determinista: puntaje descendente, distancia ascendente y `OfferId` ascendente (`NFR-041-03`).

## 5. Seguridad

- Sin exponer documentos internos; proyección explícita a `OfferSearchItemDto` (`PagedResult`).
- Consulta y paginación acotadas (`page >= 1`, `pageSize` por defecto `20` y máximo `100`).
- Lista de permitidos (`FR-041-08`): `q` nulo/vacío o `2..80` caracteres sin caracteres de control (`char.IsControl`), `lat` `[-90, 90]`, `lng` `[-180, 180]`, `radiusKm` `[1, 50]`. Longitud `1` o `> 80` responde `400 ProblemDetails`.
- Invitado (`FR-041-11`): `GuestOrRegisteredPolicy`; el servidor recorta `radiusKm` a `GuestMaxRadiusKm` (`2 km` / `2000 m`) mediante `GuestRadiusPolicy`, ignorando radios mayores solicitados por un invitado.
- Límite de tasa (`FR-041-09`): `ReadHeavyPolicy` (`120/60 s`).
- Logs sin PII, sin término `q` y sin coordenadas (`correlation-id`, longitud de `q`, si es vacía, conteo devuelto y latencia ms). Sin métricas custom nuevas (Free Tier 10/10 respetado).
- Ruta: `/api/v1/offers/search` registrada en `OfferEndpoints` antes de `/api/v1/offers/{id:guid}`.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Extracción masiva del catálogo con búsquedas vacías paginadas | Divulgación de información | `read-heavy`, `pageSize` ≤ 100 y radio ≤ 50 km (2 km invitados) | Bajo |
| Consultas de texto costosas o con caracteres de control / ReDoS | Denegación de servicio | Longitud 2..80 (o vacía), rechazo de control chars, `Regex.Escape(normalizedPrefix)` solo con prefijo `^` sin flag `i` sobre índice B-tree y acotado por `$geoWithin` | Bajo |
| Inyección de operadores en la consulta (`$where`, operadores `$`) | Manipulación | `q` pasa solo como string literal en `$text` y prefijo escapado literal en `titleNormalized`, nunca como expresión dinámica | Bajo |
| Ofertas no visibles o comercios inactivos expuestos | Divulgación de información | Filtro obligatorio (`Published`, vigente, `Stock > 0`, `merchant.activo == true`) en todos los canales de búsqueda | Bajo |
| Términos de búsqueda o coordenadas en logs | Divulgación de información | Logs estructurados solo con `correlation-id`, `qLength`, `isEmptyQuery`, `resultCount` y latencia | Bajo |

## 6. Observabilidad y rendimiento

- Logs estructurados con `correlation-id`, duración de consulta y conteo de resultados, manteniendo métricas Free Tier (`10/10`) sin nuevas métricas personalizadas en CloudWatch.
- Todas las consultas usan `txt_offers_spanish`, `ix_offers_title_normalized`, `(status, endsAt)` o `2dsphere`; `CancellationToken` propagado hasta MongoDB.

## 7. Costo AWS

Sin costo incremental: Lambda, API Gateway y logs dentro del Free Tier vigente.

## 8. Estrategia de pruebas

- Unitarias de `OfferTextNormalizer`, `OfferSearchRanker` (determinismo, pesos, prefijo >= 2 chars, coincidencia de categoría >= 90 %) y `SearchOffersQueryValidator`.
- Integración HTTP (`400`, `401`, `200` invitado recortado a `2 km`, `200` registrado hasta `50 km`, `429` rate limiting).
- Integración contra MongoDB real (`MONGO_CONNECTION_STRING` / Testcontainers): verificación de `txt_offers_spanish` (`default_language: "spanish"`), `ix_offers_title_normalized`, stemming español, prefijo 2+ caracteres, fallback vacío y exclusión de ofertas expiradas/pausadas/sin stock/comercios inactivos.

## 9. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-041-01`..`11` y `NFR-041-01`..`03` verificables; preguntas abiertas resueltas |
| II | PASS | Lógica pura en Domain, CQRS en Application, MongoDB en Infrastructure, endpoint en Api |
| III | PASS | `PagedResult<OfferSearchItemDto>`, sin entidades ni documentos expuestos |
| IV | PASS | Validación de entrada, recorte invitado 2 km, `ProblemDetails` sin detalles internos |
| V | PASS | Cobertura ≥ 80 % y pruebas reales contra MongoDB |
| VI | PASS | Sin recursos AWS nuevos; Free Tier respetado |
| VII | PASS | Índices declarados, `CancellationToken`, paginación 20/100; p95 queda en brecha `G-13` |
| VIII | PASS | STRIDE actualizado, allowlist, `read-heavy`, logs sin PII ni `q` |

> Brecha conocida aplicable al p95 en carga real: `G-13` (Apéndice F).

## 10. Orden de implementación

1. `T041-01` Actualizar y validar el contrato OpenAPI del spec (`contracts/openapi.yaml`)
2. `T041-02`/`T041-05` Domain: `OfferTextNormalizer`, `OfferSearchWeights`, `OfferSearchRanker` y puerto `IOfferRepository`
3. `T041-03`/`T041-06` Infrastructure: `titleNormalized`, `txt_offers_spanish` (`default_language: "spanish"`), `ix_offers_title_normalized` y consulta híbrida en `MongoOfferRepository`
4. `T041-02`/`T041-07`/`T041-11`/`T041-12` Application y Api: `SearchOffersQuery`, validador, `GET /api/v1/offers/search`, `OfferSearchSettings` y logging sin PII
5. `T041-08` Pruebas unitarias, HTTP y contra MongoDB real (`txt_offers_spanish`, prefijo, fallback, determinismo, categoría >= 90 %, invitado 2 km)
6. `T041-09`/`T041-10` Actualizar `specs/README.md`, `requirements.md` y `checklist.md`
