# Plan 053 — Comercios cercanos

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec (documenta código existente) |
| Versión | 1.0.0 |
| Constitution Check | FAIL (retro-spec con brechas registradas) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

`GetNearbyComerciosQuery` resuelve el radio con `GuestRadiusPolicy` y consulta `MongoComercioRepository.GetNearbyAsync`,
que filtra `activo = true` y usa `$nearSphere` sobre `ubicacion` (índice `2dsphere`) con `Limit(50)`.

## 2. Cambios por capa

| Capa | Estado actual | Pendiente |
|---|---|---|
| Domain | `GuestRadiusPolicy` | — |
| Application | `GetNearbyComerciosQuery` sin validador | Validador de coordenadas (FR-053-04) y `PagedResult` (FR-053-05) |
| Infrastructure | `$nearSphere` con tope fijo de 50 | `Skip`/`Limit` y conteo (FR-053-05) |
| Api | `GET /merchants/nearby` sin límite de tasa | Política `read-heavy` (FR-053-06) y alias de `radiusKm` (FR-053-07) |

## 3. Modelo de datos e índices

- `comercios.ubicacion` con índice `2dsphere` (ya declarado en `MongoDbContext`), más el filtro por `activo`.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Extracción masiva del directorio de comercios | Divulgación de información | Tope de 50 por llamada; falta límite de tasa (FR-053-06) | Medio |
| Coordenadas inválidas provocan errores del motor | Denegación de servicio | Falta validación (FR-053-04) | Medio |
| Invitado amplía el radio | Manipulación | Recorte en el servidor | Bajo |

## 5. Observabilidad y rendimiento

- Consulta indexada; sin caché.

## 6. Costo AWS

Sin costo incremental.

## 7. Estrategia de pruebas

- Existentes: E2E de invitado y de comerciante.
- Pendientes: integración con éxito, `400` por coordenadas, `401` sin token, paginación y `429`.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | Retro-spec con requisitos medibles |
| II | PASS | Capas respetadas |
| III | PASS | Devuelve `ComercioDto` |
| IV | FAIL | Sin validación de entrada ni límite de tasa (FR-053-04, FR-053-06) |
| V | FAIL | Sin pruebas de integración propias del endpoint |
| VI | PASS | Sin costo |
| VII | FAIL | Colección sin paginación (FR-053-05) |
| VIII | FAIL | Validación con lista de permitidos ausente (regla 3) |

> Retro-spec: las brechas quedan como deuda con tarea y fecha (§3.4 de la constitución).

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
