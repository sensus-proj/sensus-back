# Spec 048 — Caché distribuido con Redis

| Campo | Valor |
|---|---|
| ID | 048 |
| Slug | `cache-distribuido-redis` |
| Estado | Cerrado |
| Ola | 7 |
| Tipo | Nuevo |
| Dueño | Equipo Sensus |
| Dependencias | 010, 024, 026, 032 |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Contexto y problema

El backend Sensus necesita un sistema de caché distribuido para lecturas recurrentes que mejore el rendimiento de la API y reduzca la carga sobre MongoDB. Actualmente `PagedResult` existe en `Sensus.Application.Common.Models` pero no se usa en ninguna consulta expuesta. La constitución §6 exige que las lecturas recurrentes usen caché con TTL explícito e invalidación documentada. El profesor requiere caché a nivel de base de datos, backend y frontend, con uso distribuido de Redis (v2).

## 2. Alcance

**Incluye**

- Integración con Redis como caché distribuido para lecturas recurrentes: detalle y búsqueda de ofertas, instantáneas del feed de promociones, afinidad del feed y perfiles de usuario y comerciante. Recomendaciones, configuración de moderación y tokens revocados quedan fuera de esta versión porque ya usan otros mecanismos (Specs 043, 021 y 004 respectivamente)
- Interfaz `ICacheService` en `Sensus.Application.Common.Interfaces` con implementación en `Sensus.Infrastructure`
- TTL explícito configurable por entidad/clave, nunca hardcodeado
- Invalidación documentada y evicción ante cambios (creación, actualización, eliminación de ofertas, publicaciones y perfil de comerciante). Toda mutación de oferta invalida en lote las instantáneas del feed mediante el prefijo `feed:offers:`
- Uso de caché en endpoints de lectura: `GET /api/v1/offers/{id}`, `GET /api/v1/offers/nearby`, `GET /api/v1/feed/offers` (ruta canónica del feed de promociones, antes descrita como `/feed/nearby`), `GET /api/v1/merchants/me`, `GET /api/v1/users/profile`
- Límites de tamaño y política de evicción del proveedor en memoria de proceso (`Cache:Memory:MaxEntries`, TTL acotado a 60 s)
- Salud del caché (health check) integrado con spec 016: `CacheHealthCheck` reporta `Degraded`, nunca `Unhealthy`
- Fallo graceful: si Redis no está disponible, la API consulta MongoDB directamente (no falla la operación)
- Redis real en pruebas de integración con Testcontainers (sin dobles que simulen el motor), con override opcional `REDIS_CONNECTION_STRING`
- Caché Valkey Serverless en VPC con TLS y NAT administrado definido en `infra/` (EX-003); sin `cdk deploy` ejecutado en esta sesión

**No incluye**

- Caché del frontend (móvil/web) — fuera del alcance de este repositorio backend
- Caché de escritura (solo lecturas)
- Redis como almacén primario de datos (solo caché)
- Pub/Sub de Redis para sincronización entre instancias (se usa invalidación por TTL y expiración)

## 3. Actores

- Cliente de la API (recibe respuestas desde caché)
- Operador (monitorea salud y métricas del caché)
- Sistema (invalida claves ante cambios de datos)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-048-01 | Definir interfaz `ICacheService` con métodos `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync` | Must |
| FR-048-02 | Implementar `RedisCacheService` en `Sensus.Infrastructure` usando `StackExchange.Redis` | Must |
| FR-048-03 | Registrar `ICacheService` en el contenedor de DI de `Sensus.Api/Program.cs` | Must |
| FR-048-04 | Cachear respuestas de `GET /offers/{id}` con TTL configurable | Must |
| FR-048-05 | Cachear respuestas de `GET /offers/nearby` con TTL configurable | Must |
| FR-048-06 | Cachear instantáneas de `GET /api/v1/feed/offers` con TTL configurable (`FeedRanking:SnapshotTtlMinutes`) | Must |
| FR-048-07 | Invalidar caché de oferta al crear, actualizar o eliminar una oferta | Must |
| FR-048-08 | Invalidar todas las instantáneas del feed de ofertas ante mutaciones de ofertas y publicaciones (prefijo `feed:offers:`) | Must |
| FR-048-09 | Invalidar caché de comercio al actualizar el perfil del comerciante | Must |
| FR-048-10 | Configurar TTL por entidad en `appsettings.json` (no hardcodeado) | Must |
| FR-048-11 | Devolver datos desde MongoDB si Redis no está disponible (falso graceful) | Must |
| FR-048-12 | Health check del caché integrado con `/health/ready` | Should |
| FR-048-14 | Activar Redis en producción con ElastiCache Serverless (Valkey) en VPC, TLS y acceso solo desde la API, definido en `infra/` | Must |
| FR-048-13 | Usar una caché en memoria de proceso, sin costo, cuando Redis está apagado o sin cadena de conexión (despliegue en Lambda sin ElastiCache) | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-048-01 | Latencia de lectura con caché (instantánea del feed) | p95 < 300 ms, medido con 20 solicitudes HTTP en `OfferFeedEndpointsTests.GetFeedOffers_ShouldMeetLatencyTargets_ForCachedAndFallbackPaths` |
| NFR-048-02 | Latencia de lectura sin caché (fallback MongoDB) | p95 < 500 ms, medido con 20 solicitudes HTTP en la misma prueba simulando `IFeedSnapshotStore` no disponible |
| NFR-048-03 | TTL mínimo | Configurable, por entidad |
| NFR-048-04 | Disponibilidad del servicio ante caída de Redis | 100 % (fallback a MongoDB) |
| NFR-048-05 | Costo mensual | Dentro del Free Tier de AWS (ElastiCache o similar) |
| NFR-048-06 | Desactualización entre instancias con la caché en memoria | ≤ `Cache:Memory:MaxTtlSeconds` (60 s por defecto), porque cada Lambda tiene su caché y la invalidación es local |

## 6. Criterios de aceptación

### CA-048-01: Interfaz de caché
- **Dado**: la especificación 048
- **Cuando**: se inspecciona `Sensus.Application.Common.Interfaces`
- **Entonces**: existe `ICacheService` con `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`, `ExistsAsync`

### CA-048-02: Implementación Redis
- **Dado**: una solicitud de lectura a `GET /offers/{id}`
- **Cuando**: el dato está en caché
- **Entonces**: se devuelve desde Redis sin consultar MongoDB

### CA-048-03: Invalidación al modificar oferta
- **Dado**: una oferta existe en caché
- **Cuando**: se crea, actualiza o elimina esa oferta
- **Entonces**: la clave de caché correspondiente se elimina

### CA-048-04: Fallo graceful
- **Dado**: Redis no está disponible
- **Cuando**: se solicita un dato que normalmente estaría en caché
- **Entonces**: la API consulta MongoDB y devuelve el dato sin error 5xx

### CA-048-05: TTL en configuración
- **Dado**: `appsettings.json`
- **Cuando**: se inspecciona la configuración de caché
- **Entonces**: los TTL están definidos en configuración, no hardcodeados en código

### CA-048-07: Caché en memoria cuando Redis está apagado
- **Dado**: `Redis:Enabled=false` o sin cadena de conexión (como en el despliegue en Lambda)
- **Cuando**: se repite una lectura cacheable dentro del TTL
- **Entonces**: la segunda lectura sale de la caché en memoria sin consultar MongoDB, y tras `MaxTtlSeconds` se vuelve a consultar

### CA-048-06: Health check
- **Dado**: el endpoint `/health/ready`
- **Cuando**: se consulta el estado del sistema
- **Entonces**: incluye el estado del caché Redis

### CA-048-08: Latencia con y sin caché (NFR-048-01/02)
- **Dado**: el endpoint `GET /api/v1/feed/offers` autenticado
- **Cuando**: se miden 20 solicitudes con `snapshotId` válido y 20 solicitudes con el almacén de instantáneas no disponible
- **Entonces**: el p95 es menor de 300 ms en el primer caso y menor de 500 ms en el segundo

## 7. Contratos

No expone nuevos endpoints públicos; mejora el rendimiento de endpoints existentes. La ruta canónica del feed de promociones es `GET /api/v1/feed/offers` (Spec 042); no existe `/api/v1/feed/nearby`. El parámetro opcional `snapshotId` devuelve una página estable dentro del TTL.

## 8. Exclusiones

- No incluye caché del frontend (móvil/web).
- No incluye caché de escritura.
- No incluye Redis como base de datos primaria.
- No incluye pub/sub de Redis para invalidación en tiempo real entre instancias.

## 9. Estado de cierre y brechas

- **EX-003 (constitución v2.4.1)**: ElastiCache Serverless Valkey en VPC con NAT administrado queda fuera del Free Tier; la justificación y el techo de costo están registrados en el Apéndice E y en `plan.md` §6. La infraestructura existe en `infra/`; `cdk synth`/`cdk deploy` no se ejecutaron en esta sesión (sin autorización).
- **G-14**: la caché Valkey no usa autenticación IAM/RBAC; el acceso se restringe por Security Group y TLS. La brecha sigue abierta y se mantiene documentada en el Apéndice F de la constitución.
- **Brecha externa de entorno**: 11 pruebas Testcontainers (9 previas de MongoDB y 2 nuevas de Redis) no pueden ejecutarse porque el daemon Docker no está disponible en este entorno (`npipe://./pipe/docker_engine`). No se omitieron ni se bajaron umbrales; el código y la configuración de las pruebas quedan listos para ejecutarse en CI con Docker.
- **Sin secretos nuevos**: la cadena de conexión de Redis se publica como configuración de Lambda desde CDK, sin llaves de acceso en el repositorio.

---
