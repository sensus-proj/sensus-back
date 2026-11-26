# Spec 029 — Recuperación de contraseña

| Campo | Valor |
|---|---|
| ID | 029 |
| Slug | `recuperacion-password` |
| Estado | Implementado |
| Ola | 1 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 001, 004, 013 |
| Versión | 2.1.0 |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Contexto y problema

Permitir que un usuario que olvidó su contraseña solicite un enlace de recuperación de un solo uso y establezca una contraseña nueva sin revelar si el correo existe. El correo se entrega mediante Amazon SES sobre TLS 1.2 o superior, sin transportes HTTP ni credenciales de proveedores externos.

## 2. Alcance

**Incluye**

- Solicitud de recuperación sin enumeración de cuentas
- Token de un solo uso con hash y vigencia de 30 minutos
- Confirmación de nueva contraseña
- Revocación de sesiones activas tras el cambio
- Envío por correo con Amazon SES sobre TLS 1.2 o superior
- Límite de 3 solicitudes por correo y hora, particionado por hash SHA-256 del correo normalizado
- Retiro del alias HTTP y de la credencial del proveedor anterior, con cierre de EX-004

**No incluye**

- Recuperación por SMS
- Preguntas de seguridad
- Soporte telefónico

## 3. Actores

- Visitante
- Usuario registrado
- Amazon SES (servicio de correo)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-029-01 | Exponer POST /api/v1/auth/recover y responder 202 siempre | Must |
| FR-029-02 | Generar token de 32 bytes, almacenar solo su hash y caducarlo en 30 minutos | Must |
| FR-029-03 | Invalidar tokens anteriores del mismo usuario al emitir uno nuevo | Must |
| FR-029-04 | Enviar el enlace por correo mediante Amazon SES con TLS 1.2 o superior | Must |
| FR-029-05 | Exponer POST /api/v1/auth/recover/confirm para fijar la nueva contraseña | Must |
| FR-029-06 | Rechazar tokens expirados, ya usados o inexistentes con 400 | Must |
| FR-029-07 | Revocar todas las sesiones activas tras el cambio de contraseña | Must |
| FR-029-08 | Requisito histórico retirado por FR-029-09: aceptar `POST /api/v1/auth/reset-password` como alias de compatibilidad de `/auth/recover/confirm`, con la misma política de tasa y las mismas respuestas. Ya no está implementado | Must (retirado) |
| FR-029-09 | El alias `POST /api/v1/auth/reset-password` deja de existir: toda solicitud a esa ruta devuelve 404 y la única ruta canónica es `/api/v1/auth/recover/confirm` | Must |
| FR-029-10 | Rechazar cualquier transporte de correo sin TLS 1.2 o superior; no se admite excepción HTTP alguna (EX-004 retirada) | Must |
| FR-029-11 | Retirar la credencial Somee del código y de la configuración; la baja del parámetro SSM `/sensus/<entorno>/MailerSettings/ApiKey` es una tarea operativa externa documentada, no código pendiente. La credencial no se rota porque se da de baja | Must |
| FR-029-12 | Enviar el correo de recuperación mediante Amazon SES (`AWSSDK.SimpleEmail`) con TLS 1.2 o superior y cerrar EX-004 | Must |
| FR-029-13 | Otorgar a la Lambda de la API, en la IaC (`infra/`), solo `ses:SendEmail` sobre las identidades verificadas de la cuenta y región; antes del despliegue deben existir la identidad del remitente verificada en SES y los parámetros `/sensus/<entorno>/SesEmailSettings/From` y `/Region` en SSM, porque sin `From` la API no arranca (`ValidateOnStart`) | Must |
| FR-029-14 | Los envíos de SES usan un *configuration set* (`sensus-transactional-<env>`) que publica rebotes, quejas, rechazos y entregas a CloudWatch y a una alarma conectada al tema SNS; los rebotes y quejas no se registran con el correo del destinatario en claro | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-029-01 | Latencia de solicitud | p95 < 700 ms |
| NFR-029-02 | Enumeración de cuentas y redacción de correo en logs | Cero diferencia de respuesta entre correo existente e inexistente; cero correos en registros (`ISensitiveRequest`) |
| NFR-029-03 | Límite de solicitudes de `/recover` | 3 por correo y hora, particionado por hash SHA-256 del correo normalizado; `RateLimiting:Recover` con `PermitLimit=3` y `WindowSeconds=3600` |

## 6. Criterios de aceptación

### CA-029-01

- **Dado** correo registrado
- **Cuando** POST /api/v1/auth/recover
- **Entonces** 202 y correo con enlace enviado por SES

### CA-029-02

- **Dado** correo inexistente
- **Cuando** POST /api/v1/auth/recover
- **Entonces** 202 idéntico al caso anterior

### CA-029-03

- **Dado** token válido y contraseña nueva válida
- **Cuando** POST /api/v1/auth/recover/confirm
- **Entonces** 204 y sesiones previas revocadas

### CA-029-04

- **Dado** token usado dos veces
- **Cuando** se reintenta la confirmación
- **Entonces** 400

### CA-029-05

- **Dado** token expirado
- **Cuando** se confirma
- **Entonces** 400 sin cambiar la contraseña

### CA-029-06

- **Dado** el alias retirado `POST /api/v1/auth/reset-password`
- **Cuando** se envía una solicitud a esa ruta con un token y una contraseña
- **Entonces** la API devuelve 404; la ruta no figura en el contrato OpenAPI y solo `/api/v1/auth/recover/confirm` está expuesta

### CA-029-07

- **Dado** `SesEmailSettings` resuelta desde SSM con `From`, `Region`, `ConfigurationSetName` y `Simulate`
- **Cuando** se envía un correo de recuperación
- **Entonces** la solicitud se realiza contra Amazon SES con TLS 1.2 o superior y el registro no contiene el destinatario ni el token

### CA-029-08

- **Dado** el repositorio y la configuración desplegada
- **Cuando** se inspeccionan los transportes de correo y las opciones de configuración
- **Entonces** no existe configuración HTTP de correo, ni `HttpMailerEmailSender`, `MailerTransportPolicy` o `MailerSettings`; cualquier transporte sin TLS 1.2 o superior se rechaza

### CA-029-09

- **Dado** un correo con 3 solicitudes previas a `/recover` en la última hora
- **Cuando** se envía la cuarta solicitud para el mismo correo
- **Entonces** la API devuelve 429; otro correo conserva su propia cuota y la clave de partición es el hash SHA-256 del correo normalizado, sin PII

### CA-029-10

- **Dado** una solicitud a `/recover` con correo válido
- **Cuando** se emiten los registros de `LoggingBehavior` y `PerformanceBehavior`
- **Entonces** el correo aparece redactado y no se registra ningún dato personal

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| POST | `/api/v1/auth/recover` | Pública |
| POST | `/api/v1/auth/recover/confirm` | Pública |

`POST /api/v1/auth/reset-password` no forma parte del contrato: fue retirado y devuelve 404 (FR-029-09). Contrato detallado en [`contracts/openapi.yaml`](contracts/openapi.yaml).

## 8. Dependencias y supuestos

- Depende de: 001, 004, 013
- El comportamiento descrito aplica a la versión v1 de la API.
- La configuración y los secretos se resuelven desde SSM Parameter Store bajo `/sensus/<entorno>/`, nunca desde el repositorio.
- `SesEmailSettings` declara `From`, `Region`, `ConfigurationSetName` y `Simulate`. En la configuración base, `Simulate=false` y `From` vacío (se resuelve por SSM en producción); en Development, `Simulate=true`. No se escriben secretos en archivos versionados.

## 9. Riesgos

- Un fallo de SES deja al usuario sin enlace
- El correo es un canal fuera del control del sistema

## 10. Preguntas abiertas

- ¿Se exige reautenticación adicional para cambios desde un dispositivo nuevo?

## 11. Trazabilidad

- Requisitos de este spec: `FR-029-01`, `FR-029-02`, `FR-029-03`, `FR-029-04`, `FR-029-05`, `FR-029-06`, `FR-029-07`, `FR-029-08`, `FR-029-09`, `FR-029-10`, `FR-029-11`, `FR-029-12`, `NFR-029-01`, `NFR-029-02`, `NFR-029-03`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
- Contrato: [`contracts/openapi.yaml`](contracts/openapi.yaml)
