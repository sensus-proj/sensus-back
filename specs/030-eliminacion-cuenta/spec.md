# Spec 030 — Eliminación de cuenta y datos

| Campo | Valor |
|---|---|
| ID | 030 |
| Slug | `eliminacion-cuenta` |
| Estado | Escindida |
| Ola | 1 |
| Tipo | Nuevo (escindida en las specs 060 y 061) |
| Dueño | Equipo Sensus |
| Dependencias | 003, 004, 020 |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Contexto y problema

Permitir que el titular elimine su cuenta con confirmación explícita, aplicando una retención declarada y purgando sus datos personales en cascada.

**Cierre 2026-11-23.** Esta spec se cierra como **Escindida**: la baja lógica, la revocación inmediata y la purga diferida se ejecutaron en la Spec 060; la anonimización de contenido, la expiración de apartados, el archivado comercial y la auditoría anonimizada se ejecutaron en la Spec 061. El contrato vigente conserva el código `204` de `DELETE /api/v1/users/account`, no el `202` previsto en el borrador original. Los requisitos de esta spec se dan por cubiertos por esas dos specs; no se abre trabajo nuevo bajo el ID 030.

## 2. Alcance

**Incluye**

- Solicitud de eliminación autenticada con confirmación
- Borrado lógico inmediato y bloqueo de acceso
- Purga de PII a los 30 días
- Anonimización de contenido público
- Desactivación de ofertas y comercio
- Revocación de sesiones

**No incluye**

- Exportación de datos previa
- Recuperación de la cuenta tras la purga
- Eliminación de registros de auditoría antes de 90 días

## 3. Actores

- Usuario autenticado
- Operador
- Proceso de purga

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-030-01 | Exponer DELETE /api/v1/users/account con confirmación explícita y contraseña | Must |
| FR-030-02 | Marcar la cuenta como deleted y revocar todas sus sesiones de inmediato | Must |
| FR-030-03 | Impedir el login y cualquier operación autenticada tras la eliminación | Must |
| FR-030-04 | Purgar los datos personales a los 30 días mediante proceso programado | Must |
| FR-030-05 | Anonimizar publicaciones y comentarios del usuario en lugar de conservarlos asociados | Must |
| FR-030-06 | Desactivar el comercio y sus ofertas activas al eliminar una cuenta comercial | Must |
| FR-030-07 | Conservar la auditoría de moderación anonimizada durante 90 días | Must |
| FR-030-08 | Borrar de inmediato, al eliminar la cuenta, los intereses (`user_interests`, spec 054) y las interacciones seudonimizadas (`offer_interactions`, spec 055, con todas las versiones de clave HMAC vigentes) | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-030-01 | Efectividad del bloqueo | < 1 s desde la eliminación |
| NFR-030-02 | Purga | 100 % de cuentas marcadas, en ≤ 31 días |
| NFR-030-03 | Trazabilidad | Toda eliminación registrada sin PII |

## 6. Criterios de aceptación

### CA-030-01

- **Dado** contraseña correcta y confirmación explícita
- **Cuando** DELETE /api/v1/users/account
- **Entonces** `204` y cuenta bloqueada de inmediato (baja lógica de la Spec 060)

### CA-030-02

- **Dado** contraseña incorrecta
- **Cuando** DELETE /api/v1/users/account
- **Entonces** 401 sin eliminar

### CA-030-03

- **Dado** cuenta eliminada
- **Cuando** se intenta login
- **Entonces** 401 genérico

### CA-030-04

- **Dado** purga ejecutada
- **Cuando** se inspeccionan los datos
- **Entonces** sin PII del titular y contenido anonimizado

### CA-030-05

- **Dado** cuenta comercial eliminada
- **Cuando** se consultan sus ofertas
- **Entonces** ofertas desactivadas y no visibles

### CA-030-06

- **Dado** un usuario con intereses e interacciones registradas con dos versiones de clave
- **Cuando** elimina su cuenta
- **Entonces** no queda ningún documento suyo en `user_interests` ni en `offer_interactions`

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| DELETE | `/api/v1/users/account` | JWT |

## 8. Dependencias y supuestos

- Depende de: 003, 004, 020
- Purga además los datos de las specs 054 y 055 (feed de promociones), sin esperar los 30 días.
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Una purga incompleta deja datos personales retenidos
- Eliminar una cuenta comercial oculta ofertas con apartados activos

## 10. Preguntas abiertas

- ¿Se notifica por correo la eliminación completada? (resuelto: no se notifica; la notificación a terceros queda fuera de alcance, decisión registrada en la Spec 061 §10)

## 11. Trazabilidad

- Requisitos de este spec: `FR-030-01`, `FR-030-02`, `FR-030-03`, `FR-030-04`, `FR-030-05`, `FR-030-06`, `FR-030-07`, `FR-030-08`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)

## 12. Cierre por escisión (2026-11-23)

| Requisito de esta spec | Spec de ejecución | Resultado |
|---|---|---|
| `FR-030-01`, `FR-030-02`, `FR-030-03` | 060 | Endpoint con `204`, baja lógica con `DeletedAtUtc`, rotación de `SecurityStamp`, rechazo de JWT y login genérico |
| `FR-030-04` | 060 | Purga diferida e idempotente con `cutoff = ahora - retención`, índice `ix_users_deleted_at` y configuración `AccountDeletion` |
| `FR-030-05`, `FR-030-07` | 061 | Anonimización de publicaciones y comentarios, auditoría anonimizada con `subjectKey` SHA-256 y TTL configurable |
| `FR-030-06` | 061 | Apartados activos expirados antes de archivar ofertas e invalidar caché |
| `FR-030-08` | 030 (T030-13, ya completada) | Purga inmediata de intereses e interacciones al eliminar la cuenta |
| `NFR-030-01` a `NFR-030-03` | 060 y 061 | Bloqueo < 1 s, purga en ≤ 31 días verificada con reloj simulado y trazabilidad sin PII |

La Spec 030 no queda pendiente de implementación: sus brechas constitucionales quedaron cerradas por las specs 060 y 061, con evidencia en sus respectivos checklists.
