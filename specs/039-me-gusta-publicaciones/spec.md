# Spec 039 — Me gusta en publicaciones

| Campo | Valor |
|---|---|
| ID | 039 |
| Slug | `me-gusta-publicaciones` |
| Estado | Backend completado |
| Ola | 3 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 024, 013 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

---

## 1. Contexto y problema

Permitir que un usuario marque o desmarque su me gusta en una publicación visible, con semántica de alternancia y conteo consistente.

## 2. Alcance

**Incluye**

- Alternancia de me gusta por usuario y publicación
- Conteo agregado
- Restricción a publicaciones visibles
- Idempotencia del resultado

**No incluye**

- Reacciones múltiples
- Listado de usuarios que dieron me gusta
- Notificaciones por me gusta (spec 044)

## 3. Actores

- Usuario autenticado

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-039-01 | Alternar el me gusta del usuario autenticado sobre una publicación | Must |
| FR-039-02 | Devolver el estado resultante y el conteo actualizado | Must |
| FR-039-03 | Rechazar publicaciones no visibles con 404 | Must |
| FR-039-04 | Garantizar un solo registro por usuario y publicación | Must |
| FR-039-05 | Mantener el conteo consistente ante peticiones repetidas | Must |
| FR-039-06 | Limitar la operación a 60 alternancias por usuario y hora | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-039-01 | Latencia de la operación | p95 < 300 ms |
| NFR-039-02 | Consistencia del conteo | Cero conteos negativos |

## 6. Criterios de aceptación

### CA-039-01

- **Dado** publicación visible sin me gusta
- **Cuando** POST /api/v1/feed/posts/{id}/like
- **Entonces** 200 con liked=true y conteo incrementado

### CA-039-02

- **Dado** que el usuario ya recibió la confirmación de su me gusta
- **Cuando** POST /api/v1/feed/posts/{id}/like
- **Entonces** 200 con liked=false y conteo decrementado

### CA-039-03

- **Dado** publicación en cuarentena
- **Cuando** se intenta reaccionar
- **Entonces** 404

### CA-039-04

- **Dado** una publicación visible con conteo inicial `N` y dos dispositivos del mismo usuario que parten del estado `liked=false`
- **Cuando** ambos envían en paralelo la solicitud para marcar el me gusta
- **Entonces** ambas respuestas indican `liked=true`, queda exactamente un registro para ese usuario y publicación y el conteo final es `N + 1`

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| POST | `/api/v1/feed/posts/{id}/like` | JWT |

## 8. Dependencias y supuestos

- Depende de: 024, 013
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Un contador desincronizado rompe la confianza en la métrica
- El POST con semántica de alternancia no es idempotente por diseño

## 10. Preguntas abiertas

Se conserva `POST /api/v1/feed/posts/{id}/like`. El cliente envía el estado deseado en el booleano
`liked`: `true` para marcar y `false` para quitar. Dos solicitudes con la misma intención producen
una sola transición. Una pulsación posterior que quita un me gusta confirmado envía `liked=false`.

### Dependencia pendiente
- Para verificar visibilidad, hace falta confirmar el contrato de lectura de la Spec 024. La consulta de comentarios en `origin/Fin_Beto` es un precedente. Antes de reutilizarla, debe exigir un estado publicado explícito y excluir la cuarentena.

**Resolución (2026-11-25).** La visibilidad se verifica con `IFeedPostVisibilityLookup`, que exige `status = Published` y `isDeleted = false` y excluye la cuarentena. Detalle en `plan.md` §3.

## 11. Trazabilidad

- Requisitos de este spec: `FR-039-01`, `FR-039-02`, `FR-039-03`, `FR-039-04`, `FR-039-05`, `FR-039-06`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
