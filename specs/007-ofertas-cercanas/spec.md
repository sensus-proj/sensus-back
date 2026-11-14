# Spec 007 — Ofertas cercanas

| Campo | Valor |
|---|---|
| ID | 007 |
| Slug | `ofertas-cercanas` |
| Estado | Implementado |
| Ola | 2 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 005, 010, 034 |
| Versión | 1.1.0 |
| Alineado a | `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0 |

> **v1.1.0** — Alineación documental con el backlog de la escuela (SE-008/011/012/013/016): se añaden
> `FR-007-08` y `FR-007-09` y las secciones de equivalencia SQL Server → MongoDB y de mapeo SE → FR.
> No se modifican los requisitos existentes.

---

## 1. Contexto y problema

Permitir que un usuario descubra ofertas activas cerca de su ubicación, con filtros por radio y categoría, resultados paginados y ordenados por distancia.

## 2. Alcance

**Incluye**

- Consulta geoespacial de ofertas publicadas
- Filtro por radio y categoría
- Exclusión de ofertas vencidas o sin stock
- Paginación estándar
- Distancia calculada en la respuesta

**No incluye**

- Búsqueda por texto (spec 041)
- Ranking personalizado (spec 042)
- Ofertas de comercios inactivos

## 3. Actores

- Usuario autenticado (radio de 1 a 5 km)
- Usuario invitado (radio máximo estricto de 2 km, SE-013)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-007-01 | Listar ofertas publicadas dentro de un radio de la ubicación enviada | Must |
| FR-007-02 | Excluir ofertas vencidas, pausadas, archivadas o sin stock | Must |
| FR-007-03 | Filtrar por categoría del catálogo | Should |
| FR-007-04 | Aplicar pageSize 20 por defecto y 100 máximo | Must |
| FR-007-05 | Ordenar por distancia ascendente y desempatar por fecha de publicación | Must |
| FR-007-06 | Limitar el radio a 50 km y rechazar valores fuera de rango | Must |
| FR-007-07 | Devolver la distancia en metros junto a cada oferta | Should |
| FR-007-08 | Aceptar un radio de 1 a 5 km para el usuario registrado y rechazar valores fuera de ese rango | Must |
| FR-007-09 | Aplicar un radio máximo estricto de 2 km para el usuario invitado, sin importar el valor solicitado | Must |

> **Relación entre `FR-007-06`, `FR-007-08` y `FR-007-09`:** `FR-007-06` es el **tope técnico de
> rechazo** (defensa en profundidad, cualquier valor > 50 km se rechaza). `FR-007-08` fija el **rango
> de producto** para el usuario registrado (1–5 km, alineado al slider de SE-012) y `FR-007-09` el
> **límite de invitado** (2 km estrictos, SE-013). Los tres se aplican en ese orden.

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-007-01 | Latencia del listado | p95 < 500 ms |
| NFR-007-02 | Consulta geoespacial | Sin recorrido completo de colección (índice 2dsphere) |

## 6. Criterios de aceptación

### CA-007-01

- **Dado** parámetros válidos
- **Cuando** GET /api/v1/offers/nearby?lat=-12.05&lng=-77.04&radiusKm=5
- **Entonces** 200 con metadatos de paginación y distancia

### CA-007-02

- **Dado** pageSize mayor a 100
- **Cuando** se envía la petición
- **Entonces** 400 con el límite documentado

### CA-007-03

- **Dado** oferta vencida dentro del radio
- **Cuando** se consulta
- **Entonces** no aparece en el resultado

### CA-007-04

- **Dado** radio de 80 km
- **Cuando** se consulta
- **Entonces** 400 por límite excedido

### CA-007-05

- **Dado** un usuario registrado y `radiusKm=3`
- **Cuando** se consulta
- **Entonces** 200 con resultados dentro de 3 km
- **Y** `radiusKm=0.5` o `radiusKm=6` devuelven 400

### CA-007-06

- **Dado** un usuario invitado y `radiusKm=5`
- **Cuando** se consulta
- **Entonces** 200 con resultados limitados a 2 km como máximo

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| GET | `/api/v1/offers/nearby` | JWT |

> **Ruta canónica:** `/api/v1/offers/nearby`. El catálogo de endpoints de la escuela referencia
> `/api/offers/nearby`; se documenta como **alias conceptual** y no se expone en código por ahora
> (AGENTS.md §15 exige el prefijo versionado `/api/v1`).

## 8. Equivalencia SQL Server → MongoDB

El backlog de la escuela describe la consulta con SQL Server (`GEOGRAPHY` y `STDistance()`). Sensus
Backend usa **MongoDB** por mandato de `constitution.md`, por lo que el comportamiento se implementa
con operadores geoespaciales equivalentes:

| Concepto | SQL Server (documento de la escuela) | MongoDB (Sensus) |
|---|---|---|
| Tipo de dato espacial | `GEOGRAPHY` / `geometry` | GeoJSON `Point`: `{ "type": "Point", "coordinates": [lng, lat] }` |
| Índice espacial | Índice `SPATIAL` | Índice `2dsphere` sobre el campo GeoJSON |
| Filtro por radio | `WHERE ubicacion.STDistance(@punto) < @radio` | `$geoNear` con `maxDistance`, o `$near` / `$nearSphere` |
| Distancia devuelta | `ubicacion.STDistance(@punto)` como columna | `$geoNear` con `distanceField` (metros) |
| Radio en unidad geográfica | metros | `maxDistance` en metros; `$centerSphere` requiere radianes (`km / 6378.1`) |
| Validación de rango | `CHECK` / validación en aplicación | Validación de borde en Application (FluentValidation) |

Notas técnicas:

- `NearSphere` (usado hoy en `MongoComercioRepository.GetNearbyAsync`) filtra por distancia pero **no**
  devuelve la distancia. `FR-007-07` exige devolverla, por lo que la colección `offers` debe
  consultarse con una agregación `$geoNear` y `distanceField`.
- El campo `location` se almacena como `GeoJsonPoint<GeoJson2DGeographicCoordinates>` y el índice se
  declara en `MongoDbContext` (patrón ya aplicado a `ComercioDocument.Ubicacion`).
- La equivalencia es **funcional**, no sintáctica: no se usa SQL, ORM relacional ni migraciones SQL
  (AGENTS.md §12).

## 9. Mapeo con el backlog de la escuela (SE-###)

| ID escuela | Historia / criterio | Requisito del repo |
|---|---|---|
| SE-008 | Búsqueda de comercios cercanos; invitado limitado a 2 km | `FR-007-09` (radio estricto de invitado) |
| SE-011 | Consulta de ofertas cercanas con funciones espaciales en BD | `FR-007-01`, `NFR-007-02` |
| SE-012 | Radio ajustable de 1 a 5 km para usuario registrado | `FR-007-08` |
| SE-013 | Restricción de 2 km para invitado | `FR-007-09` |
| SE-016 | Orden por cercanía y visualización de distancia | `FR-007-05`, `FR-007-07` |

> Los IDs `SE-###` provienen de los documentos de planificación de la escuela y se registran aquí
> únicamente como trazabilidad externa; el identificador normativo sigue siendo `FR-007-nn`.

## 10. Dependencias y supuestos

- Depende de: 005, 010, 034
- La colección `offers` **aún no existe** en el código; su creación corresponde a la spec 034.
  Mientras no exista, este spec no es implementable y `GET /api/v1/offers/nearby` no se expone.
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 11. Riesgos

- Consultas geoespaciales sin índice degradan la latencia
- Un radio grande multiplica resultados y costo de lectura

## 12. Preguntas abiertas

- ¿El listado requiere ubicación obligatoria o admite una ubicación por defecto?

## 13. Trazabilidad

- Requisitos de este spec: `FR-007-01`, `FR-007-02`, `FR-007-03`, `FR-007-04`, `FR-007-05`, `FR-007-06`, `FR-007-07`, `FR-007-08`, `FR-007-09`
- Mapeo externo: `SE-008`, `SE-011`, `SE-012`, `SE-013`, `SE-016` (§9)
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
