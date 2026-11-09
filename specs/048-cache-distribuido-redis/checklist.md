# Checklist 048 — Caché distribuido con Redis

Alineado a `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 048 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 010, 024, 026, 032
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Existe interfaz `ICacheService` en `Sensus.Application.Common.Interfaces`
- [x] Existe implementación `RedisCacheService` en `Sensus.Infrastructure`
- [x] `StackExchange.Redis` solo referenciado en `Sensus.Infrastructure`
- [x] `ICacheService` registrado en DI de `Sensus.Infrastructure/DependencyInjection.cs`
- [x] TTL configurado en `appsettings.json` (no hardcodeado)
- [x] Caché integrado en `GET /users/profile` y `GET /merchants/me`
- [x] Invalidación al actualizar perfil de comerciante y perfil de usuario
- [x] Fallback graceful a persistencia si Redis no está disponible o falla la conexión
- [x] Sin PII ni tokens almacenados en caché

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4 pruebas de arquitectura superadas)
- [x] Pruebas unitarias y de integración añadidas y en verde (201 pruebas superadas)
- [x] Cobertura preservada en Domain y Application
- [x] `dotnet format --verify-no-changes` verificado en verde
- [x] Logs estructurados con advertencias de fallback graceful y sin PII
- [x] Revisión de seguridad: sin secretos expuestos
- [x] Constitution Check en PASS

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS (ICacheService en Application, Redis en Infrastructure)
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS (Sin PII ni tokens en claves o payloads)
- [x] **V. Verificación automatizada** — PASS (201 pruebas pasando)
- [x] **VI. Costo cero e IaC** — PASS (Opción dentro de Free Tier / Fail-graceful)
- [x] **VII. Rendimiento y observabilidad** — PASS (Reducción de latencia en consultas frecuentes)
