# Plan 048 — Caché distribuido con Redis

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (Implementado) |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

---

## 1. Resumen técnico

Implementar caché distribuido con Redis para lecturas recurrentes del backend Sensus. Se crea la interfaz `ICacheService` en Application, la implementación `RedisCacheService` en Infrastructure usando `StackExchange.Redis`, y se integra en los endpoints de lectura de ofertas, feed y comercios. El caché usa TTL explícito configurado por entidad, invalidación ante cambios y fallback graceful a MongoDB si Redis no está disponible.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Sin cambios (el caché es una preocupación de infraestructura/aplicación) |
| Application | Interfaz `ICacheService` en `Common/Interfaces`, DTOs de respuesta sin cambios |
| Infrastructure | `RedisCacheService` implementando `ICacheService`, paquete `StackExchange.Redis` |
| Api | Registro en DI en `Program.cs`, integración en endpoints de lectura (`GET /offers/{id}`, `GET /offers/nearby`, `GET /feed/nearby`, `GET /merchants/me`, `GET /users/profile`) |

## 3. Modelo de datos e índices

No aplica: Redis es un almacén clave-valor en memoria, no requiere índices declarados como MongoDB. Las claves siguen el patrón `sensus:{entidad}:{id}` con TTL por entidad.

## 4. Seguridad

- Sin datos sensibles (PII, tokens) almacenados en caché
- Los datos en caché son los mismos que se devuelven por la API (sin datos adicionales)
- Redis accesible solo desde la red interna de AWS (Security Group restringido)
- Sin secretos en código: cadena de conexión de Redis en AWS Secrets Manager o SSM

## 5. Observabilidad y rendimiento

- Health check del caché integrado con `/health/ready`
- Métricas de aciertos/fallos del caché (hit/miss ratio)
- TTL monitoreado por entidad

## 6. Costo AWS

- ElastiCache para Redis (nodo cache.t3.micro o similar dentro del Free Tier)
- Si no cabe en Free Tier, usar caché en memoria (`IMemoryCache`) como alternativa de desarrollo local
- Costo objetivo: 0 USD (Free Tier) o aprobación previa si excede

## 7. Estrategia de pruebas

- Unitarias: `RedisCacheService` con dobles de `IDatabase` (StackExchange.Redis)
- Integración: Redis real vía Testcontainers en `tests/Sensus.IntegrationTests`
- Fallo graceful: prueba que simula Redis caído y verifica fallback a MongoDB
- Invalidación: prueba que crea/actualiza/elimina una oferta y verifica que la caché se invalida

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | Requisitos `FR-048-01` a `FR-048-12` con criterios de aceptación medibles |
| II | PASS | Interface en Application, implementación en Infrastructure, composición en Api |
| III | PASS | No se exponen entidades de dominio; el caché es infraestructura |
| IV | PASS | Sin PII en caché, Security Group restringido, fallback seguro |
| V | PASS | Pruebas unitarias, integración con Redis real, pruebas de fallback |
| VI | PASS | Costo 0 USD objetivo con Free Tier; sin recursos fuera de `infra/` |
| VII | PASS | Latencia p95 < 300 ms con caché, health check, métricas hit/miss |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- ElastiCache puede no estar disponible en el Free Tier de AWS en todas las regiones; se usará `IMemoryCache` como fallback en desarrollo local.
- La invalidación por TTL puede causar lecturas de datos obsoletos por un corto período; se acepta como compensación de rendimiento.
- StackExchange.Redis es una dependencia externa; se debe evaluar licencia y mantenimiento (constitución §15).

---
