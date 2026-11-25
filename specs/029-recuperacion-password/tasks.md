# Tareas 029 — Recuperación de contraseña

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado (salida del sandbox de SES pendiente, T029-23) |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T029-01 | Actualizar y validar el contrato OpenAPI del spec (v2.0.0, sin la ruta del alias) | Api | `FR-029-01` | — | Hecho |
| T029-02 | Entidad `PasswordResetToken` con vigencia de 30 minutos y uso único | Domain | `FR-029-02` | T029-01 | Hecho |
| T029-03 | `RequestPasswordResetCommand` y `ConfirmPasswordResetCommand` | Application | `FR-029-01` | T029-02 | Hecho |
| T029-04 | Colección `password_reset_tokens` con índice TTL de 30 minutos y cliente SES | Infrastructure | `FR-029-01` | T029-03 | Hecho |
| T029-05 | Endpoints de solicitud y confirmación | Api | `FR-029-01` | T029-04 | Hecho |
| T029-06 | Modelar el token de recuperación y su vigencia de 30 minutos | Domain | `FR-029-02` | T029-05 | Hecho |
| T029-07 | Implementar el envío con SES y la plantilla de correo | Infrastructure | `FR-029-04` | T029-06 | Hecho |
| T029-08 | Coordinar la revocación de sesiones tras el cambio | Application | `FR-029-07` | T029-07 | Hecho |
| T029-09 | Probar que la respuesta no permite enumerar cuentas | Api | `FR-029-01` | T029-08 | Hecho |
| T029-10 | Pruebas de integración del flujo completo: 202/400/404/429 y latencia | Pruebas | `FR-029-01` | T029-09 | Hecho |
| T029-11 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-029-01` | T029-10 | Hecho |
| T029-12 | Registrar evidencias del Constitution Check | Docs | `FR-029-01` | T029-11 | Hecho |
| T029-13 | Documentar el alias `POST /auth/reset-password` y su contrato (histórico) | Docs | `FR-029-08` | — | Hecho |
| T029-14 | Retirar el alias: la ruta deja de existir y devuelve 404 | Api | `FR-029-09` | T029-13 | Hecho |
| T029-15 | Eliminar `HttpMailerEmailSender`, `MailerTransportPolicy`, `MailerSettings` y toda su configuración | Infrastructure | `FR-029-10` | — | Hecho |
| T029-16 | Retirar la credencial Somee del código y de la configuración; documentar la baja del parámetro SSM como tarea operativa externa | Operación | `FR-029-11` | T029-15 | Hecho |
| T029-17 | Migrar el envío a Amazon SES con TLS 1.2+ y cerrar EX-004 | Infrastructure | `FR-029-12` | T029-15 | Hecho |
| T029-18 | Límite de `/recover`: 3 solicitudes por correo y hora, partición por hash SHA-256 del correo normalizado y `RateLimiting:Recover` | Api | `NFR-029-03` | T029-05 | Hecho |
| T029-19 | `RequestPasswordResetCommand` implementa `ISensitiveRequest`; correo redactado en `LoggingBehavior` y `PerformanceBehavior` | Application | `NFR-029-02` | T029-03 | Hecho |
| T029-20 | Retirada del alias en el contrato OpenAPI: sin ruta, 404 documentado y versión 2.0.0 | Docs | `FR-029-09` | T029-14 | Hecho |
| T029-21 | Cierre de EX-004 y baja de la credencial Somee (retiro y documentación operativa) | Docs | `FR-029-11`, `FR-029-12` | T029-17 | Hecho |
| T029-22 | Permiso `ses:SendEmail` del rol de la Lambda en `SensusInfraStack.cs` (`AllowSesSendTransactionalEmail`), restringido a identidades de la cuenta y región | Infra | `FR-029-13` | T029-21 | Hecho |
| T029-23 | Operativo: identidad del remitente verificada en SES us-east-2, parámetros SSM `SesEmailSettings/From` y `/Region` creados (2026-11-25) y solicitud de acceso de producción de SES | Operación | `FR-029-13` | T029-22 | Parcial (falta salir del sandbox de SES) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- **Salvedad operativa (T029-16)**: la baja del parámetro SSM `/sensus/<entorno>/MailerSettings/ApiKey` se ejecuta fuera del repositorio por el responsable de operaciones. No es código pendiente. La credencial Somee no se rota porque se da de baja.
- EX-004 quedó cerrada el 2026-11-23 en el Apéndice E de la constitución; no queda excepción TLS vigente.
