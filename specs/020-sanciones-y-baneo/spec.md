# Spec 020 — Sanciones y baneo de usuario

| Campo | Valor |
|---|---|
| ID | 020 |
| Slug | `sanciones-y-baneo` |
| Estado | Implementado |
| Ola | 4 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 004, 017, 018, 019, 027 |
| Versión | 2.1.0 |
| Alineado a | `constitution.md` v2.3.0 · `AGENTS.md` v2.3.0 |

---

## 1. Contexto y problema

Aplicar sanciones **proporcionales y reversibles** a los usuarios que publican contenido de riesgo alto, y
bloquear su acceso de forma verificable.

Hoy la moderación (specs 017 a 019) decide `Approved`, `Quarantine` o `Rejected` sobre cada imagen, pero la
decisión no tiene consecuencia sobre la cuenta: un usuario puede subir contenido rechazado de forma
repetida. La infraestructura de acceso ya existe y esta spec la reutiliza:

- El rol `Administrador` y la política `AdminOnly` (spec 019, `POST /media/{id}/release`).
- `LoginCommandHandler` ya devuelve 403 `Auth.UserDeactivated` para cuentas con `Estatus = false`.
- La validación del JWT (`Program.cs`, `OnTokenValidated`) ya carga al usuario en **cada solicitud** y
  compara el `SecurityStamp`, lo que permite un bloqueo inmediato sin consultas adicionales.

La sanción **no** reutiliza `Estatus`: ese campo significa cuenta desactivada o eliminada (spec 030) y
mezclarlo impediría revertir una sanción sin reactivar cuentas borradas.

## 2. Alcance

**Incluye**

- Sanción automática cuando la moderación emite una decisión de **banda alta** (`Rejected`).
- Escalamiento proporcional por reincidencia, configurable (sin valores en código).
- Sanción manual por un administrador (incluye baneo permanente).
- Reversión de una sanción por un administrador, con motivo.
- Registro de motivo, evidencia, responsable y vigencia de cada sanción.
- Bloqueo de login y de tokens vigentes mientras la sanción esté activa.
- Alerta **push** al usuario al aplicarse la sanción y al restablecerse su acceso (decisión del responsable, D-7).
- Consulta paginada de sanciones para administradores.

**No incluye**

- Apelaciones formales por parte del usuario (la reversión administrativa es el único camino en v1).
- Sanciones económicas.
- Notificación por correo electrónico al usuario sancionado.
- Efectos sobre el contenido ya publicado por el usuario (ofertas, publicaciones) o sobre su comercio.
- Reportes de explotación infantil: se canalizan por vía legal externa y **nunca** se procesan aquí
  (`AGENTS.md` §12).
- Panel de administración (UI).

## 3. Actores

- **Usuario sancionado**: quien recibe la sanción.
- **Sistema de moderación**: origina las sanciones automáticas (responsable `system`).
- **Administrador**: aplica, consulta y revierte sanciones. No existe un rol Moderador separado en v1.

## 4. Decisiones de diseño (resuelven las preguntas abiertas de la v1.1.0)

| # | Pregunta | Decisión | Dónde se configura |
|---|---|---|---|
| D-1 | ¿Cuánto dura una sanción por defecto? | Escalonada por reincidencia en una ventana móvil de 90 días: 1.ª = 24 h, 2.ª = 7 días, 3.ª = 30 días, 4.ª o más = baneo permanente | `Moderation:Sanctions` |
| D-2 | ¿Existe proceso de apelación? | No en v1. Un administrador puede revertir con motivo. Una apelación formal sería una spec nueva | — |
| D-3 | ¿Qué cuenta como "banda alta"? | Solo la decisión explícita `Rejected`. La decisión `Quarantine` (también usada cuando la moderación falla, *fail-closed*) **no** sanciona, para no castigar a un usuario por un fallo del proveedor | `Moderation:Sanctions:TriggerDecision` |
| D-4 | ¿Cómo se bloquea? | Con un estado de suspensión en el propio usuario, que ya se carga en login y en cada validación de token | — |
| D-5 | ¿Quién puede sancionar o revertir? | Solo `Administrador`. Un administrador no puede sancionarse a sí mismo ni a otro administrador | — |
| D-7 | ¿Se alerta al usuario? | Sí: notificación push al aplicarse y al revertirse la sanción, y el acceso queda bloqueado durante el tiempo definido en D-1. Es una comunicación de seguridad transaccional: no depende de las preferencias de alertas del usuario y su mensaje no incluye evidencia ni categorías. Un fallo de entrega se registra y no impide el bloqueo | `IPushDispatcher` (Spec 027 y ADR 0007) |
| D-6 | ¿Cómo expira una sanción temporal? | Por evaluación perezosa de `EndsAtUtc` en cada comprobación; no requiere trabajo programado | — |

Los valores de D-1 son una **propuesta** y se ajustan solo por configuración (constitución, Principio IV:
umbrales fuera del código). Si el responsable prefiere otros, cambia `Moderation:Sanctions`, no la spec.

## 5. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-020-01 | Aplicar una sanción automática cuando la moderación emita una decisión de banda alta (`Rejected`) sobre contenido del usuario | Must |
| FR-020-02 | Permitir a un administrador revertir una sanción activa, con motivo obligatorio | Must |
| FR-020-03 | Registrar en cada sanción su motivo, evidencia (identificador del activo, categorías, puntajes, umbral aplicado y acción) y responsable (`system` o el identificador del administrador) | Must |
| FR-020-04 | Bloquear el acceso del usuario sancionado: login con 403 y rechazo de los tokens ya emitidos | Must |
| FR-020-05 | Permitir a un administrador consultar sanciones con filtros y paginación | Must |
| FR-020-06 | Escalar la duración según el número de sanciones del usuario dentro de la ventana configurada, hasta el baneo permanente | Should |
| FR-020-07 | Permitir a un administrador aplicar una sanción manual, temporal o permanente, con motivo obligatorio | Should |
| FR-020-08 | Notificar por push al usuario cuando se aplica una sanción (con su fecha de fin) y cuando se revierte | Must |

## 6. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-020-01 | Efectividad del bloqueo | < 1 s desde que se aplica hasta que la siguiente solicitud del usuario es rechazada |
| NFR-020-02 | Reversibilidad | Sin pérdida de datos del usuario; la reversión conserva el registro histórico de la sanción |
| NFR-020-03 | Privacidad y observabilidad | 0 PII, tokens o contenido moderado en logs; métricas `SanctionsApplied` y `SanctionsReverted` con `correlation-id` |
| NFR-020-04 | Rendimiento de consulta | Listados paginados (20 por defecto, 100 máximo) sobre índices declarados; sin recorridos completos |
| NFR-020-05 | Seguridad de nivel fintech (Principio VIII) | Sin enumeración de cuentas: el login solo revela la sanción con credenciales válidas; autorización de administrador verificada en el borde y en el caso de uso; registro de auditoría de solo adición |

## 7. Criterios de aceptación

### CA-020-01

- **Dado** una decisión de moderación `Rejected` sobre contenido de un usuario sin sanciones previas
- **Cuando** se procesa la decisión
- **Entonces** se crea una sanción de 24 h, el usuario queda bloqueado y la sanción conserva motivo, evidencia
  y responsable `system`

### CA-020-02

- **Dado** una sanción activa y un administrador autenticado
- **Cuando** la revierte con un motivo
- **Entonces** el usuario puede iniciar sesión de nuevo, la sanción queda en estado `Revertida` con su
  responsable y fecha, y ningún dato del usuario se pierde

### CA-020-03

- **Dado** un usuario con una sanción activa
- **Cuando** intenta iniciar sesión
- **Entonces** recibe `403` `ProblemDetails` con código `Auth.UserSanctioned`, un mensaje claro y la fecha de
  fin (`sanctionEndsAtUtc`, nula si es permanente), sin exponer la evidencia ni las categorías

### CA-020-04

- **Dado** un usuario con un token vigente emitido antes de la sanción
- **Cuando** llama a cualquier endpoint protegido tras aplicarse la sanción
- **Entonces** recibe `401` en menos de 1 s

### CA-020-05

- **Dado** que el usuario acumuló sanciones previas dentro de la ventana de 90 días
- **Cuando** se aplica una nueva sanción automática
- **Entonces** la duración corresponde al escalón siguiente (7 días, 30 días o permanente)

### CA-020-06

- **Dado** una sanción temporal cuya fecha de fin ya pasó
- **Cuando** el usuario intenta iniciar sesión
- **Entonces** el acceso se concede sin intervención de un administrador

### CA-020-07

- **Dado** una decisión `Quarantine`, producto de una falla o timeout de la moderación
- **Cuando** se procesa
- **Entonces** el contenido va a cuarentena y **no** se crea ninguna sanción

### CA-020-11

- **Dado** que se aplica o se revierte una sanción
- **Cuando** el usuario tiene dispositivos activos
- **Entonces** recibe una notificación push sin evidencia ni categorías, y un fallo del proveedor push no impide el bloqueo ni la reversión

### CA-020-12

- **Dado** un usuario sancionado
- **Cuando** intenta iniciar sesión con una contraseña incorrecta
- **Entonces** recibe `401` genérico y no se revela que la cuenta existe ni que está sancionada

### CA-020-08

- **Dado** un llamante sin token o sin rol `Administrador`
- **Cuando** invoca cualquier endpoint de sanciones
- **Entonces** recibe `401` o `403` respectivamente

### CA-020-09

- **Dado** una sanción ya revertida, vencida o inexistente
- **Cuando** un administrador intenta revertirla
- **Entonces** recibe `409` `Sanction.NotActive` o `404` y nada cambia

### CA-020-10

- **Dado** un administrador
- **Cuando** intenta sancionarse a sí mismo o a otro administrador
- **Entonces** recibe `403` `Sanction.TargetNotAllowed` y no se crea la sanción

## 8. Contratos expuestos

Prefijo `/api/v1/moderation/sanctions`. El código de error viaja en `title` del `ProblemDetails`. Autorización: JWT con rol `Administrador` (política `AdminOnly`) y
rate limit `sensitive-write` en las escrituras o `read-heavy` en las lecturas. Contrato completo en
[`contracts/openapi.yaml`](contracts/openapi.yaml).

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| `GET` | `/api/v1/moderation/sanctions` | Lista paginada. Filtros: `userId`, `status`, `page`, `pageSize` | `200`, `401`, `403`, `429` |
| `GET` | `/api/v1/moderation/sanctions/{id}` | Detalle de una sanción | `200`, `401`, `403`, `404` |
| `POST` | `/api/v1/moderation/sanctions` | Sanción manual (FR-020-07) | `201`, `400`, `401`, `403`, `404`, `429` |
| `POST` | `/api/v1/moderation/sanctions/{id}/revert` | Revierte una sanción activa | `200`, `400`, `401`, `403`, `404`, `409`, `429` |

La sanción automática **no** expone endpoint: la invoca el caso de uso de moderación de medios.

Cambio en un contrato existente: `POST /api/v1/auth/login` añade la respuesta `403` `Auth.UserSanctioned`
con la extensión `sanctionEndsAtUtc`. Debe reflejarse en el contrato de la spec 002.

### Configuración (`Moderation:Sanctions`)

| Clave | Valor por defecto propuesto |
|---|---|
| `Enabled` | `true` |
| `TriggerDecision` | `Rejected` |
| `EscalationWindowDays` | `90` |
| `Steps` | `[ { "Hours": 24 }, { "Hours": 168 }, { "Hours": 720 }, { "Permanent": true } ]` |

## 9. Dependencias y supuestos

- Depende de: 004 (invalidación de sesión con `SecurityStamp`), 017 (moderación multimodal), 018 (bandas y
  umbrales), 019 (cuarentena y rol administrador) y 027 (registro de dispositivos y despacho push).
- Quienes dependen de esta spec: 021 (auditoría de moderación) y 030 (eliminación de cuenta).
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.
- Hoy solo las imágenes pasan por una decisión de moderación sancionable; la moderación de texto de Sensi IA
  tiene su propio flujo (spec 046) y no activa sanciones en v1.

## 10. Riesgos

- **Falso positivo:** un usuario inocente queda bloqueado. Mitigación: el escalamiento empieza en 24 h, las
  decisiones `Quarantine` no sancionan, la evidencia queda registrada y un administrador puede revertir.
- **Bloqueo de administradores:** un fallo en la política de objetivos podría sancionar a un administrador.
  Mitigación: CA-020-10 con prueba de integración.
- **Reincidencia mal contada:** una sanción revertida no debe contar para el escalamiento. Mitigación: solo
  cuentan las sanciones en estado `Activa` o `Vencida`, nunca `Revertida`.

## 11. Preguntas abiertas

Ninguna. El responsable confirmó los valores de D-1 y la alerta push (D-7) el 2026-11-19. Los usuarios con
rol `Comerciante` reciben el mismo trato que los consumidores.

## 12. Trazabilidad

- Requisitos de este spec: `FR-020-01` a `FR-020-08` y `NFR-020-01` a `NFR-020-05`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
- Contrato: [`contracts/openapi.yaml`](contracts/openapi.yaml)
