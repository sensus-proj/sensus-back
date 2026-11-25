# Checklist 029 — Recuperación de contraseña

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 029 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 001, 004, 013
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La respuesta es idéntica para correo existente e inexistente
- [x] El token solo se guarda hasheado
- [x] El enlace caduca a los 30 minutos (T029-06)
- [x] Las sesiones previas quedan revocadas
- [x] El envío se realiza por Amazon SES con TLS 1.2 o superior (T029-07, T029-17)
- [x] No existe configuración HTTP ni transporte sin TLS: retirados `HttpMailerEmailSender`, `MailerTransportPolicy` y `MailerSettings` (T029-15)
- [x] EX-004 cerrada el 2026-11-23 por la migración a SES (T029-17, T029-21)
- [x] El alias `POST /api/v1/auth/reset-password` devuelve 404 y no figura en el contrato OpenAPI (T029-14, T029-20)
- [x] El límite de `/recover` es de 3 solicitudes por correo y hora con partición por hash SHA-256, sin PII en la clave (T029-18)
- [x] `RequestPasswordResetCommand` es `ISensitiveRequest` y el correo se redacta en los logs (T029-19)
- [x] La credencial Somee se retiró del código y de la configuración; la baja operativa externa del parámetro SSM está documentada (T029-16, T029-21). No se rota: se da de baja

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas añadidas y ejecutadas: 911 unitarias, 4 de arquitectura, 3 E2E, 489/498 de integración y las 17 pruebas específicas de la Spec 029 en verde. Las 9 restantes de integración son Testcontainers y no pueden arrancar porque el daemon Docker no está disponible en este entorno; no se omitieron ni se redujeron umbrales (brecha externa registrada en la fila E)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado a la versión 2.0.0 y sin el alias
- [x] IaC actualizada y `cdk diff` revisado si aplica (sin recursos nuevos)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. DevSecOps de nivel fintech** — PASS (STRIDE y controles fintech en `plan.md` §4; sin excepción TLS vigente)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/`: `PasswordResetToken` con TTL de 30 minutos y uso único; handler con texto de 30 minutos e `ISensitiveRequest`. Resultado: 911/911 en verde | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/`: 202 idéntico entre correo existente e inexistente; 400; 404 en el alias; 429 en la cuarta solicitud del mismo correo, cuotas separadas por correo y cuota compartida entre IP; latencia p95 < 700 ms con 20 solicitudes; `SesEmailSenderTests` (simulado, request a SES y fallo sin PII en logs). Resultado: 17/17 específicas en verde; 489/498 del total de integración en verde | ☑ |
| Pruebas de arquitectura y E2E | 4/4 de arquitectura y 3/3 E2E (incluido el viaje de recuperación por ruta canónica) en verde | ☑ |
| Cobertura | Domain 89,41 % (2297/2569 líneas) y Application 95,94 % (6126/6385 líneas), umbral 80 % verificado con `check-coverage.mjs` | ☑ |
| Brecha externa de entorno | 9 pruebas Testcontainers (`NotificationMongoRepositoryTests` y `MongoOfferDiscoveryIntegrationTests`) fallan por `DockerUnavailableException`: `npipe://./pipe/docker_engine` no disponible. No están omitidas y no guardan relación con los cambios de la Spec 029 | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` (v2.0.0, esquemas `RecoverRequest`/`ResetPasswordRequest`, 202/400/429 y 204/400/429, `correlation-id` y `ProblemDetails`, sin ruta del alias) | ☑ |
| Configuración | `SesEmailSettings` (`From`, `Region`, `ConfigurationSetName`, `Simulate`) y `RateLimiting:Recover` (`PermitLimit=3`, `WindowSeconds=3600`) | ☑ |
| Retiro del transporte HTTP | Sin `HttpMailerEmailSender`, `MailerTransportPolicy`, `MailerSettings` ni `MailerTransportPolicyTests` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-14 | Claude (sesión de verificación) | Parcial | `POST /auth/recover/confirm` canónico con `reset-password` como alias para la app móvil; ambos probados. Pendiente: caducidad de 30 min (hoy 15), envío por SES (FR-029-04), invalidar tokens previos (FR-029-03), límite de 3 por correo y hora (NFR-029-03; hoy 5 por IP cada 15 min y fijo en código). `dotnet build -c Release` 0 errores y 0 advertencias; 613 pruebas en verde (4 arquitectura, 498 unitarias, 3 E2E, 108 integración); cobertura combinada Domain 90,2 % y Application 97,4 %. |
| 2026-11-23 | OpenCode (sesión de implementación) | Implementado | Envío por Amazon SES con TLS 1.2+ y `AWSSDK.SimpleEmail` 4.0.100.25; TTL del token de 30 minutos; alias `POST /api/v1/auth/reset-password` retirado y con 404 verificado; límite de 3 por correo y hora con hash SHA-256 y sin PII en la clave; `ISensitiveRequest` y correo redactado en logs; `EX-004` cerrada el 2026-11-23. Baja operativa externa del parámetro SSM `/sensus/<entorno>/MailerSettings/ApiKey` documentada en `tasks.md` (T029-16): no es código pendiente y la credencial no se rota porque se da de baja. Verificación local 2026-11-23: `dotnet build -c Release -warnaserror` 0 errores/0 advertencias; `dotnet format --verify-no-changes` sin cambios; `check-specs.mjs` 59 specs válidas; 911 unitarias, 4 de arquitectura, 3 E2E y 489/498 de integración en verde; 9 pruebas Testcontainers bloqueadas por daemon Docker no disponible (brecha externa, sin omisiones); cobertura Domain 89,41 % y Application 95,94 %. Sin operaciones de Git. |
