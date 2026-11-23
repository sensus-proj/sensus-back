# Plan 029 — Recuperación de contraseña

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 2.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## 1. Resumen técnico

Permitir que un usuario que olvidó su contraseña solicite un enlace de recuperación de un solo uso y establezca una contraseña nueva sin revelar si el correo existe. El enlace se entrega por Amazon SES sobre TLS 1.2 o superior, con token vigente 30 minutos, límite de 3 solicitudes por correo y hora y retiro del alias HTTP y de la credencial del proveedor anterior.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `PasswordResetToken` con vigencia de 30 minutos y uso único |
| Application | `RequestPasswordResetCommand` (`ISensitiveRequest`) y `ConfirmPasswordResetCommand`; política de tasa `recover` |
| Infrastructure | Colección `password_reset_tokens` con índice TTL de 30 minutos y `SesEmailSender` sobre `AWSSDK.SimpleEmail`; sin `HttpMailerEmailSender`, `MailerTransportPolicy` ni `MailerSettings` |
| Api | Endpoints de solicitud y confirmación; el alias `/auth/reset-password` no existe y devuelve 404 |

## 3. Modelo de datos e índices

- `password_reset_tokens`: hash del token, `userId`, `expiresAt`, `usedAt`; índice TTL de 30 minutos sobre `expiresAt`.
- Clave de partición del limitador: hash SHA-256 del correo normalizado, sin PII en la clave.
- Configuración `RateLimiting:Recover`: `PermitLimit=3`, `WindowSeconds=3600`.

## 4. Seguridad

- Token aleatorio de 32 bytes; solo se persiste su hash; vigencia de 30 minutos.
- Respuesta neutra (202) ante correo existente e inexistente; token de un solo uso e invalidación de tokens previos.
- Revocación de todas las sesiones activas tras el cambio de contraseña.
- Sin token ni correo en logs: `RequestPasswordResetCommand` implementa `ISensitiveRequest` y `LoggingBehavior`/`PerformanceBehavior` redactan el correo.
- Transporte de correo exclusivamente Amazon SES sobre TLS 1.2 o superior. No se rechaza ni se admite configuración HTTP: `HttpMailerEmailSender`, `MailerTransportPolicy`, `MailerSettings` y su configuración han sido eliminados. EX-004 quedó cerrada el 2026-11-23 y no hay excepción TLS vigente.
- `SesEmailSettings` (`From`, `Region`, `ConfigurationSetName`, `Simulate`) se resuelve desde SSM bajo `/sensus/<entorno>/`. Base: `Simulate=false` y `From` vacío (resolución en producción); Development: `Simulate=true`. Sin secretos en el repositorio.
- La credencial Somee se retira del código y de la configuración; su baja en `/sensus/<entorno>/MailerSettings/ApiKey` es operación externa documentada. No se rota porque se da de baja.
- Límite `recover`: 3 solicitudes por correo y hora, particionado por hash SHA-256 del correo normalizado, con 429 y `Retry-After`. Cuotas independientes por correo.
- **Modelo de amenazas (STRIDE) del flujo de recuperación**

| Amenaza | Vector | Mitigación | Riesgo residual |
|---|---|---|---|
| *Spoofing* del correo | Solicitud haciéndose pasar por el titular o suplantación del remitente | Titularidad verificada por el buzón, respuesta 202 neutra, `From` fijo resuelto desde SSM e identidad verificada en SES | Bajo |
| *Tampering* del token | Alteración del enlace o del token en tránsito o en la petición | Token de 32 bytes del que solo se guarda el hash, TLS 1.2+ de extremo a extremo, un solo uso, invalidación de tokens previos y 400 ante token inválido | Bajo |
| *Information disclosure* por logs | Correo o token filtrado en trazas o métricas | `ISensitiveRequest`, redacción en `LoggingBehavior` y `PerformanceBehavior`, hash SHA-256 como clave de partición y ausencia de PII en CloudWatch | Muy bajo |
| *DoS* por solicitudes | Inundación de `/recover` para agotar correo o capacidad | `RateLimiting:Recover` de 3 por correo y hora con hash SHA-256, 429 con `Retry-After`, alerta por volumen y *throttling* de API Gateway como control compensatorio | Medio-bajo |

- **Principio VIII (DevSecOps de nivel fintech):** ASVS 5.0 nivel 3 en autenticación y recuperación, NIST SP 800-63B para el ciclo de vida de la credencial, validación con lista de permitidos y límites de tamaño, errores uniformes `ProblemDetails` sin enumeración, auditoría y logs sin PII con `correlation-id`, secretos en SSM, *fail-closed* ante fallo del envío y escaneos bloqueantes (SAST, SCA, secretos, IaC, DAST y SBOM) en el pipeline.

## 5. Observabilidad y rendimiento

- Métricas de solicitudes, confirmaciones y fallos de envío de SES, con `correlation-id`.
- Alerta si el volumen de solicitudes se dispara.
- Objetivo `NFR-029-01`: p95 < 700 ms, verificado con 20 solicitudes.

## 6. Costo AWS

- Amazon SES dentro del Free Tier (3.000 mensajes/mes desde Lambda). Sin recursos nuevos de pago y sin cambios en IaC.
- Paquete NuGet `AWSSDK.SimpleEmail` versión `4.0.100.25`, licencia Apache-2.0, sin costo de paquete; el costo es el del servicio SES dentro del free tier (3.000 mensajes/mes desde Lambda).
- La configuración `SesEmailSettings` se lee desde SSM al arrancar; ningún despliegue crea ni modifica secretos.

## 7. Estrategia de pruebas

- Unitarias de `PasswordResetToken`: vigencia de 30 minutos y uso único.
- Unitarias del handler: texto de 30 minutos en el correo.
- Integración: 202 idéntico entre correo existente e inexistente; 400 ante token inválido, usado o vencido; 404 en el alias retirado; 429 en la cuarta solicitud del mismo correo y cuotas separadas por correo.
- Prueba de latencia p95 < 700 ms con 20 solicitudes.
- `SesEmailSender`: doble simulado, envío con request a SES y fallo sin PII en logs.
- Se eliminan las pruebas de `MailerTransportPolicyTests`, transporte retirado.
- Test de arquitectura y cobertura ≥ 80 % en Domain y Application.

## 8. Constitution Check

Evaluación del plan vigente.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-029-01` a `FR-029-12` y `NFR-029-01` a `NFR-029-03`, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2; SDK de AWS solo en Infrastructure |
| III | PASS | DTOs, comandos y consultas separados; contrato OpenAPI v2.0.0 sin el alias retirado |
| IV | PASS | SES con TLS 1.2+, TTL de 30 minutos, hash del correo para el límite de tasa, `ISensitiveRequest`, sin secretos en repositorio |
| V | PASS | Estrategia de pruebas definida en §7, incluidas dobles de fallo de SES y regresión del alias 404 |
| VI | PASS | SES dentro del Free Tier (3.000 mensajes/mes desde Lambda) y sin recursos nuevos de pago (§6) |
| VII | PASS | Índice TTL, latencia p95 < 700 ms con 20 solicitudes y métricas con `correlation-id` (§5) |
| VIII | PASS | Modelo de amenazas STRIDE y controles fintech en §4; sin excepción TLS vigente |

Resultado global: **PASS**. Excepción `EX-004` cerrada el 2026-11-23 por la migración a Amazon SES (Spec 029); no queda excepción TLS vigente.

## 9. Riesgos de implementación

- Un fallo de SES deja al usuario sin enlace
- El correo es un canal fuera del control del sistema

## 10. Orden de implementación

1. `T029-01` Contrato OpenAPI v2.0.0 validado, sin la ruta del alias
2. `T029-02` Entidad `PasswordResetToken` con vigencia de 30 minutos y uso único
3. `T029-03` `RequestPasswordResetCommand` y `ConfirmPasswordResetCommand`
4. `T029-04` Colección `password_reset_tokens` con índice TTL y cliente SES
5. `T029-05` Endpoints de solicitud y confirmación
6. `T029-06` Modelado del token y su vigencia de 30 minutos
7. `T029-07` Envío con SES y plantilla de correo
8. `T029-08` Revocación de sesiones tras el cambio
9. `T029-09` Respuesta neutra sin enumeración de cuentas
10. `T029-10` Pruebas de integración del flujo completo
11. `T029-11` Actualización de `specs/README.md`, `requirements.md` y checklist
12. `T029-12` Evidencias del Constitution Check
13. `T029-13` Documentación histórica del alias
14. `T029-14` Retirada del alias: la ruta devuelve 404
15. `T029-15` Eliminación de `HttpMailerEmailSender`, `MailerTransportPolicy` y `MailerSettings`
16. `T029-16` Retiro de la credencial Somee y documentación de la baja operativa externa
17. `T029-17` Migración a SES con TLS 1.2+ y cierre de EX-004
18. `T029-18` Límite de 3 solicitudes por correo y hora con partición por hash SHA-256
19. `T029-19` `ISensitiveRequest` y redacción del correo en los comportamientos de logs
20. `T029-20` Retirada del alias en OpenAPI y publicación de la versión 2.0.0
21. `T029-21` Cierre formal de EX-004 y documentación de la baja de la credencial Somee
