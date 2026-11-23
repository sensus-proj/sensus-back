# Spec 061 — Anonimización de contenido y cierre comercial

| Campo | Valor |
|---|---|
| ID | 061 |
| Slug | `anonimizacion-contenido-cierre-comercial` |
| Estado | Cerrado |
| Ola | 1 |
| Tipo | Nuevo (desdoblamiento de la spec 030) |
| Dueño | Equipo Sensus |
| Dependencias | 003, 020, 024, 025, 030, 033 |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Contexto y problema

La spec 030 dejó pendientes tres responsabilidades al eliminar una cuenta: (a) las publicaciones y comentarios del feed conservan `AuthorUserId`, nombre y foto del titular; (b) la cuenta comercial se borra y sus ofertas se archivan sin resolver los apartados `Active`, que quedan hasta el job genérico de expiración; y (c) no existe una copia anonimizada de la evidencia de moderación con retención de 90 días. Esta spec cierra esos tres frentes.

La spec 060 cubre la baja lógica y la purga diferida; ambas specs sustituyen a la spec 030, que se cierra como **Escindida**.

## 2. Alcance

**Incluye**

- Anonimización de publicaciones y comentarios del feed en la baja, conservando el contenido ya moderado
- Expiración explícita de los apartados activos del comercio antes de archivar sus ofertas
- Archivado de ofertas activas e invalidación de caché (comportamiento existente, ahora con apartados resueltos)
- Conservación de la evidencia de moderación anonimizada durante 90 días en una colección con TTL
- Idempotencia de todas las operaciones anteriores

**No incluye**

- Baja lógica, revocación de sesiones ni purga del documento de usuario (spec 060)
- Auditoría de decisiones de moderación en tiempo de decisión (spec 021)
- Borrado de la colección de sanciones, que sigue siendo de solo adición (Principio VIII)
- Modificación del contenido publicado ni de imágenes ya aprobadas

## 3. Actores

- Usuario autenticado que solicita la baja
- Proceso de baja de cuenta
- Operador (consulta de auditoría anonimizada)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-061-01 | Anonimizar las publicaciones del feed del usuario: `AuthorUserId = Guid.Empty`, nombre «Usuario» y foto nula, conservando contenido e imagen | Must |
| FR-061-02 | Anonimizar los comentarios del feed del usuario con el mismo criterio, conservando el texto | Must |
| FR-061-03 | Marcar como expirados todos los apartados activos del comercio del usuario antes de archivar sus ofertas | Must |
| FR-061-04 | Archivar las ofertas activas del comercio e invalidar su caché, sin cambiar sus identificadores | Must |
| FR-061-05 | Copiar la evidencia de moderación existente del usuario a una colección anonimizada con TTL de 90 días configurable | Must |
| FR-061-06 | Mantener todas las operaciones idempotentes: repetir la baja no duplica registros ni altera contenido ya anonimizado | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-061-01 | Anonimización completa | 100 % de publicaciones y comentarios del usuario sin `AuthorUserId` original, verificado por prueba de regresión |
| NFR-061-02 | Apartados resueltos | 0 apartados `Active` del comercio tras la baja, verificado por prueba |
| NFR-061-03 | Retención de auditoría | Evidencia anonimizada con `expiresAtUtc = ahora + 90 días`, configurable y sin PII |
| NFR-061-04 | Sin regresión visual | Las ofertas archivadas conservan su `Id` y dejan de devolverse en consultas públicas |

## 6. Criterios de aceptación

### CA-061-01: Publicaciones anonimizadas

- **Dado** un usuario con publicaciones moderadas y visibles
- **Cuando** elimina su cuenta
- **Entonces** las publicaciones siguen visibles, pero sin identificador, nombre ni foto del autor

### CA-061-02: Comentarios anonimizados

- **Dado** un usuario con comentarios publicados
- **Cuando** elimina su cuenta
- **Entonces** los comentarios conservan el texto moderado y muestran «Usuario» como autor

### CA-061-03: Apartados activos resueltos

- **Dado** un comercio con apartados `Active` en sus ofertas
- **Cuando** se elimina la cuenta del comerciante
- **Entonces** todos esos apartados pasan a `Expired` antes de archivar las ofertas y no quedan `Active`

### CA-061-04: Ofertas archivadas

- **Dado** un comercio con ofertas `Published`
- **Cuando** se elimina la cuenta
- **Entonces** las ofertas pasan a `Archived`, conservan su `Id` y su caché se invalida

### CA-061-05: Evidencia de moderación anonimizada

- **Dado** un usuario con sanciones que contienen evidencia de moderación (decisión, categorías, puntajes, umbral y acción)
- **Cuando** elimina su cuenta
- **Entonces** se guarda una copia sin correo, nombre ni identificador directo, con expiración a 90 días configurable

### CA-061-06: Idempotencia

- **Dado** un usuario ya dado de baja
- **Cuando** se repite el proceso de anonimización y cierre comercial
- **Entonces** no se duplican registros de auditoría ni se modifican las publicaciones ya anonimizadas

## 7. Contratos expuestos

No expone endpoints nuevos. Reutiliza `DELETE /api/v1/users/account` documentado en la spec 030 y la baja lógica de la spec 060.

## 8. Dependencias y supuestos

- Depende de 003 y 030 (cuenta), 020 y 033 (sanciones y apartados), 024 y 025 (publicaciones y comentarios).
- La evidencia de moderación disponible hoy es la de las sanciones automáticas (`Sanction.Evidence`); la auditoría de decisiones en tiempo real sigue en la spec 021.
- El contenido se anonimiza **en lugar de borrarse** para preservar la conversación pública; el texto ya pasó moderación preventiva.
- Los plazos de retención se leen de configuración, nunca del código.

## 9. Riesgos

- Una anonimización incompleta deja datos personales asociados al contenido público.
- Expirar apartados activos puede afectar a usuarios que ya tenían un código; se documenta como consecuencia del cierre del comercio.
- La copia de auditoría podría incluir PII si se copian campos del usuario; se restringe explícitamente a los campos de `SanctionEvidence`.

## 10. Preguntas abiertas

- ¿Se notifica a los poseedores de apartados activos? Fuera de alcance: el cierre del comercio ocurre tras reautenticación del titular y el aviso pertenece a la spec 044 (resuelto: no notificar desde esta spec).

## 11. Trazabilidad

- Requisitos de este spec: `FR-061-01` a `FR-061-06`, `NFR-061-01` a `NFR-061-04`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
