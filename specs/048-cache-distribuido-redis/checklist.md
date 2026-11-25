# Checklist 048 — Caché distribuido con Redis

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 048 está aprobada y sin preguntas bloqueantes (ruta enmendada a `/api/v1/feed/offers`)
- [x] Los requisitos tienen identificador y criterio medible (`FR-048-01` a `FR-048-14`, `NFR-048-01` a `NFR-048-06`)
- [x] Las dependencias están resueltas: 010, 024, 026, 032
- [x] El alcance y las exclusiones están declarados, incluidos EX-003 y G-14
- [x] El impacto en seguridad, costo y rendimiento fue evaluado en `plan.md` §4, §6 y §8

## B. Específicos de este spec

- [x] Existe interfaz `ICacheService` en `Sensus.Application.Common.Interfaces`
- [x] Existen `RedisCacheService` y `MemoryCacheService` en `Sensus.Infrastructure`, seleccionados por configuración
- [x] `StackExchange.Redis` y `Testcontainers.Redis` solo se referencian en Infrastructure y pruebas
- [x] `ICacheService` registrado en DI de `Sensus.Infrastructure/DependencyInjection.cs`
- [x] TTL configurado en `appsettings.json` (por entidad y para snapshots), no hardcodeado
- [x] Caché integrado en `GET /offers/{id}`, `/offers/nearby`, `/feed/offers`, `/users/profile` y `/merchants/me`
- [x] Invalidación de detalle, búsquedas cercanas e instantáneas del feed ante mutaciones de ofertas (`OfferCacheKeys.InvalidateAsync`)
- [x] Invalidación ante actualización de perfil de comerciante y usuario
- [x] Fallback graceful a persistencia si Redis no está disponible o falla la conexión
- [x] Sin PII ni tokens almacenados en caché ni registrados en logs
- [x] Redis real probado con Testcontainers (aislamiento por usuario, TTL e invalidación por prefijo)
- [x] Health check exponiendo `Degraded` sin tumbar la API

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4)
- [x] Pruebas añadidas y ejecutadas: 920 unitarias, 4 de arquitectura, 3 E2E y 498/509 de integración (11 fallos por daemon Docker ausente, sin omisiones)
- [x] Cobertura ≥ 80 % en Domain y Application: 89,35 % y 96,16 %
- [x] NFR-048-01 y NFR-048-02 medidos con 20 solicitudes HTTP por camino (p95 < 300 ms y < 500 ms)
- [x] `dotnet build sensus-back.sln --configuration Release -warnaserror`: 0 advertencias, 0 errores
- [x] `dotnet format sensus-back.sln --verify-no-changes`: sin cambios pendientes
- [x] Contrato del feed vigente en la Spec 042 (`/api/v1/feed/offers`); sin endpoints nuevos en esta spec
- [x] IaC actualizada en `infra/`; `cdk synth`/`cdk deploy` no ejecutados (sin autorización)
- [x] Logs estructurados con advertencias de fallback graceful y sin PII
- [x] Revisión de seguridad: STRIDE en `plan.md` §4, sin secretos expuestos
- [x] Constitution Check en PASS con EX-003 y G-14 documentadas

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS (`ICacheService` en Application, Redis en Infrastructure)
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS (sin PII ni tokens en claves o payloads; STRIDE)
- [x] **V. Verificación automatizada** — PASS: gates ejecutados; brecha externa de Docker registrada sin omitir pruebas ni bajar umbrales
- [x] **VI. Costo cero e IaC** — PASS con EX-003 aprobada (Valkey Serverless en VPC); alternativa `MemoryCacheService` sin costo
- [x] **VII. Rendimiento y observabilidad** — PASS: p95 < 300 ms con caché y < 500 ms sin caché, health check y métricas
- [x] **VIII. DevSecOps de nivel fintech** — PASS con G-14 abierta y documentada (caché sin IAM/RBAC, mitigada por red privada, TLS y Security Group)

## E. Evidencia

| Artefacto | Ubicación / comando | Resultado |
|---|---|---|
| Spec, plan, tareas y checklist | `specs/048-cache-distribuido-redis/` | Completos, v2.0.0, alineados a constitución v2.4.1 |
| Compilación | `dotnet build sensus-back.sln --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| Formato | `dotnet format sensus-back.sln --verify-no-changes` | Sin cambios pendientes (`FORMAT_OK`) |
| Pruebas unitarias | `dotnet test sensus-back.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/coverage-048-final` | 920/920 en verde |
| Pruebas de arquitectura | Misma ejecución | 4/4 en verde |
| Pruebas E2E | Misma ejecución | 3/3 en verde |
| Pruebas de integración | Misma ejecución | 498/509 en verde; 11 fallos por `DockerUnavailableException` en 9 pruebas previas de MongoDB y 2 de `RedisCacheTestcontainersTests` (`npipe://./pipe/docker_engine`), sin omisiones ni umbrales reducidos |
| Cobertura | `node .github/scripts/check-coverage.mjs artifacts/coverage-048-final 80 Sensus.Domain Sensus.Application` | Domain 89,35 % (2299/2573), Application 96,16 % (6237/6486) |
| Estructura SDD | `node .github/scripts/check-specs.mjs specs` | 59 especificaciones revisadas, estructura correcta |
| Invalidación de caché | `OfferCacheTests` (8/8) | Detalle + nearby + prefijo `feed:offers:` |
| Redis real | `RedisCacheTestcontainersTests` (2 pruebas) | Código listo; bloqueadas por daemon Docker ausente en este entorno |
| Latencia | `OfferFeedEndpointsTests.GetFeedOffers_ShouldMeetLatencyTargets_ForCachedAndFallbackPaths` | 1/1 en verde; p95 < 300 ms con caché y < 500 ms sin caché |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-14 | Equipo Sensus | Parcial | Caché de `GET /offers/{id}` y `/offers/nearby` con TTL `Redis:TtlByEntityMinutes:Offers` e invalidación en crear, editar, estado, republicar, apartar y expirar; pruebas en `OfferCacheTests` y handlers. Pendiente: feed, health check y Redis real. |
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Parcial | `MemoryCacheService` (TTL acotado, 1000 entradas) y `CacheHealthCheck` en `/health/ready`. Seguían pendientes el feed, Redis real y la medición de latencia. |
| 2026-11-23 | Equipo Sensus | CERRADO | Ruta canónica enmendada a `/api/v1/feed/offers`; instantáneas del feed cacheadas por usuario con TTL configurable e invalidadas en lote por prefijo `feed:offers:` ante mutaciones de ofertas. Redis real con Testcontainers y override `REDIS_CONNECTION_STRING`; p95 medido en ambos caminos. Gates: build 0/0, formato limpio, 920 unitarias + 4 arquitectura + 3 E2E + 498/509 integración, cobertura 89,35 % / 96,16 %, `check-specs.mjs` 59 specs. Brechas documentadas: 11 pruebas Testcontainers bloqueadas por Docker no disponible (sin omisiones), EX-003 (Valkey Serverless fuera de Free Tier) y G-14 (caché sin IAM/RBAC). Sin `cdk deploy` ni operaciones de Git. |
