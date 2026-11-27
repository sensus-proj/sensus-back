# Checklist 002 — Inicio de sesión

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 002 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 001
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El token incluye todos los claims declarados (`AuthEndpointsTests`, `JwtTokenGenerator`)
- [x] El mensaje de error es idéntico para correo y contraseña inválidos (`AuthEndpointsTests`)
- [x] No se registran credenciales en logs

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`Sensus.ArchitectureTests` 6/6)
- [x] Pruebas unitarias y de integración añadidas y en verde (`LoginCommand*Tests`, `GetCurrentUserQueryHandlerTests`, `AuthEndpointsTests`, `AuthMeEndpointTests`, `RefreshTokenEndpointsTests`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado (`CurrentUserDto` + `RefreshToken`)
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica; SSM en 045)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [ ] Rotación operativa del secreto JWT en SSM tras retiro de `appsettings.json` (acción del usuario: rotar valor `/sensus/prod/JwtSettings/Secret` en SSM; ver plan cierre §5.Z-5)
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS (`CurrentUserDto` en `/auth/me`)
- [x] **IV. Seguridad y privacidad** — PASS (`appsettings.json` sin secreto; JWT ≤ 15 min + refresh rotatorio)
- [x] **V. Verificación automatizada** — PASS (`AuthEndpointsTests`, `AuthMeEndpointTests`, `RefreshTokenEndpointsTests`, `SanctionEndpointsTests`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS (`correlation-id`, TTL `refresh_tokens`)
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (STRIDE en `plan.md` §4 y escaneos CI bloqueantes verdes `ci.yml`/`dast-zap.yml`, `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Users/` (`LoginCommand*`, `GetCurrentUserQueryHandlerTests`) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Authentication/` (`AuthEndpointsTests`, `AuthMeEndpointTests`, `RefreshTokenEndpointsTests`) | ☑ |
| Contrato OpenAPI | `specs/002-inicio-sesion/contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Antigravity | T002-10 completada (`G-06`) | `JwtSettings:ExpiryMinutes` restringido a `[1, 15]` (default `15`), `RefreshToken` rotatorio con índice único `ux_refresh_tokens_token_hash` + TTL `ttl_refresh_tokens_expires_at`, endpoint `POST /api/v1/auth/refresh` con detección de reuso (revoca familia + `SecurityStamp`), revocación en `LogoutCommandHandler` y `TokenRefreshAuthenticator` + `SecurePreferences` en móvil `movil_Rodrigo`. Verificado en integración (`22/22`) y Testcontainers MongoDB. |
| 2026-11-26 | Ejecutor cierre specs | T002-09 completada (`FR-002-07`) | Reemplazado objeto anónimo de `/api/v1/auth/me` por `CurrentUserDto` y consulta CQRS `GetCurrentUserQuery` (`ICurrentUserService.FullName`). Esquema `CurrentUserDto` en `contracts/openapi.yaml`. Unitarias `GetCurrentUserQueryHandlerTests` (`3/3`), integración `AuthMeEndpointTests` (`3/3`) y `OpenApiContractTests` (`1/1`) en verde. |
