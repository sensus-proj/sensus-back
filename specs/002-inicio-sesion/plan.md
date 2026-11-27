# Plan 002 — Inicio de sesión

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Documenta estado actual con brechas |
| Versión | 1.0.0 |
| Constitution Check | FAIL |
| Alineado a | `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0 |

## 1. Resumen técnico

Permitir que un usuario registrado obtenga un token JWT a partir de sus credenciales, sin revelar si el correo existe y respetando el estado de la cuenta. Este plan documenta la implementación existente en la rama actual y las brechas detectadas frente a la constitución.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Application | LoginCommand, validador y handler |
| Infrastructure | JwtTokenGenerator y lectura por correo con índice único |
| Api | Endpoint POST /api/v1/auth/login |

## 3. Modelo de datos e índices

- users; lectura por email usando el índice único

## 4. Seguridad

- Comparación BCrypt
- Mensaje genérico sin filtrar existencia
- Registro de intentos fallidos sin PII
- Límite de tasa más estricto que el resto de la API

### Modelo de amenazas (STRIDE — Principio VIII)

| Amenaza | Escenario | Control existente (con archivo/prueba) | Riesgo residual |
|---|---|---|---|
| **S — Spoofing** | Suplantación por fuerza bruta o reutilización de tokens filtrados | Verificación BCrypt (`PasswordHasher.cs`), validación de firma/emisor/audiencia/expiración (`JwtTokenGenerator.cs`, `JwtSettingsValidatorTests.cs`) y rotación de `SecurityStamp` | Medio-bajo (brecha `G-06` abierta para expiración corta y *refresh*) |
| **T — Tampering** | Alteración de claims del JWT (rol, `sub`, expiración) | Firma simétrica HMAC-SHA256 validada en `Program.cs` (`JwtSettingsValidator.cs`); rechazo si secreto es débil o ausente en `Production` | Bajo |
| **R — Repudiation** | Negación de intentos de inicio de sesión fallidos o exitosos | Logs estructurados con `CorrelationId` sin exponer credenciales ni PII (`LoginCommandHandler.cs`, `LoginCommandHandlerTests.cs`) | Bajo |
| **I — Information Disclosure** | Enumeración de cuentas existentes/eliminadas o fuga de credenciales en logs | Mismo código `Auth.InvalidCredentials` para usuario inexistente, contraseña inválida o cuenta eliminada (`LoginCommandHandler.cs`, `DeleteAccountEndpointTests.cs`) | Bajo |
| **D — Denial of Service** | Ráfagas de login para saturar BCrypt o bloquear cuentas | Rate limiting `AuthStrictPolicy` por IP/solicitud en `AuthEndpoints.cs` (`RateLimitingTests.cs`) y validación de entrada antes del cómputo | Medio-bajo |
| **E — Elevation of Privilege** | Acceso con cuenta suspendida o dada de baja (`Deleted`) | Verificación explícita de estado de usuario y `SecurityStamp` en `LoginCommandHandler.cs` y en `OnTokenValidated` (`Program.cs`, `SecurityPipelineTests.cs`) | Bajo |

## 5. Observabilidad y rendimiento

- Métricas de logins exitosos y fallidos
- Nunca registrar contraseñas ni tokens

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de handler y validador
- Integración de login válido, inválido y suspendido

## 8. Constitution Check

Evaluación del estado actual. Las brechas marcadas como FAIL se convierten en tareas de remediación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-002-01` y 4 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Secreto JWT retirado de `appsettings.json` (`"Secret": ""`) y leído de SSM (Spec 045); vigencia ≤ 15 min y refresh rotatorio (`T002-10`) |
| V | PASS | Unitarias (`LoginCommand*Tests`, `GetCurrentUserQueryHandlerTests` 3/3), integración (`AuthEndpointsTests`, `AuthMeEndpointTests` 3/3, `RefreshTokenEndpointsTests`, `SanctionEndpointsTests`) |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Logs estructurados con `correlation-id` sin credenciales, TTL e índices de `refresh_tokens` |
| VIII | PASS | Matriz STRIDE en §4, JWT ≤ 15 min + rotación refresh y escaneos CI bloqueantes en verde (`ci.yml`, `dast-zap.yml`, `4a00d24`) |

> **Brechas abiertas:** las decisiones `FAIL` no bloquean la documentación de un retro-spec, pero MUST resolverse antes de implementar cualquier funcionalidad que dependa de ella.

## 9. Riesgos de implementación

- Sin rate limiting, el endpoint es vulnerable a fuerza bruta (ver spec 013)

## 10. Orden de implementación

1. `T002-01` Actualizar y validar el contrato OpenAPI del spec
2. `T002-02` Sacar el secreto JWT del repositorio y rotarlo
3. `T002-03` Prueba de integración de login y de mensaje genérico
4. `T002-04` Cubrir el caso de cuenta suspendida
5. `T002-05` Implementar las pruebas de integración del flujo completo
6. `T002-06` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
7. `T002-07` Registrar evidencias del Constitution Check
