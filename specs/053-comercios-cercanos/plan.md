# Plan 053 — Comercios cercanos

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec (documenta código existente) |
| Versión | 1.0.0 |
| Constitution Check | Pendiente de verificación en CI |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

`GetNearbyComerciosQuery` resuelve el radio con `GuestRadiusPolicy` y consulta `MongoComercioRepository.GetNearbyAsync`,
que filtra `activo = true` y usa `$geoNear` sobre `ubicacion` (índice `2dsphere`) para devolver el total y la página.

## 2. Cambios por capa

| Capa | Comportamiento de partida | Cambio |
|---|---|---|
| Domain | `GuestRadiusPolicy` | — |
| Application | `GetNearbyComerciosQuery` sin validador | Validador de coordenadas (FR-053-04) y `PagedResult` (FR-053-05) |
| Infrastructure | `$nearSphere` con tope fijo de 50 | `$geoNear` con página y conteo (FR-053-05) |
| Api | `GET /merchants/nearby` sin límite de tasa | Política `read-heavy` (FR-053-06). `radiusKm` prevalece sobre `radioMetros` y se aplica el mismo límite por rol (FR-053-07). |

## 3. Modelo de datos e índices

- `comercios.ubicacion` con índice `2dsphere` (ya declarado en `MongoDbContext`), más el filtro por `activo`.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Usuario suplanta una sesión o adquiere radio privilegiado | Suplantación / elevación de privilegios | JWT y radio según rol en el servidor | Bajo |
| Extracción masiva del directorio de comercios | Divulgación de información | DTO público, página acotada y límite de tasa (FR-053-06) | Bajo |
| Coordenadas o paginación abusivas | Denegación de servicio | Validación, radio máximo, índice y rate limit (FR-053-04, FR-053-05, FR-053-06) | Bajo |
| Invitado amplía el radio | Manipulación | Recorte en el servidor | Bajo |
| Solicitud inválida o limitada sin trazabilidad | Repudio | Los problemas de validación y tasa incluyen identificador de correlación | Bajo |

## 5. Observabilidad y rendimiento

- Consulta indexada; sin caché.

## 6. Costo AWS

Sin costo incremental.

## 7. Estrategia de pruebas

- Existentes: E2E de invitado y de comerciante.
- Integración HTTP para `200`, `400`, `401` y `429`, y MongoDB real para orden, selección y conteo.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | Retro-spec con requisitos medibles |
| II | PASS | Capas respetadas |
| III | PASS | Devuelve `ComercioDto` |
| IV | PASS | Validación de entrada y límite de tasa (FR-053-04, FR-053-06) |
| V | PASS local | Pruebas de integración HTTP y con MongoDB |
| VI | PASS | Sin costo |
| VII | PASS | Paginación indexada y metadatos (FR-053-05) |
| VIII | Pendiente | STRIDE y controles del endpoint revisados. Faltan los escaneos bloqueantes de CI. |


## 9. Riesgos de implementación

- Paginar cambia la forma de la respuesta: la app móvil debe adaptarse (lista → `items` con metadatos).

## 10. Orden de implementación

1. `T053-01` Documentar el comportamiento existente
2. `T053-02` Contrato OpenAPI
3. `T053-03` Validación de coordenadas
4. `T053-04` Paginación
5. `T053-05` Límite de tasa
6. `T053-06` Pruebas de integración
7. `T053-07` Parámetro `radiusKm` con alias

El responsable confirmó el cambio de contrato de `T053-04` el 2026-11-22. MongoDB agrega con `$geoNear` sobre el índice `2dsphere`, filtra comercios activos y ordena por distancia e `_id` para desempatar. Un `$facet` devuelve la página y el total en la misma consulta. `page` empieza en 1 y `pageSize` acepta de 1 a 100, con 20 por defecto.
