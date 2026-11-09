# Spec 048 — Caché distribuido con Redis

| Campo | Valor |
|---|---|
| ID | 048 |
| Slug | `cache-distribuido-redis` |
| Estado | Implementado (Core) |
| Ola | 7 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 010, 024, 026, 032 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

---

## 1. Contexto y problema

El backend Sensus necesita un sistema de caché distribuido para lecturas recurrentes que mejore el rendimiento de la API y reduzca la carga sobre MongoDB. Actualmente `PagedResult` existe en `Sensus.Application.Common.Models` pero no se usa en ninguna consulta expuesta. La constitución §6 exige que las lecturas recurrentes usen caché con TTL explícito e invalidación documentada. El profesor requiere caché a nivel de base de datos, backend y frontend, con uso distribuido de Redis (v2).

## 2. Alcance

**Incluye**

- Integración con Redis como caché distribuido para lecturas recurrentes (feed, detalle de oferta, recomendaciones, configuración de moderación, tokens revocados)
- Interfaz `ICacheService` en `Sensus.Application.Common.Interfaces` con implementación en `Sensus.Infrastructure`
- TTL explícito configurable por entidad/clave, nunca hardcodeado
- Invalidación documentada y evicción ante cambios (creación, actualización, eliminación de ofertas, publicaciones, comentarios)
- Uso de caché en endpoints de lectura: `GET /offers/{id}`, `GET /offers/nearby`, `GET /feed/nearby`, `GET /merchants/me`, `GET /users/profile`
- Límites de tamaño y política de evicción (LRU)
- Salud del caché (health check) integrado con spec 016
- Fallo graceful: si Redis no está disponible, la API consulta MongoDB directamente (no falla la operación)

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
| FR-048-06 | Cachear respuestas de `GET /feed/nearby` con TTL configurable | Must |
| FR-048-07 | Invalidar caché de oferta al crear, actualizar o eliminar una oferta | Must |
| FR-048-08 | Invalidar caché de feed al crear, actualizar o eliminar una publicación | Must |
| FR-048-09 | Invalidar caché de comercio al actualizar el perfil del comerciante | Must |
| FR-048-10 | Configurar TTL por entidad en `appsettings.json` (no hardcodeado) | Must |
| FR-048-11 | Devolver datos desde MongoDB si Redis no está disponible (falso graceful) | Must |
| FR-048-12 | Health check del caché integrado con `/health/ready` | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-048-01 | Latencia de lectura con caché | p95 < 300 ms |
| NFR-048-02 | Latencia de lectura sin caché (fallback MongoDB) | p95 < 500 ms |
| NFR-048-03 | TTL mínimo | Configurable, por entidad |
| NFR-048-04 | Disponibilidad del servicio ante caída de Redis | 100 % (fallback a MongoDB) |
| NFR-048-05 | Costo mensual | Dentro del Free Tier de AWS (ElastiCache o similar) |

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

### CA-048-06: Health check
- **Dado**: el endpoint `/health/ready`
- **Cuando**: se consulta el estado del sistema
- **Entonces**: incluye el estado del caché Redis

## 7. Contratos

No expone nuevos endpoints públicos; mejora el rendimiento de endpoints existentes.

## 8. Exclusiones

- No incluye caché del frontend (móvil/web).
- No incluye caché de escritura.
- No incluye Redis como base de datos primaria.
- No incluye pub/sub de Redis para invalidación en tiempo real entre instancias.

---
