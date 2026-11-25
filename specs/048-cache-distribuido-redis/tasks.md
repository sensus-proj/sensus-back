# Tareas 048 — Caché distribuido con Redis

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Cerrado |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T048-01 | Crear interfaz `ICacheService` en `Sensus.Application.Common.Interfaces` | Application | `FR-048-01` | — | Completada |
| T048-02 | Añadir paquete `StackExchange.Redis` a `Sensus.Infrastructure` | Infrastructure | `FR-048-02` | T048-01 | Completada |
| T048-03 | Implementar `RedisCacheService` en `Sensus.Infrastructure` | Infrastructure | `FR-048-02` | T048-02 | Completada |
| T048-04 | Registrar `ICacheService` en DI de `Sensus.Infrastructure/DependencyInjection.cs` | Api | `FR-048-03` | T048-03 | Completada |
| T048-05 | Configurar TTL por entidad en `appsettings.json` | Api | `FR-048-10` | T048-04 | Completada |
| T048-06 | Integrar caché en `GET /offers/{id}` | Api | `FR-048-04` | T048-04 | Completada |
| T048-07 | Integrar caché en `GET /offers/nearby` | Api | `FR-048-05` | T048-04 | Completada |
| T048-08 | Integrar caché de instantáneas en `GET /api/v1/feed/offers` | Api | `FR-048-06` | T048-04 | Completada |
| T048-09 | Integrar caché en `GET /merchants/me` / `/api/v1/comercios/me` | Api | `FR-048-04` | T048-04 | Completada |
| T048-10 | Integrar caché en `GET /users/profile` | Api | `FR-048-04` | T048-04 | Completada |
| T048-11 | Implementar invalidación de caché al crear/actualizar/eliminar oferta | Infrastructure | `FR-048-07` | T048-03 | Completada |
| T048-12 | Invalidar instantáneas del feed en lote por prefijo `feed:offers:` ante mutaciones de ofertas y publicaciones | Infrastructure | `FR-048-08` | T048-03 | Completada |
| T048-13 | Implementar invalidación de caché al actualizar perfil de comerciante y usuario | Infrastructure | `FR-048-09` | T048-03 | Completada |
| T048-14 | Implementar fallback graceful a MongoDB si Redis no está disponible | Infrastructure | `FR-048-11` | T048-03 | Completada |
| T048-15 | Integrar health check del caché en `/health/ready` (`CacheHealthCheck`: `Degraded`, nunca `Unhealthy`) | Api | `FR-048-12` | T048-04 | Completada |
| T048-16 | Pruebas unitarias de `RedisCacheService` y Handlers con caché | Pruebas | `FR-048-01` | T048-03 | Completada |
| T048-17 | Pruebas de integración con Redis real mediante Testcontainers (`REDIS_CONNECTION_STRING` opcional) | Pruebas | `FR-048-02` | T048-03 | Completada |
| T048-18 | Prueba de fallo graceful (Redis caído → MongoDB sin error 5xx) | Pruebas | `FR-048-11` | T048-14 | Completada |
| T048-19 | Prueba de invalidación de caché al modificar oferta | Pruebas | `FR-048-07` | T048-11 | Completada |
| T048-20 | Actualizar `specs/README.md`, `requirements.md` y checklist del spec | Docs | `FR-048-01` | T048-21 | Completada |
| T048-21 | Registrar evidencias del Constitution Check | Docs | `FR-048-01` | T048-16 | Completada |
| T048-22 | Implementar `MemoryCacheService` (TTL acotado, tope de entradas, JSON inmutable) y seleccionar proveedor por configuración | Infrastructure | `FR-048-13` | T048-03 | Completada |
| T048-23 | Pruebas de `MemoryCacheService`, selección de proveedor y `CacheHealthCheck` | Pruebas | `FR-048-13` | T048-22 | Completada |
| T048-24 | Aprovisionar VPC, Valkey Serverless y Lambda en VPC con CDK (`SensusInfraStack.CacheAndSchedule.cs`); `Redis__Enabled=true` | IaC | `FR-048-14` | T048-22 | Hecho (pendiente de despliegue por CI; sin `cdk deploy` en esta sesión) |
| T048-25 | Medir p95 con caché y sin caché en `/api/v1/feed/offers` (20 solicitudes por camino) | Pruebas | `NFR-048-01`, `NFR-048-02` | T048-08 | Completada |
| T048-26 | Cierre documental con evidencia de gates, EX-003, G-14 y brecha Docker | Docs | `FR-048-01` | T048-20, T048-25 | Completada |

## Notas de ejecución

- Cada tarea se cerró actualizando `checklist.md` y `plan.md` cuando cambió el diseño.
- Ninguna tarea se consideró terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- `StackExchange.Redis` y `Testcontainers.Redis` se añadieron solo a `Sensus.Infrastructure` y `Sensus.IntegrationTests`; `Domain` y `Application` no referencian `Infrastructure`.
- La ruta canónica del feed es `/api/v1/feed/offers`; la denominación histórica `/feed/nearby` quedó enmendada en la spec 048 v2.0.0.
- 11 pruebas Testcontainers siguen bloqueadas por daemon Docker no disponible (`npipe://./pipe/docker_engine`): no se omitieron ni se bajaron umbrales.
