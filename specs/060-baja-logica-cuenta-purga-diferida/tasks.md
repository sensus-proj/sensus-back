# Tareas 060 — Baja lógica de cuenta y purga diferida

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Cerrado |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T060-01 | Añadir `DeletedAtUtc`, `IsDeleted`, `MarkAsDeleted` y `RestoreDeletion` a `User` | Domain | `FR-060-01` | — | Implementado |
| T060-02 | Persistir `deletedAtUtc` en `UserDocument` y restaurarlo en `ToDomain` | Infrastructure | `FR-060-01` | T060-01 | Implementado |
| T060-03 | Ampliar `IUserRepository` con `MarkAsDeletedAsync` y `GetIdsDeletedBeforeAsync` | Domain | `FR-060-01` | T060-01 | Implementado |
| T060-04 | Implementar los nuevos métodos en `MongoUserRepository` y el índice `ix_users_deleted_at` | Infrastructure | `FR-060-01` | T060-02, T060-03 | Implementado |
| T060-05 | Rotar `SecurityStamp` en la baja y rechazar tokens en `OnTokenValidated` | Api | `FR-060-02` | T060-01 | Implementado |
| T060-06 | Rechazar el login de cuentas dadas de baja con error genérico | Application | `FR-060-03` | T060-01 | Implementado |
| T060-07 | Sustituir el borrado físico por baja lógica en `DeleteAccountCommandHandler` y registrar auditoría sin PII | Application | `FR-060-01`, `FR-060-05` | T060-03, T060-05 | Implementado |
| T060-08 | Implementar `PurgeDeletedAccountsCommand` y su handler con `cutoff` inyectado | Application | `FR-060-04`, `FR-060-06` | T060-03 | Implementado |
| T060-09 | Registrar `DeletedAccountPurgeBackgroundService` y la configuración `AccountDeletion` | Api | `FR-060-04`, `NFR-060-03` | T060-08 | Implementado |
| T060-10 | Pruebas unitarias de dominio, baja, login, purga, idempotencia y cuenta activa | Pruebas | `FR-060-01` a `FR-060-06` | T060-09 | Implementado |
| T060-11 | Pruebas de integración HTTP (baja 204, token 401, login 401) y de purga con reloj fijo | Pruebas | `NFR-060-01`, `NFR-060-02` | T060-10 | Implementado |
| T060-12 | Gates (build, formato, suites, cobertura) y cierre documental | Docs | `FR-060-05` | T060-11 | Implementado |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral.
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- La purga en cascada ya implementada en la spec 030 permanece inmediata; esta spec solo difiere la eliminación del documento de usuario.
