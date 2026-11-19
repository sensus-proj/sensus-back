# Plan 020 — Sanciones y baneo de usuario

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 2.1.0 |
| Constitution Check | PASS CON OBSERVACIONES |
| Alineado a | `constitution.md` v2.3.0 · `AGENTS.md` v2.3.0 |

## 1. Resumen técnico

La sanción es un agregado propio (`Sanction`) en la colección `sanctions`, que es la fuente de verdad y el
registro histórico. Para que el bloqueo sea inmediato y sin consultas extra, el usuario conserva una
**suspensión denormalizada** (`SanctionId` y `EndsAtUtc`, nula si es permanente) que ya viaja en el documento
que `Login` y `OnTokenValidated` cargan en cada solicitud.

Al aplicar una sanción se rota el `SecurityStamp` (mecanismo de la spec 004), de modo que los tokens
emitidos antes quedan inválidos. Al revertir se limpia la suspensión; el usuario inicia sesión de nuevo.

```
UploadMediaCommandHandler ──(Rejected)──► ApplySanctionCommand ──► Sanction + User.Suspend()
Admin ──► POST /moderation/sanctions/{id}/revert ──► RevertSanctionCommand ──► Sanction.Revert() + User.Lift()
Login / JWT OnTokenValidated ──► User.IsSuspended(now) ──► 403 Auth.UserSanctioned / 401
```

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `Sanction` (estados `Active`, `Expired`, `Reverted`; tipo `Automatic` o `Manual`; vigencia con `StartsAtUtc` y `EndsAtUtc`); `SanctionEvidence` (objeto de valor sin contenido ni PII); `User.Suspend`, `User.LiftSuspension`, `User.IsSuspended(nowUtc)`; política de escalamiento pura `SanctionEscalationPolicy` |
| Application | Comandos `ApplySanction`, `CreateManualSanction` y `RevertSanction`; consultas `ListSanctions` y `GetSanction`; DTOs; `SanctionSettings`; `SanctionNotifier` (push, FR-020-08); mapeo explícito. `ISanctionRepository` vive en Domain |
| Infrastructure | `MongoSanctionRepository` e índices; enlace de opciones `Moderation:Sanctions`; métricas EMF; notificación push mediante `IPushDispatcher` |
| Api | `SanctionEndpoints` (4 rutas bajo `/api/v1/moderation/sanctions`), política `AdminOnly`, rate limits; mapeo de `Auth.UserSanctioned` a 403 con `sanctionEndsAtUtc`; comprobación en `OnTokenValidated` |
| Pruebas | Unitarias del dominio y la política; integración con MongoDB real |
| IaC | Sin cambios: no hay recursos nuevos |

### Puntos de integración existentes

| Archivo | Cambio |
|---|---|
| `Users/Commands/Login/LoginCommandHandler.cs` | Tras validar `Estatus`, rechazar si `user.IsSuspended(now)` con `Error.Forbidden("Auth.UserSanctioned", ...)` |
| `Sensus.Api/Program.cs` (`OnTokenValidated`) | Hacer fallar el token si `user.IsSuspended(now)` |
| `Media/Commands/UploadMedia/UploadMediaCommandHandler.cs` | Si la decisión es `Rejected` y `Moderation:Sanctions:Enabled`, enviar `ApplySanctionCommand` (sin que un fallo de la sanción rompa la respuesta de la subida) |

## 3. Modelo de datos e índices

Colección `sanctions`:

| Campo | Descripción |
|---|---|
| `_id` | GUID |
| `userId` | Usuario sancionado |
| `type` | `Automatic` o `Manual` |
| `status` | `Active` o `Reverted` (`Expired` se deriva de `endsAtUtc`, no se escribe) |
| `reason` | Texto corto, sin PII |
| `evidence` | `{ mediaId, decision, categories[], scores{}, thresholdApplied, action }` |
| `appliedBy` | `system` o GUID del administrador |
| `startsAtUtc`, `endsAtUtc` | `endsAtUtc` nula = permanente |
| `revertedBy`, `revertedAtUtc`, `revertReason` | Solo si se revierte |

Índices (justificación obligatoria, `AGENTS.md` §9):

| Índice | Consulta que lo exige |
|---|---|
| `{ userId: 1, startsAtUtc: -1 }` | Escalamiento por reincidencia y listado filtrado por usuario |
| `{ status: 1, startsAtUtc: -1 }` | Listado filtrado por estado, paginado |
| `{ startsAtUtc: -1 }` | Listado general ordenado |

El usuario añade `suspension: { sanctionId, endsAtUtc? }`. No requiere índice: se lee por `_id`.

## 4. Seguridad

- Solo `Administrador` consulta, crea manualmente y revierte (política `AdminOnly`). La sanción automática no
  tiene superficie HTTP.
- Un administrador no puede sancionarse a sí mismo ni a otro administrador (CA-020-10).
- El `ProblemDetails` del login no incluye evidencia, categorías ni puntajes.
- Los logs registran `sanctionId`, `userId` y tipo; nunca el motivo libre, el correo ni el contenido.
- Fail-closed intacto: la sanción no altera la regla de que un fallo de moderación manda a cuarentena
  (D-3); y un fallo al aplicar la sanción no publica el contenido.
- Auditoría: cada sanción y cada reversión conservan responsable y fecha; alimenta la spec 021.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Un usuario o un token sin rol invoca los endpoints de sanción | Elevación de privilegios | Política `AdminOnly` en el borde y verificación del rol en cada caso de uso (defensa en profundidad); 401/403 con pruebas por endpoint | Bajo |
| Un administrador se sanciona a sí mismo o a otro administrador, o la moderación sanciona a un administrador | Elevación de privilegios y denegación de servicio | Regla de objetivos en dominio y casos de uso (CA-020-10); la sanción automática omite a los administradores | Bajo |
| Un atacante enumera cuentas o descubre cuáles están sancionadas | Divulgación de información | El login valida la contraseña antes de revelar la sanción; error uniforme 401 (CA-020-12) | Bajo |
| Se reutiliza un token emitido antes de la sanción | Suplantación | Rotación del `SecurityStamp` y comprobación de la suspensión en cada solicitud (NFR-020-01) | Bajo |
| Un administrador revierte o altera sanciones sin rastro | Repudio | Registro de solo adición (sin borrado en el repositorio), responsable, fecha y motivo en cada reversión; log de auditoría con `correlation-id` | Medio: falta un segundo factor para administradores (brecha G-05) |
| Un falso positivo de moderación sanciona a un inocente | Denegación de servicio | Escalamiento progresivo desde 24 h, `Quarantine` no sanciona, reversión administrativa y notificación push | Medio |
| El texto de la sanción o la evidencia filtran PII o contenido | Divulgación de información | La evidencia solo guarda metadatos técnicos; logs sin motivo libre; el 403 no incluye evidencia | Bajo |
| Reintentos de moderación generan sanciones duplicadas | Manipulación | Idempotencia por activo con índice único parcial y reparación de la suspensión | Bajo |
| Falla de MongoDB o del push al sancionar | Denegación de servicio | La falla de la sanción no publica el contenido (fail-closed); la falla del push se registra sin impedir el bloqueo | Bajo |

## 5. Observabilidad y rendimiento

- Métricas `SanctionsApplied` (dimensión `type`) y `SanctionsReverted` en el espacio `Sensus/Api`, con la
  infraestructura de la spec 015.
- Sin consultas adicionales en la ruta caliente: la suspensión viaja en el documento del usuario que ya se
  carga (NFR-020-01).
- Listados paginados 20 por defecto y 100 máximo, con `CancellationToken` hasta la base (NFR-020-04).

## 6. Costo AWS

Sin costo incremental: sin recursos nuevos; Lambda, API Gateway y MongoDB dentro de lo ya aprovisionado.

## 7. Estrategia de pruebas

| Tipo | Cobertura |
|---|---|
| Unitarias de dominio | Vigencia (activa, vencida, permanente, revertida), transiciones de estado, `IsSuspended` en el límite exacto |
| Unitarias de la política | Cada escalón (24 h, 7 d, 30 d, permanente); ventana de 90 días; las revertidas no cuentan |
| Unitarias de handlers | Aplicar, revertir (ya revertida, inexistente), sanción manual, objetivo administrador, `Quarantine` sin sanción |
| Integración (MongoDB real) | Flujo completo: subida `Rejected` → bloqueo de login 403 → token previo 401 → reversión → acceso restaurado; 401/403 por endpoint; paginación; expiración perezosa |
| Fallos | Doble que simula falla del repositorio al sancionar: la subida sigue devolviendo la decisión de moderación |
| Regresión | Login de usuario no sancionado sin cambios |
| Contrato | El OpenAPI coincide con las rutas implementadas |

## 8. Constitution Check

Evaluación contra `constitution.md` v2.3.0.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-020-01` a `FR-020-07` y 4 NFR con criterios de aceptación medibles (CA-020-01 a CA-020-10) |
| II | PASS | Cambios acotados a las capas de §2; Domain sin dependencias externas |
| III | PASS | Comandos y consultas separados; DTOs, sin exponer entidades ni documentos de MongoDB |
| IV | PASS | §4: autorización en servidor, sin PII en logs, umbrales y duraciones en configuración, fail-closed |
| V | PASS | §7: unitarias, integración con MongoDB real y dobles de fallo |
| VI | PASS | §6: sin costo incremental, sin recursos nuevos |
| VII | PASS | §3 y §5: índices declarados, paginación y métricas |
| VIII | PASS CON OBSERVACIONES | Modelo de amenazas en §4, ASVS L3 en autorización y sesiones, auditoría de solo adición, sin enumeración. Observación: el segundo factor para administradores sigue abierto en la brecha G-05 del Apéndice F (vence 2027-01-29) |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Falso positivo de moderación que sanciona a un usuario inocente: mitigado por el escalamiento y la reversión.
- La denormalización puede desincronizarse de `sanctions`: mitigado aplicando ambos cambios en un único
  caso de uso, con prueba de consistencia, y reconstruible desde la colección.
- El hook en `UploadMediaCommandHandler` no debe convertir un fallo de sanción en un fallo de subida.

## 10. Orden de implementación

1. `T020-01` Entidad `Sanction`, estados y vigencia (Domain)
2. `T020-02` Suspensión en `User` (Domain)
3. `T020-03` Política de escalamiento y `SanctionSettings` (Domain y Application)
4. `T020-04` Pruebas unitarias del dominio y la política
5. `T020-05` Comando `ApplySanction`
6. `T020-06` Comando `RevertSanction`
7. `T020-07` Comando `CreateManualSanction`
8. `T020-08` Consultas `ListSanctions` y `GetSanction`
9. `T020-09` Repositorio MongoDB e índices
10. `T020-10` Bloqueo en login y en validación de token
11. `T020-11` Enlace desde `UploadMediaCommandHandler`
12. `T020-12` Endpoints, rate limits y manejo de errores
13. `T020-13` Métricas y logs sin PII
14. `T020-14` Pruebas de integración del flujo completo
15. `T020-15` Actualizar OpenAPI de las specs 020 y 002
16. `T020-16` Actualizar `specs/README.md`, `requirements.md` y el checklist
17. `T020-17` Registrar evidencias del Constitution Check
