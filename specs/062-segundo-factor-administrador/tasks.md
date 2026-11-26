# Tareas 062 — Segundo factor TOTP para Administrador

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T062-01 | Documentar contrato OpenAPI e invariants de `UserMfaState` + `ModerationTargetType.AdminMfa` | Domain / Docs | `FR-062-01`, `FR-062-02` | — | Completada |
| T062-02 | Implementar `Rfc6238TotpService` (`HMACSHA1` sin `Otp.NET` + `AesGcm` + recovery codes SHA-256) y `TotpServiceTests` con vectores RFC 6238 | Infrastructure / Pruebas | `FR-062-01`, `FR-062-02` | T062-01 | Completada |
| T062-03 | Implementar `EnrollMfaCommand`, `VerifyMfaCommand`, desafío en `LoginCommandHandler` y auditoría `IModerationAuditRecorder` | Application / Infrastructure | `FR-062-03`, `FR-062-04` | T062-02 | Completada |
| T062-04 | Endurecer `AdminOnlyPolicy` (`amr=mfa` + recencia) y mapear `POST /api/v1/auth/mfa/enroll` y `POST /api/v1/auth/mfa/verify` en `AuthEndpoints` | Api | `FR-062-03`, `FR-062-05` | T062-03 | Completada |
| T062-05 | Ejecutar pruebas unitarias, integración (`MfaEndpointsTests`, suites Admin) y cerrar `G-05` en código | Pruebas / Docs | `FR-062-05`, `NFR-062-01` | T062-04 | Completada |

## Notas de ejecución

- Prohibido agregar `Otp.NET`; toda la matemática RFC 6238 usa `HMACSHA1` nativo de .NET.
