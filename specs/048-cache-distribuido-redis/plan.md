# Plan 048 — Caché distribuido con Redis

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (Cerrado) |
| Versión | 2.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Resumen técnico

Caché distribuido con Redis para lecturas recurrentes del backend Sensus. `ICacheService` vive en Application; `RedisCacheService` (StackExchange.Redis) y `MemoryCacheService` (proceso, sin costo) viven en Infrastructure y se seleccionan por configuración. La composición ocurre solo en `Sensus.Api/Program.cs`. Las instantáneas ordenadas del feed de promociones se guardan en Redis mediante `IFeedSnapshotStore`/`RedisFeedSnapshotStore`; `GET /api/v1/feed/offers` las consume para paginación estable dentro del TTL. Toda mutación de oferta invalida detalle, búsquedas cercanas e instantáneas del feed en lote por prefijo. Si Redis falla, la operación continúa contra MongoDB y `CacheHealthCheck` reporta `Degraded`, nunca `Unhealthy`.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Sin cambios (el caché es una preocupación de infraestructura/aplicación) |
| Application | `ICacheService`, `IFeedSnapshotStore`, `FeedSnapshot`, claves `OfferCacheKeys`/`FeedCacheKeys`, integración en handlers de lectura e invalidación en handlers de escritura |
| Infrastructure | `RedisCacheService`, `MemoryCacheService`, `RedisFeedSnapshotStore`, `CacheHealthCheck`; paquetes `StackExchange.Redis` (y `Testcontainers.Redis` solo en pruebas) |
| Api | Registro en DI, integración en `GET /offers/{id}`, `/offers/nearby`, `/feed/offers`, `/merchants/me` y `/users/profile`; health check en `/health/ready` |
| IaC | VPC, Valkey Serverless, NAT y Lambda en subredes privadas (`SensusInfraStack.CacheAndSchedule.cs`, EX-003) |

## 3. Modelo de datos e índices

No aplica: Redis es un almacén clave-valor en memoria, no requiere índices declarados como MongoDB. Claves: `sensus:offers:detail:{id}`, `sensus:offers:nearby:{parámetros}`, `feed:affinity:{userId}`, `feed:offers:{userId}:{snapshotId}` y claves de perfil. TTL por entidad en `Redis:TtlByEntityMinutes`; TTL del snapshot en `FeedRanking:SnapshotTtlMinutes`; tope en `Cache:Memory:MaxTtlSeconds`. `FeedCacheKeys.SnapshotPrefix = "feed:offers:"` permite invalidar todas las instantáneas en lote.

## 4. Seguridad y modelo de amenazas (STRIDE)

| Amenaza | Control |
|---|---|
| Spoofing | Cada instantánea se clave por `userId` del token; `RedisCacheTestcontainersTests` verifica que un `snapshotId` no puede ser leído por otro usuario. `GET /feed/offers` exige `RegisteredUserPolicy` |
| Tampering | Payloads serializados con `System.Text.Json`; la caché en memoria devuelve copias inmutables; las Instantáneas se reconstruyen desde MongoDB en cada página |
| Repudiation | Sin mutaciones de negocio en caché; no aplica auditoría adicional |
| Information disclosure | Sin PII, tokens ni secretos en claves o payloads; la cadena de conexión se inyecta por configuración desde CDK, nunca en el repositorio |
| Denial of service | LRU con `MaxEntries`, TTL acotado, rate limit `ReadHeavyPolicy` en el feed y fallback graceful si Redis no responde |
| Elevation of privilege | Sin permisos en Redis: acceso por Security Group y TLS; brecha G-14 (sin IAM/RBAC) documentada en el Apéndice F |

## 5. Observabilidad y rendimiento

- `CacheHealthCheck` integrado en `/health/ready`; degrade a `Degraded` sin tumbar la API.
- Métricas del feed con `isSnapshotHit`, `cacheDegraded` y latencia emitidas por `IFeedMetricsPublisher` (Spec 055).
- NFR-048-01 y NFR-048-02 medidos en `OfferFeedEndpointsTests.GetFeedOffers_ShouldMeetLatencyTargets_ForCachedAndFallbackPaths`: 20 solicitudes por camino, p95 < 300 ms con caché y p95 < 500 ms sin caché.
- TTL y límite de entradas en configuración, no hardcodeados.

## 6. Costo AWS

ElastiCache Serverless Valkey en VPC con NAT administrado no cabe en el nivel gratuito: se acoge a **EX-003** y su costo estimado queda registrado en el Apéndice E de la constitución. Alternativa sin costo (`MemoryCacheService`) disponible cuando `Redis:Enabled=false` o no hay cadena de conexión; TTL acotado a 60 s para limitar la desactualización entre instancias Lambda (NFR-048-06). Todo recurso está en `infra/` y etiquetado con `project`, `environment` y `owner`. No se ejecutó `cdk synth` ni `cdk deploy` en esta sesión.

## 7. Estrategia de pruebas

- Unitarias: `RedisCacheServiceTests` con dobles de `IDatabase`, `OfferCacheTests` y `MemoryCacheServiceTests` (TTL, LRU, proveedor y health check).
- Integración: `RedisCacheTestcontainersTests` arranca Redis real con Testcontainers o usa `REDIS_CONNECTION_STRING`; verifica round-trip, TTL, aislamiento por usuario e invalidación por prefijo. Sin dobles que simulen el motor.
- Integración HTTP: `OfferFeedEndpointsTests` cubre snapshot estable, parámetros inválidos, 401/403, 429 y latencia p95 de ambos caminos.
- Fallo graceful: pruebas con Redis deshabilitado, con excepciones de `StackExchange.Redis` y con `IFeedSnapshotStore` no disponible.
- Brecha de entorno: 11 pruebas Testcontainers no ejecutables por daemon Docker ausente; no omitidas.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Cuatro artefactos v2.0.0, `FR-048-01` a `FR-048-14` y `NFR-048-01` a `NFR-048-06` con criterios medibles y trazabilidad en `requirements.md` |
| II. Clean Architecture | PASS | `ICacheService` en Application; Redis solo en Infrastructure; composición en Api; test de arquitectura 4/4 |
| III. CQRS y contratos | PASS | Sin entidades de dominio en respuestas; snapshots con DTOs; `/api/v1/feed/offers` documentado en Spec 042 |
| IV. Seguridad y privacidad | PASS | STRIDE en §4, sin PII/secretos en caché o logs, TLS y Security Group, `RegisteredUserPolicy` |
| V. Verificación automatizada | PASS | 920 unitarias + 4 arquitectura + 3 E2E + 498/509 integración; cobertura Domain 89,35 % y Application 96,16 %; 11 pruebas bloqueadas por Docker ajeno al código |
| VI. Costo cero e IaC | PASS (brecha EX-003 aprobada) | Valkey Serverless en `infra/`, alternativa `MemoryCacheService` sin costo; sin recursos creados fuera de IaC ni `cdk deploy` |
| VII. Rendimiento y observabilidad | PASS | p95 < 300 ms con caché y < 500 ms sin caché, health check y métricas de snapshot |
| VIII. DevSecOps de nivel fintech | PASS (brecha G-14 documentada) | Sin secretos ni PII, validación con lista de permitidos, auditoría redactada, escaneos bloqueantes del pipeline; G-14 (caché sin IAM/RBAC) abierta en Apéndice F |

## 9. Riesgos de implementación

- EX-003: Valkey Serverless tiene costo fuera del Free Tier; compensado por el techo presupuestario y la alternativa en memoria.
- La invalidación por TTL puede causar lecturas de datos obsoletos por un corto período en la caché en memoria; acotada a 60 s (NFR-048-06).
- StackExchange.Redis y Testcontainers.Redis 4.16.0 son dependencias externas con licencia permissiva; evaluadas en el pipeline de SCA.
- G-14: la caché no usa IAM/RBAC; mitigada por red privada, TLS y Security Group.

---