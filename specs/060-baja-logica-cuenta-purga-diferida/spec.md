# Spec 060 — Baja lógica de cuenta y purga diferida

| Campo | Valor |
|---|---|
| ID | 060 |
| Slug | `baja-logica-cuenta-purga-diferida` |
| Estado | Cerrado |
| Ola | 1 |
| Tipo | Nuevo (desdoblamiento de la spec 030) |
| Dueño | Equipo Sensus |
| Dependencias | 003, 004, 020 |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Contexto y problema

La spec 030 quedó parcial: `DELETE /api/v1/users/account` realiza un **borrado físico inmediato** del documento de usuario. No existe estado de eliminación, no se puede programar la purga a 30 días y la revocación depende de que el usuario desaparezca de MongoDB. El checklist 030 registra una brecha constitucional en el Principio IV («borrado físico inmediato sin estado Deleted ni purga a 30 días»). Esta spec cierra esa brecha con baja lógica, bloqueo inmediato y purga diferida mediante proceso programado.

La spec 061 cubre la anonimización de contenido y el cierre comercial; ambas specs sustituyen a la spec 030, que se cierra como **Escindida**.

## 2. Alcance

**Incluye**

- Estado de baja lógica en `User` y `UserDocument` (`DeletedAtUtc`, sin borrado físico inmediato)
- Revocación inmediata de sesiones mediante rotación de `SecurityStamp` y rechazo de tokens por estado de baja
- Bloqueo de inicio de sesión y de cualquier operación autenticada con error genérico (sin enumeración)
- Purga programada de la PII restante tras el plazo configurable (30 días por defecto)
- Registro de auditoría de solo adición para la baja y para la purga, con `correlation-id` y sin PII
- Idempotencia del proceso de purga y retención/frecuencia externalizadas en configuración

**No incluye**

- Anonimización de publicaciones, comentarios ni cierre comercial (spec 061)
- Exportación de datos previa ni recuperación de la cuenta tras la purga
- Eliminación de registros de auditoría antes de los 90 días
- Cambios en el contrato HTTP: la ruta `DELETE /api/v1/users/account` ya existe

## 3. Actores

- Usuario autenticado que solicita la baja
- Proceso de purga en segundo plano
- Operador (consumidor de la auditoría, sin acceso de escritura)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-060-01 | Marcar la cuenta como dada de baja con `DeletedAtUtc` y conservar la PII solo durante la retención configurada | Must |
| FR-060-02 | Rotar `SecurityStamp` en la baja y rechazar de inmediato los tokens emitidos con anterioridad | Must |
| FR-060-03 | Rechazar el inicio de sesión y cualquier operación autenticada de una cuenta dada de baja con error genérico, sin revelar si el correo existe | Must |
| FR-060-04 | Purgar el documento de usuario y la PII restante transcurrida la retención, mediante un proceso programado e idempotente | Must |
| FR-060-05 | Registrar la baja y la purga como eventos de auditoría de solo adición con actor, objetivo, acción, resultado, marca temporal y `correlation-id`, sin PII | Must |
| FR-060-06 | No purgar cuentas activas: el proceso selecciona únicamente usuarios con `DeletedAtUtc <= ahora - retención` | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-060-01 | Efectividad del bloqueo | < 1 s desde la confirmación; verificado por token rechazado y login 401 en la misma petición |
| NFR-060-02 | Purga | 100 % de las cuentas marcadas purgadas en ≤ 31 días; verificado con reloj simulado y `cutoff = ahora - retención` |
| NFR-060-03 | Configuración | Retención y frecuencia de purga en `appsettings.json`, nunca hardcodeadas |
| NFR-060-04 | Privacidad | Cero PII (correo, nombre, hash de contraseña, foto) en logs, respuestas y eventos de auditoría |

## 6. Criterios de aceptación

### CA-060-01: Baja lógica con revocación inmediata

- **Dado** un usuario autenticado con contraseña correcta
- **Cuando** envía `DELETE /api/v1/users/account`
- **Entonces** recibe `204`, el documento sigue en MongoDB con `DeletedAtUtc` poblado y `SecurityStamp` distinto al del token usado

### CA-060-02: Token denegado

- **Dado** un token emitido antes de la baja
- **Cuando** se usa en cualquier endpoint autenticado inmediatamente después
- **Entonces** se rechaza con `401` en menos de 1 segundo, sin depender de la expiración del JWT

### CA-060-03: Login denegado sin enumeración

- **Dado** una cuenta dada de baja
- **Cuando** se intenta iniciar sesión con su correo y contraseña correctos
- **Entonces** se devuelve el mismo `401` genérico que para un correo inexistente

### CA-060-04: Purga diferida

- **Dado** una cuenta dada de baja hace más de `AccountDeletion:RetentionDays`
- **Cuando** se ejecuta el proceso de purga
- **Entonces** su documento desaparece de MongoDB y queda registrado un evento de auditoría sin PII

### CA-060-05: Cuenta activa intacta

- **Dado** una cuenta sin `DeletedAtUtc`
- **Cuando** se ejecuta el proceso de purga
- **Entonces** no se elimina ni se modifica

### CA-060-06: Idempotencia

- **Dado** una cuenta ya purgada
- **Cuando** se ejecuta de nuevo el proceso de purga con el mismo `cutoff`
- **Entonces** no se produce ninguna eliminación adicional ni evento de auditoría duplicado

## 7. Contratos expuestos

No expone endpoints nuevos. Utiliza el contrato existente `DELETE /api/v1/users/account` (JWT, contraseña en el cuerpo) documentado en la spec 030; el código de éxito permanece `204`.

## 8. Dependencias y supuestos

- Depende de 003 (registro y sesiones), 004 (revocación) y 020 (estado y sanciones).
- La purga en cascada de intereses, interacciones, trazas de Sensi IA, tokens, foto, ubicación y ofertas ya implementada en la spec 030 se mantiene **inmediata**; esta spec solo difiere la eliminación del documento de usuario.
- El índice único de correo se mantiene durante la retención: un correo dado de baja no puede reutilizarse hasta la purga. Esto se documenta como consecuencia de conservar la PII.
- Los secretos y cadenas de conexión se resuelven por configuración, nunca en código.

## 9. Riesgos

- Una purga incompleta deja PII retenida más allá del plazo declarado.
- Un fallo en la rotación de `SecurityStamp` dejaría tokens válidos tras la baja.
- Un reintento concurrente del proceso de purga podría duplicar eventos de auditoría.

## 10. Preguntas abiertas

- ¿Se notifica por correo la purga completada? Fuera de alcance: no se envía notificación porque el correo del titular se elimina en la purga (resuelto: no notificar).

## 11. Trazabilidad

- Requisitos de este spec: `FR-060-01`, `FR-060-02`, `FR-060-03`, `FR-060-04`, `FR-060-05`, `FR-060-06`, `NFR-060-01`, `NFR-060-02`, `NFR-060-03`, `NFR-060-04`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
