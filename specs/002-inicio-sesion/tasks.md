# Tareas 002 — Inicio de sesión

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado (rotación operativa JWT en SSM en §5.Z-5) |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T002-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-002-01` | — | Completada (`specs/002-inicio-sesion/contracts/openapi.yaml`; `OpenApiContractTests` 1/1) |
| T002-02 | Sacar el secreto JWT del repositorio y rotarlo | Seguridad | `FR-002-02` | T002-01 | Completada en código/SSM (`appsettings.json:16` `"Secret": ""`, `JwtSettingsValidator`, `SecretsConfigurationExtensions.cs`; rotación del valor en SSM: ver plan cierre §5.Z-5) |
| T002-03 | Prueba de integración de login y de mensaje genérico | Api | `FR-002-03` | T002-02 | Completada (`AuthEndpointsTests.Login_WhenValid_Returns200WithStandardClaims_AndUnknownEmailMatchesWrongPasswordError`) |
| T002-04 | Cubrir el caso de cuenta suspendida | Application | `FR-002-05` | T002-03 | Completada (`SanctionEndpointsTests` + `SanctionHandlersTests`) |
| T002-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-002-01` | T002-04 | Completada (`AuthEndpointsTests`, `AuthMeEndpointTests`, `RefreshTokenEndpointsTests`) |
| T002-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-002-01` | T002-05 | Completada |
| T002-07 | Registrar evidencias del Constitution Check | Docs | `FR-002-01` | T002-06 | Completada (`plan.md` §4 STRIDE + CI `4a00d24`) |
| T002-08 | Documentar `GET /auth/me` (retro-spec) y su contrato | Docs | `FR-002-06` | — | Hecho |
| T002-09 | Sustituir el objeto anónimo de `/auth/me` por `CurrentUserDto` con prueba de mapeo | Api | `FR-002-07` | T002-08 | Completada (`CurrentUserDto`, `GetCurrentUserQueryHandler`, `GetCurrentUserQueryHandlerTests` 3/3 y `AuthMeEndpointTests` 3/3) |
| T002-10 | Reducir vigencia JWT a ≤ 15 min (`JwtSettingsValidator`), implementar `RefreshToken` rotatorio en MongoDB (`ux_refresh_tokens_token_hash`, TTL `ttl_refresh_tokens_expires_at`), endpoint `POST /api/v1/auth/refresh` con detección de reutilización, revocación en `/auth/logout` e interceptor `TokenRefreshAuthenticator` en móvil `movil_Rodrigo` (`G-06`) | Domain / Application / Infrastructure / Api / Móvil / Pruebas | `FR-002-02` | — | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
