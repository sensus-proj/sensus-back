# Plan 060 — Baja lógica de cuenta y purga diferida

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Cerrado |
| Versión | 2.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Resumen técnico

Sustituir el borrado físico inmediato por un estado de baja lógica. `User` y `UserDocument` incorporan `DeletedAtUtc`; `MarkAsDeleted` fija la marca, desactiva la cuenta y rota el `SecurityStamp`. La validación JWT (`OnTokenValidated`) y `LoginCommandHandler` rechazan cuentas dadas de baja. Un hosted service similar a `QuarantineRetentionBackgroundService` envía `PurgeDeletedAccountsCommand` con un `cutoff` calculado desde `AccountDeletion:RetentionDays`; el handler elimina solo los documentos vencidos y emite auditoría estructurada sin PII.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `User.DeletedAtUtc`, `User.IsDeleted`, `User.MarkAsDeleted(nowUtc)`, `User.RestoreDeletion(...)`; índice de consulta por `deletedAtUtc` |
| Application | `IUserRepository.MarkAsDeletedAsync` y `GetIdsDeletedBeforeAsync`; `PurgeDeletedAccountsCommand` y handler |
| Infrastructure | Implementación Mongo de los nuevos métodos; índice `ix_users_deleted_at`; auditoría estructurada vía `ILogger` y `ICorrelationIdAccessor` |
| Api | `OnTokenValidated` rechaza `IsDeleted`; registro del hosted service de purga; configuración `AccountDeletion` |

## 3. Modelo de datos e índices

- `users.deletedAtUtc`: nulo para cuentas activas; fecha UTC para dadas de baja.
- Índice `ix_users_deleted_at` sobre `deletedAtUtc` con filtro parcial por existencia, para que la consulta de purga no recorra la colección completa.
- El índice único de `correo` permanece; la PII se conserva hasta la purga (supuesto documentado en la spec).
- La purga elimina el documento completo; no hay TTL automático en `users` porque la retención debe ser auditable y controlada por el proceso.

## 4. Seguridad y modelo de amenazas (STRIDE, Principio VIII)

Nivel ASVS 5.0 3 en autenticación, sesiones y eliminación de cuenta.

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Token robado sigue vigente tras la baja | Spoofing / Sesiones | Rotación de `SecurityStamp` + rechazo explícito de `IsDeleted` en `OnTokenValidated` | Bajo (el token se rechaza en la primera petición) |
| Enumeración de cuentas por el login | Information disclosure | Mismo `Auth.InvalidCredentials` para correo inexistente y cuenta dada de baja | Bajo |
| Baja no autorizada | Elevation of privilege | `RegisteredUserPolicy` + reautenticación con contraseña + `SensitiveWritePolicy` | Bajo |
| PII en logs o auditoría | Information disclosure | Solo `UserId`, resultado y `correlation-id`; nunca correo, nombre, hash ni foto | Bajo |
| Purga de cuentas activas por error de filtro | Tampering / Disponibilidad | `cutoff` calculado con reloj inyectado y filtro `deletedAtUtc <= cutoff` en la base de datos; prueba de cuenta activa intacta | Bajo |
| Reintentos concurrentes | Denial of service | Eliminación por `_id`, idempotente; auditoría emitida solo cuando el borrado afectó a un documento | Bajo |

## 5. Observabilidad y rendimiento

- Eventos `SecurityAudit AccountDeletionRequested` y `SecurityAudit DeletedAccountPurged` con `UserId`, resultado y `CorrelationId`; sin PII.
- `NFR-060-01`: la denegación ocurre en la validación del token, sin llamadas externas; se verifica con una petición inmediata.
- `NFR-060-02`: la purga consulta por índice y procesa en lotes; se verifica con reloj simulado en pruebas unitarias y de integración.
- Retención y frecuencia en configuración (`AccountDeletion:RetentionDays`, `AccountDeletion:PurgeIntervalHours`).

## 6. Costo AWS

Sin costo incremental: la purga reutiliza el proceso `BackgroundService` dentro de la misma Lambda/API existente, sin EventBridge ni recursos nuevos. La alternativa de programar una regla de EventBridge se descarta por costo y complejidad; queda documentada como opción futura. No se ejecuta `cdk deploy`.

## 7. Estrategia de pruebas

- Unitarias: transición `MarkAsDeleted`, rotación de stamp, `PurgeDeletedAccountsCommandHandler` con repositorio simulado (cuenta vencida, cuenta activa, idempotencia) y `LoginCommandHandler` con cuenta dada de baja.
- Integración HTTP: baja `204`, token anterior rechazado, login `401` genérico y documento persistido con `DeletedAtUtc`.
- Integración de purga: repositorio Mongo simulado con reloj fijo, verificando el `cutoff` y que no se purgan activas.
- Regresión: una prueba que fallaba antes del arreglo (token válido tras la baja) se añade y pasa.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Cuatro artefactos con `FR-060-01` a `FR-060-06` y `NFR-060-01` a `NFR-060-04`, criterios medibles |
| II. Clean Architecture | PASS | Estado en Domain, casos de uso en Application, Mongo en Infrastructure, composición en Api; test de arquitectura en verde |
| III. CQRS y contratos | PASS | Sin endpoints nuevos; comando de purga separado del comando de baja; sin entidades de dominio en respuestas |
| IV. Seguridad y privacidad | PASS | STRIDE en §4, ASVS nivel 3, rotación de stamp, sin PII en logs ni respuestas |
| V. Verificación automatizada | PASS | Pruebas unitarias y de integración con cobertura ≥ 80 % en Domain y Application |
| VI. Costo cero e IaC | PASS | Sin recursos nuevos; reutilización del hosted service existente; sin `cdk deploy` |
| VII. Rendimiento y observabilidad | PASS | Bloqueo < 1 s y purga trazable con índice y auditoría |
| VIII. DevSecOps de nivel fintech | PASS | Auditoría de solo adición con actor/objetivo/resultado/correlación, sin secretos ni PII, validación de borde y lista de permitidos |

## 9. Riesgos de implementación

- El índice parcial de `deletedAtUtc` debe crearse en cada arranque; se usa el patrón existente de `MongoDbContext.EnsureIndexesAsync`.
- Un reintento concurrente puede emitir dos eventos de auditoría; se usa el resultado de `DeleteOneAsync` para registrar solo eliminaciones efectivas.
- La retención se lee de configuración: si falta la clave se usa 30 días por defecto, documentado en `appsettings.json`.

## 10. Orden de implementación

1. Modelar `DeletedAtUtc` y `MarkAsDeleted` en Domain y documento Mongo.
2. Ampliar `IUserRepository` y su implementación.
3. Bloquear login y tokens de cuentas dadas de baja.
4. Sustituir el borrado físico por baja lógica en `DeleteAccountCommandHandler`.
5. Implementar el comando de purga y el hosted service.
6. Añadir pruebas, ejecutar gates y cerrar artefactos.
