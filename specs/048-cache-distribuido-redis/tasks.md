# Tareas 048 — Caché distribuido con Redis

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | En progreso (Core implementado) |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T048-01 | Crear interfaz `ICacheService` en `Sensus.Application.Common.Interfaces` | Application | `FR-048-01` | — | Completada |
| T048-02 | Añadir paquete `StackExchange.Redis` a `Sensus.Infrastructure` | Infrastructure | `FR-048-02` | T048-01 | Completada |
| T048-03 | Implementar `RedisCacheService` en `Sensus.Infrastructure` | Infrastructure | `FR-048-02` | T048-02 | Completada |
| T048-04 | Registrar `ICacheService` en DI de `Sensus.Infrastructure/DependencyInjection.cs` | Api | `FR-048-03` | T048-03 | Completada |
| T048-05 | Configurar TTL por entidad en `appsettings.json` | Api | `FR-048-10` | T048-04 | Completada |
| T048-06 | Integrar caché en `GET /offers/{id}` | Api | `FR-048-04` | T048-04 | Pendiente (Ola 2) |
| T048-07 | Integrar caché en `GET /offers/nearby` | Api | `FR-048-05` | T048-04 | Pendiente (Ola 2) |
| T048-08 | Integrar caché en `GET /feed/nearby` | Api | `FR-048-06` | T048-04 | Pendiente (Ola 3) |
| T048-09 | Integrar caché en `GET /merchants/me` / `/api/v1/comercios/me` | Api | `FR-048-04` | T048-04 | Completada |
| T048-10 | Integrar caché en `GET /users/profile` | Api | `FR-048-04` | T048-04 | Completada |
| T048-11 | Implementar invalidación de caché al crear/actualizar/eliminar oferta | Infrastructure | `FR-048-07` | T048-03 | Pendiente (Ola 2) |
| T048-12 | Implementar invalidación de caché al crear/actualizar/eliminar publicación | Infrastructure | `FR-048-08` | T048-03 | Pendiente (Ola 3) |
| T048-13 | Implementar invalidación de caché al actualizar perfil de comerciante y usuario | Infrastructure | `FR-048-09` | T048-03 | Completada |
| T048-14 | Implementar fallback graceful a MongoDB si Redis no está disponible | Infrastructure | `FR-048-11` | T048-03 | Completada |
| T048-15 | Integrar health check del caché en `/health` | Api | `FR-048-12` | T048-04 | Pendiente |
| T048-16 | Pruebas unitarias de `RedisCacheService` y Handlers con caché | Pruebas | `FR-048-01` | T048-03 | Completada |
| T048-17 | Pruebas de integración con Redis real | Pruebas | `FR-048-02` | T048-03 | Pendiente |
| T048-18 | Prueba de fallo graceful (Redis caído → MongoDB sin error 5xx) | Pruebas | `FR-048-11` | T048-14 | Completada |
| T048-19 | Prueba de invalidación de caché al modificar oferta | Pruebas | `FR-048-07` | T048-11 | Pendiente (Ola 2) |
| T048-20 | Actualizar `specs/README.md`, `requirements.md` y checklist del spec | Docs | `FR-048-01` | T048-20 | Completada |
| T048-21 | Registrar evidencias del Constitution Check | Docs | `FR-048-01` | T048-20 | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- El paquete `StackExchange.Redis` se añade solo a `Sensus.Infrastructure`; `Domain` y `Application` no referencian `Infrastructure`.
