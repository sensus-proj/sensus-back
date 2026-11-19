# Tareas 020 — Sanciones y baneo de usuario

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v2.3.0 · `AGENTS.md` v2.3.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T020-01 | Entidad `Sanction`: estados, tipo, evidencia y reglas de vigencia | Domain | `FR-020-03` | — | Hecho |
| T020-02 | `User.Suspend`, `User.LiftSuspension` y `User.IsSuspended(nowUtc)` con rotación de `SecurityStamp` | Domain | `FR-020-04` | T020-01 | Hecho |
| T020-03 | `SanctionEscalationPolicy` y `SanctionSettings` (`Moderation:Sanctions`) | Domain | `FR-020-06` | T020-01 | Hecho |
| T020-04 | Pruebas unitarias de vigencia, estados y escalamiento | Pruebas | `FR-020-06` | T020-03 | Hecho |
| T020-05 | Comando `ApplySanction` (automática, excluye `Quarantine`, idempotente) | Application | `FR-020-01` | T020-04 | Hecho |
| T020-06 | Comando `RevertSanction` con motivo obligatorio | Application | `FR-020-02` | T020-04 | Hecho |
| T020-07 | Comando `CreateManualSanction` y regla de objetivos permitidos | Application | `FR-020-07` | T020-04 | Hecho |
| T020-08 | Consultas `ListSanctions` y `GetSanction` con paginación | Application | `FR-020-05` | T020-04 | Hecho |
| T020-09 | `MongoSanctionRepository` e índices (`userId+startsAtUtc`, `startsAtUtc`, `evidence.mediaId` único parcial) | Infrastructure | `NFR-020-04` | T020-05 | Hecho |
| T020-10 | Bloqueo en `LoginCommandHandler` y en `OnTokenValidated` | Api | `FR-020-04` | T020-02 | Hecho |
| T020-11 | Enlazar la decisión `Rejected` de `UploadMediaCommandHandler` con `ApplySanction` sin romper el veredicto | Application | `FR-020-01` | T020-05 | Hecho |
| T020-12 | `SanctionEndpoints`, política `AdminOnly`, rate limits y mapeo de `Auth.UserSanctioned` | Api | `FR-020-05` | T020-06, T020-07, T020-08, T020-09 | Hecho |
| T020-13 | Métricas `SanctionsApplied` y `SanctionsReverted`; logs sin PII | Infrastructure | `NFR-020-03` | T020-12 | Hecho |
| T020-14 | Pruebas de integración: flujo completo, 401 y 403, paginación, expiración y persistencia en MongoDB real | Pruebas | `FR-020-01` | T020-10, T020-11, T020-12 | Hecho |
| T020-15 | Verificar `NFR-020-01` (bloqueo en la siguiente solicitud) y `NFR-020-02` (reversión sin pérdida de datos) | Pruebas | `NFR-020-01` | T020-14 | Hecho |
| T020-16 | Actualizar OpenAPI de la spec 020 y la respuesta 403 de `/auth/login` | Docs | `FR-020-04` | T020-12 | Hecho (la respuesta 403 de la spec 002 queda anotada en el OpenAPI de la 020) |
| T020-17 | Actualizar `specs/README.md`, `requirements.md`, `memory.md` y el checklist | Docs | `FR-020-01` | T020-15, T020-16 | Hecho |
| T020-18 | Registrar evidencias del Constitution Check | Docs | `FR-020-01` | T020-17 | Hecho |
| T020-19 | `SanctionNotifier`: push al aplicar y al revertir, sin evidencia y sin romper el bloqueo si falla | Application | `FR-020-08` | T020-05, T020-06 | Hecho |
| T020-20 | Modelo de amenazas STRIDE y `NFR-020-05` (no enumeración y autorización en dos capas) | Docs y pruebas | `NFR-020-05` | T020-10 | Hecho |

## Cobertura de requisitos

| Requisito | Tareas |
|---|---|
| `FR-020-01` | T020-05, T020-11, T020-14 |
| `FR-020-02` | T020-06, T020-14 |
| `FR-020-03` | T020-01 |
| `FR-020-04` | T020-02, T020-10, T020-16 |
| `FR-020-05` | T020-08, T020-12 |
| `FR-020-06` | T020-03, T020-04 |
| `FR-020-07` | T020-07 |
| `FR-020-08` | T020-19 |
| `NFR-020-01` | T020-15 |
| `NFR-020-02` | T020-15 |
| `NFR-020-03` | T020-13 |
| `NFR-020-04` | T020-09 |
| `NFR-020-05` | T020-20 |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §5 y §6.
- T020-11 garantiza que un fallo al sancionar no rompa la respuesta de moderación de la subida.
- Las pruebas de `MongoSanctionRepositoryTests` se ejecutan cuando `MONGO_CONNECTION_STRING` está definida (el job de integración de CI la define).
