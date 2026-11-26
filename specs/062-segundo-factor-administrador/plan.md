# Plan 062 — Segundo factor TOTP para Administrador y step-up

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Implementar MFA TOTP RFC 6238 usando `System.Security.Cryptography.HMACSHA1` e `AesGcm` en .NET 10 sin dependencias externas, integrado con el agregado `User`, el login con desafío intermedio, los endpoints `/api/v1/auth/mfa/enroll` y `/api/v1/auth/mfa/verify`, y la política `AdminOnlyPolicy` con validación de claims `amr=mfa` y ventana de recencia `mfa_at` en todas las rutas privilegiadas.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `UserMfaState` en `User.cs` (estado cifrado AES-GCM, hashes de recovery codes, `FailedAttempts`, `LockedUntilUtc`) y enum `ModerationTargetType.AdminMfa` |
| Application | `ITotpService`, `MfaSettings`, `EnrollMfaCommand`, `VerifyMfaCommand`, actualización `LoginCommandHandler` y `AuthResponseDto` (`RequiresMfa`, `MfaChallengeToken`) |
| Infrastructure | `Rfc6238TotpService` (`HMACSHA1` + `AesGcm` + SHA-256), `UserDocument` persistencia MFA, `JwtTokenGenerator` (`GenerateAdminStepUpToken`, `GenerateMfaChallengeToken`) |
| Api | `AuthEndpoints` (`/mfa/enroll`, `/mfa/verify`), `SecurityExtensions` (`AdminOnlyPolicy` con `amr=mfa` y recencia, rechazo de `token_use=mfa_challenge` en políticas generales) |

## 3. Modelo de datos e índices

- Subdocumento opcional embebido en `usuarios` (`mfa`): `isEnabled`, `isPendingConfirmation`, `encryptedSecret`, `recoveryCodeHashes`, `failedAttempts`, `lockedUntilUtc`, `enrolledAtUtc`, `lastVerifiedTimeStep`. Sin colección extra ni impacto de almacenamiento Free Tier.

## 4. Seguridad

- **Zero dependencias nuevas**: RFC 6238 implementado con `HMACSHA1` de .NET y Base32 RFC 4648; sin `Otp.NET`.
- **Cifrado autenticado**: `AesGcm` con clave de 256 bits en SSM `/sensus/<env>/Mfa/EncryptionKey` (nonce aleatorio de 12 bytes + tag de 16 bytes por cifrado).
- **Anti-reuso en la misma ventana**: almacenamiento de `lastVerifiedTimeStep` para impedir reutilizar el mismo código de 6 dígitos en la misma ventana de 30 s.
- **Step-up enforcement**: todas las rutas con `AdminOnlyPolicy` validan `amr=mfa` + `mfa_at` dentro de `Mfa:StepUpWindowMinutes` (15 min) + exclusión de `mfa_challenge` en las demás políticas.

### Modelo de amenazas (STRIDE — Principio VIII)

| Amenaza | Escenario | Control existente (con archivo/prueba) | Riesgo residual |
|---|---|---|---|
| **S — Spoofing** | Atacante con contraseña de Admin intenta iniciar sesión o forzar códigos de 6 dígitos | Desafío intermedio `mfaChallengeToken` con `token_use=mfa_challenge`, rate limit `AuthStrictPolicy` y lockout tras 5 fallos (`VerifyMfaCommand.cs`, `MfaEndpointsTests.cs`) | Bajo |
| **T — Tampering** | Falsificación de claim `amr=mfa` o `mfa_at` en el JWT | Firma HMAC-SHA256 validada en `JwtBearer`; tiempo validado contra `IDateTimeProvider.UtcNow` (`SecurityExtensions.cs`) | Bajo |
| **R — Repudiation** | Ejecución administrativa o intento fallido de MFA sin traza | Auditoría de solo adición vía `IModerationAuditRecorder` con `ModerationTargetType.AdminMfa` y `CorrelationId` (`VerifyMfaCommand.cs`) | Bajo |
| **I — Information Disclosure** | Fuga del secreto TOTP o códigos de recuperación desde MongoDB o logs | Secreto cifrado con `AesGcm`, códigos guardados solo por SHA-256, `ISensitiveRequest` bloquea log de payloads (`ArchitectureTests.cs`) | Bajo |
| **D — Denial of Service** | Fuerza bruta masiva contra `/auth/mfa/verify` | Rate limiting `AuthStrictPolicy` y bloqueo temporal (`LockoutMinutes=15`) por cuenta (`VerifyMfaCommand.cs`) | Bajo |
| **E — Elevation of Privilege** | Uso de `mfaChallengeToken` o JWT sin `amr=mfa` en rutas de Admin o usuario normal | Rechazo explícito de `token_use=mfa_challenge` en políticas generales y validación `amr=mfa` + recencia en `AdminOnlyPolicy` (`SecurityExtensions.cs`, `MfaEndpointsTests.cs`) | Bajo |

### Decisión provisional del ejecutor — revisar con Rodrigo

- **Step-up estricto en `AdminOnlyPolicy`**: Todas las rutas bajo `AdminOnlyPolicy` exigen `amr=mfa` y `mfa_at` reciente (`≤ 15 min`). Los endpoints `/api/v1/auth/mfa/enroll` y `/api/v1/auth/mfa/verify` utilizan políticas específicas para permitir el primer enrolamiento de un administrador antes de tener `amr=mfa`, y el bloqueo de `token_use=mfa_challenge` impide usar el token intermedio de desafío en cualquier otra ruta. Alternativa descartada: eximir de `amr=mfa` a administradores sin 2FA activo (dejaría el rol `Administrador` vulnerable sin segundo factor).

## 5. Observabilidad y rendimiento

- Eventos auditables con `ModerationTargetType.AdminMfa` y `CorrelationId`, sin PII ni códigos.

## 6. Costo AWS

Cero adicional: código en Lambda existente, almacenamiento embebido en MongoDB M0 y secreto opcional `/sensus/<env>/Mfa/EncryptionKey` en SSM Parameter Store Free Tier.

## 7. Estrategia de pruebas

- `TotpServiceTests`: 6 vectores oficiales RFC 6238 SHA-1, tolerancia ±1 paso, anti-reuso en mismo paso, cifrado/descifrado AES-GCM y códigos de recuperación de un solo uso.
- `MfaHandlersTests`: enrolamiento, activación al primer `verify`, desafío en `login`, rechazo por código inválido, bloqueo al 5º fallo y recuperación con código de respaldo.
- `MfaEndpointsTests` (integración HTTP): ciclo completo `login -> challenge -> verify -> AdminOnly 200 OK`, rechazo `403` sin `amr=mfa` o con `mfa_at` expirado, rechazo de `mfaChallengeToken` en `/auth/me` y bloqueo tras 5 fallos.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-062-01`..`05` y `NFR-062-01`..`02` definidos |
| II | PASS | Clean Architecture verificada con `ArchitectureTests` |
| III | PASS | CQRS estricto (`EnrollMfaCommand`, `VerifyMfaCommand`) y OpenAPI v1.0.0 |
| IV | PASS | AES-256-GCM, SHA-256 recovery codes, lockout y step-up `amr=mfa` |
| V | PASS | Vectores RFC 6238, unitarias e integración |
| VI | PASS | Sin paquetes pagos ni nuevos servicios AWS |
| VII | PASS | Auditoría append-only sin PII ni códigos |
| VIII | PASS | STRIDE completo y cierre de brecha `G-05` en código |

## 9. Riesgos de implementación

- El secreto `/sensus/prod/Mfa/EncryptionKey` debe configurarse en AWS SSM antes de que un administrador use `/auth/mfa/enroll` en producción (pendiente de operación de Rodrigo).

## 10. Orden de implementación

1. `T062-01` Definir contrato OpenAPI y modelo `UserMfaState` en Domain
2. `T062-02` Implementar `Rfc6238TotpService` (RFC 6238 `HMACSHA1` + `AesGcm`) y pruebas de vectores RFC
3. `T062-03` Implementar `EnrollMfaCommand`, `VerifyMfaCommand`, desafío en `LoginCommandHandler` y auditoría
4. `T062-04` Aplicar `AdminOnlyPolicy` con `amr=mfa` + recencia y exponer `/auth/mfa/enroll` y `/auth/mfa/verify`
5. `T062-05` Actualizar tests administrativos y agregar `MfaEndpointsTests`
