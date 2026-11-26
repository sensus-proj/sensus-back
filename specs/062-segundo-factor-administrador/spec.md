# Spec 062 — Segundo factor TOTP para Administrador y step-up en acciones privilegiadas

| Campo | Valor |
|---|---|
| ID | 062 |
| Slug | `segundo-factor-administrador` |
| Estado | Implementado |
| Ola | 6 |
| Tipo | Nuevo |
| Dueño | Backend Sensus |
| Dependencias | 002, 004, 020, 021 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

---

## 1. Contexto y problema

El rol `Administrador` concentra operaciones de alto impacto (sanciones manuales, reversión de sanciones, consulta de auditoría de moderación, liberación manual de cuarentena multimedia y disparadores de expiración). Sin segundo factor (2FA), la exposición de una contraseña concede acceso privilegiado completo (brecha `G-05`, Principio VIII / OWASP ASVS L3).

## 2. Alcance

**Incluye**

- Enrolamiento TOTP RFC 6238 (`HMACSHA1`, paso de 30 s, 6 dígitos) nativo en .NET sin dependencias externas (`Otp.NET` prohibida)
- Cifrado en reposo del secreto TOTP con AES-256-GCM mediante clave configurada en SSM (`/sensus/<env>/Mfa/EncryptionKey`)
- Generación de 8 códigos de recuperación de un solo uso almacenados únicamente por hash SHA-256
- Endpoints `POST /api/v1/auth/mfa/enroll` y `POST /api/v1/auth/mfa/verify` con límite de intentos y bloqueo temporal
- Desafío intermedio en `POST /api/v1/auth/login` cuando una cuenta `Administrador` tiene TOTP activo
- Política de *step-up* (`amr=mfa` + ventana de recencia configurable en `mfa_at`) sobre todas las rutas administrativas
- Registro de auditoría de solo adición de eventos MFA (Spec 021)

**No incluye**

- 2FA obligatorio para usuarios `Consumidor` o `Comerciante`
- Envío de OTP por SMS o correo electrónico
- WebAuthn / FIDO2

## 3. Actores

- Usuario con rol `Administrador`
- Sistema de auditoría de solo adición

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-062-01 | Implementar TOTP RFC 6238 con `HMACSHA1` nativo de .NET (6 dígitos, ventana ±1 paso de 30 s) y URI `otpauth://totp/`, validado contra vectores oficiales del RFC 6238 sin paquetes de terceros | Must |
| FR-062-02 | Cifrar el secreto TOTP en reposo con AES-256-GCM usando `Mfa:EncryptionKey` (SSM) y almacenar 8 códigos de recuperación de un solo uso únicamente con hash SHA-256; fallar cerrado si falta la clave de cifrado | Must |
| FR-062-03 | Exponer `POST /api/v1/auth/mfa/enroll` (solo `Administrador`) y `POST /api/v1/auth/mfa/verify` para activar el enrolamiento, validar TOTP o código de recuperación y emitir JWT con claims `amr=mfa` y `mfa_at` | Must |
| FR-062-04 | Emitir un desafío intermedio en `POST /api/v1/auth/login` (`requiresMfa=true`, `mfaChallengeToken` de corta vigencia sin permisos operativos) cuando una cuenta `Administrador` tenga 2FA activo | Must |
| FR-062-05 | Exigir `amr=mfa` con recencia ≤ `Mfa:StepUpWindowMinutes` (15 min) en todas las rutas con política `AdminOnly`, aplicar bloqueo temporal tras intentos fallidos y auditar eventos MFA en colección de solo adición | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-062-01 | Resistencia a fuerza bruta | Bloqueo temporal tras 5 intentos fallidos consecutivos (15 min) + rate limiting estricto |
| NFR-062-02 | Protección de secretos | Secreto en texto claro visible únicamente una vez al enrolar; cero secretos ni códigos en logs |

## 6. Criterios de aceptación

### CA-062-01 — Vectores oficiales RFC 6238

- **Dado** los vectores SHA-1 del Apéndice B del RFC 6238
- **Cuando** se calcula el código con la implementación .NET `HMACSHA1`
- **Entonces** los códigos generados coinciden exactamente con los 6 valores oficiales del RFC

### CA-062-02 — Cifrado en reposo y códigos de recuperación

- **Dado** un `Administrador` que solicita `POST /api/v1/auth/mfa/enroll`
- **Cuando** se persiste su configuración MFA en `User`
- **Entonces** el documento en base de datos contiene únicamente el texto cifrado AES-GCM y los hashes SHA-256 de los 8 códigos; cada código de recuperación es de un solo uso (`FR-062-02`)

### CA-062-03 — Desafío intermedio y verificación

- **Dado** un `Administrador` con TOTP activo que envía contraseña correcta a `POST /api/v1/auth/login`
- **Cuando** responde el servidor
- **Entonces** devuelve `requiresMfa=true` y `mfaChallengeToken` sin sesión operativa; ese token solo sirve en `POST /api/v1/auth/mfa/verify` y, al verificar código TOTP o recuperación válido, emite JWT con `amr=mfa` y `mfa_at` (`FR-062-03`, `FR-062-04`)

### CA-062-04 — Step-up obligatorio y bloqueo por fuerza bruta

- **Dado** un token de `Administrador` sin `amr=mfa` o con `mfa_at` vencido (> 15 min)
- **Cuando** llama a cualquier ruta `AdminOnly` (`/moderation/sanctions`, `/admin/moderation/audit`, `/media/{id}/release`, `/offers/expire-check`, `/offers/claims/expire-check`)
- **Entonces** recibe `403 Forbidden`; tras 5 códigos inválidos consecutivos la verificación responde bloqueo temporal (`429`/`423` o `403`) y se registra evento de auditoría de solo adición (`FR-062-05`)

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| POST | `/api/v1/auth/mfa/enroll` | JWT (`Administrador`) |
| POST | `/api/v1/auth/mfa/verify` | JWT (`Administrador` o `mfaChallengeToken`) |

## 8. Dependencias y supuestos

- Depende de: 002, 004, 020, 021
- La clave `Mfa:EncryptionKey` se provisiona en SSM `/sensus/<env>/Mfa/EncryptionKey` (32 bytes Base64). En ausencia de clave, el enrolamiento y verificación fallan cerrados.

## 9. Riesgos y mitigaciones

- Pérdida del autenticador móvil por parte del administrador → mitigada mediante 8 códigos de recuperación de un solo uso con hash SHA-256.
- Desincronización de reloj NTP → ventana de verificación ±1 paso de 30 segundos.

## 10. Preguntas abiertas

- Sin preguntas bloqueantes.

## 11. Trazabilidad

- Requisitos de este spec: `FR-062-01`, `FR-062-02`, `FR-062-03`, `FR-062-04`, `FR-062-05`, `NFR-062-01`, `NFR-062-02`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
