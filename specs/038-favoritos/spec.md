# Spec 038 — Favoritos de ofertas

| Campo | Valor |
|---|---|
| ID | 038 |
| Slug | `favoritos` |
| Estado | Implementado |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 032; consumida como señal de afinidad por 042 |
| Versión | 2.1.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Permitir que un usuario marque o desmarque una oferta como favorita, devolviendo el estado resultante y el conteo, y alimentando la afinidad de categoría que consume el ranking de la spec 042.

## 2. Alcance

**Incluye**

- Alternancia de favorito por usuario y oferta (`POST /api/v1/users/favorites/{id}`)
- Conteo de favoritos por oferta calculado desde la colección
- Restricción a ofertas visibles
- Diferenciación entre 401 (sin token) y 403 (invitado)
- Señal de afinidad para el ranking, con peso 2 y caché invalidada al alternar
- Límite de 120 alternancias por usuario y hora

**No incluye**

- Listado de favoritos (fuera del catálogo)
- Notificaciones por favorito
- Colecciones nombradas
- Contador atómico `Offer.FavoriteCount` (inexistente en el dominio)

## 3. Actores

- Usuario autenticado (registrado)
- Invitado (rechazado con 403)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-038-01 | Alternar el favorito del usuario autenticado sobre una oferta visible | Must |
| FR-038-02 | Devolver el estado resultante y el conteo | Must |
| FR-038-03 | Rechazar ofertas no visibles con 404 | Must |
| FR-038-04 | Garantizar un solo favorito por usuario y oferta | Must |
| FR-038-05 | Exponer la afinidad de categoría al ranking de la spec 042, invalidando `feed:affinity:{userId}` al alternar | Should |
| FR-038-06 | Limitar la operación a 120 alternancias por usuario y hora | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-038-01 | Latencia de la operación | p95 < 300 ms |
| NFR-038-02 | Consistencia | Cero conteos negativos |

## 6. Criterios de aceptación

### CA-038-01

- **Dado** oferta visible sin favorito
- **Cuando** POST /api/v1/users/favorites/{id}
- **Entonces** 200 con `favorited=true` y `favoriteCount` actualizado

### CA-038-02

- **Dado** segunda llamada del mismo usuario
- **Cuando** POST /api/v1/users/favorites/{id}
- **Entonces** 200 con `favorited=false` y `favoriteCount` actualizado

### CA-038-03

- **Dado** oferta archivada
- **Cuando** se intenta marcar
- **Entonces** 404

### CA-038-04

- **Dado** dos dispositivos en paralelo
- **Cuando** se envían ambas peticiones
- **Entonces** un solo registro garantizado por índice único; el booleano y el conteo pueden reflejar un estado intermedio

### CA-038-05 — Invitado

- **Dado** un token de invitado
- **Cuando** POST /api/v1/users/favorites/{id}
- **Entonces** 403 `Favorite.GuestForbidden`; sin token, 401 `Favorite.Unauthorized`

## 7. Contratos expuestos

| Método | Ruta | Autorización | Límite de tasa |
|---|---|---|---|
| POST | `/api/v1/users/favorites/{id}` | JWT (`RegisteredUserPolicy`) | `FavoritesPolicy` — 120/hora por usuario |

## 8. Dependencias y supuestos

- Depende de: 032 (ofertas activas y visibles).
- La spec 042 consume los favoritos como señal de afinidad; no hay dependencia circular porque 042 funciona sin esta señal.
- El conteo se obtiene con `CountDocumentsAsync` sobre `offer_favorites`, no con un contador desnormalizado en `Offer`.
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Alimentar el ranking con datos escasos puede sesgar las recomendaciones: el peso del favorito (2) es menor que el de un apartado (3).
- La alternancia usa borrado seguido de inserción protegido por índice único: ante concurrencia extrema el booleano devuelto y el conteo pueden reflejar un estado intermedio, aunque nunca se duplican registros.
- Invalidar la caché de afinidad encarece la siguiente petición al feed; es necesaria para que la nueva señal se refleje.

## 10. Decisiones tomadas

- **Integración con 042** (2026-11-23): `MongoUserAffinityReader` incluye los favoritos del usuario con peso 2.0 (vistas 1.0, apartados 3.0). `ToggleOfferFavoriteCommandHandler` invalida `FeedCacheKeys.Affinity(userId)` junto con `OfferCacheKeys.Detail(offerId)`.
- **Política de tasa dedicada** (2026-11-23): `FavoritesPolicy` con 120 peticiones por hora y usuario, en lugar de `SensitiveWritePolicy` (30/min).
- **Sin contador en `Offer`** (2026-11-23): el plan original proponía `Offer.FavoriteCount`, que no existe en el dominio; se calcula el conteo desde la colección.

## 11. Preguntas abiertas

- ¿El cliente necesita leer sus favoritos? El catálogo solo expone la alternancia.

## 12. Trazabilidad

- Requisitos de este spec: `FR-038-01`, `FR-038-02`, `FR-038-03`, `FR-038-04`, `FR-038-05`, `FR-038-06`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
