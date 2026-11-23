# Spec 058 — Actividad de amigos en comunidad (`CommunityEndpoints`)

| Campo | Valor |
|---|---|
| Estado | Backend completado |
| Versión | 1.0.0 |
| Fecha | 2026-11-19 |
| Autor | Equipo Sensus |
| Depende de | [050](../050-amigos-y-solicitudes/spec.md), [056](../056-resenas-comercios/spec.md), [057](../057-me-gusta-ofertas/spec.md) |

## 1. Contexto y objetivo

Exponer en la pestaña **Comunidad** un flujo cronológico enfocado exclusivamente en las interacciones reales de tus amigos conectados y tuyas: «Me gusta» en ofertas/productos (`OfferLike`) y comentarios o respuestas en reseñas de comercios (`StoreReview` / `ReviewReply`), evitando ruido de publicaciones generales y asegurando reciprocidad entre amigos.

> **Diferencia explícita con Specs 024–026 (`024-publicaciones-feed`, `025-comentarios`, `026-feed-cercano`):** Las specs 024–026 modelan un muro público geolocalizado de publicaciones editoriales/consumidores (`/api/v1/feed`). La Spec 058 modela únicamente la actividad social entre amigos aceptados (`GET /api/v1/community/friends-activity`) sobre likes de ofertas y reseñas/respuestas de comercios.

## 2. Alcance y exclusiones

### Incluye
- Endpoint `GET /api/v1/community/friends-activity?page=1&pageSize=20` protegido por `RegisteredUserPolicy` y `ReadHeavyPolicy`. El alias temporal `limit` se acepta como tamaño de página si no se proporciona `pageSize`.
- Resolución de amistades aceptadas (`FriendRequestStatus.Accepted`) desde `IFriendRequestRepository` más el `userId` actual para reciprocidad bidireccional.
- Consolidación cronológica descendente (`CreatedAtUtc desc`) de eventos `OfferLike`, `StoreReview` y `ReviewReply` en `CommunityActivityItemDto`.

### Excluye
- Creación de posts libres o hilos genéricos (`Specs 024–026`).
- Actividad de usuarios desconocidos o solicitudes pendientes/rechazadas.

## 3. Actores y reglas de negocio

| Regla | Descripción |
|---|---|
| `RN-058-01` | Solo se incluyen eventos generados por usuarios con vínculo `FriendRequestStatus.Accepted` respecto al usuario autenticado o por el propio usuario autenticado. |
| `RN-058-02` | Solo existen tres tipos de evento permitidos en la respuesta: `OfferLike`, `StoreReview` y `ReviewReply`. |
| `RN-058-03` | `page` empieza en 1, y el tamaño de página acepta de 1 a 100 (20 por defecto). |
| `RN-058-04` | El endpoint es exclusivo para usuarios registrados (`RegisteredUserPolicy`); invitados reciben `403 Forbidden`. |

## 4. Requisitos funcionales y no funcionales

| ID | Tipo | Requisito | Prioridad / Métrica | Estado |
|---|---|---|---|---|
| `FR-058-01` | FR | Exponer `GET /api/v1/community/friends-activity` solo para usuarios registrados | Must | Implementado |
| `FR-058-02` | FR | Filtrar estrictamente por amigos en estado `Accepted` más el propio usuario para visibilidad mutua de actividad | Must | Implementado |
| `FR-058-03` | FR | Proyectar únicamente eventos `OfferLike`, `StoreReview` y `ReviewReply` ordenados por `CreatedAtUtc desc` en `CommunityActivityItemDto` | Must | Implementado |
| `FR-058-04` | FR | Adoptar `PagedResult` con paginación por página (`page`/`pageSize` 20/100) | Should | Implementado |
| `NFR-058-01` | NFR | Privacidad: nunca filtrar actividad de terceros sin amistad aceptada ni exponer correo/ubicación de amigos | 0 fugas de privacidad | Implementado |
| `NFR-058-02` | NFR | Seguridad y control de tasa: `RegisteredUserPolicy`, `ReadHeavyPolicy` y respuestas `401`/`403` vía `ProblemDetails` | 100 % cumplimiento | Implementado |
| `NFR-058-03` | NFR | Tolerancia a nulos: colecciones vacías o entidades referenciadas eliminadas no provocan excepciones 500 | 0 errores 500 | Implementado |

## 5. Criterios de aceptación

- **CA-058-01:** Si A y B son amigos aceptados, los likes en ofertas y reseñas/respuestas de A aparecen en `GET /api/v1/community/friends-activity` tanto de B como de A.
- **CA-058-02:** Eventos de usuario C (sin amistad o solicitud solo pendiente) nunca aparecen en el resultado de A.
- **CA-058-03:** Un invitado recibe `403 Forbidden` y una petición anónima recibe `401 Unauthorized`.
- **CA-058-04:** Con tres eventos visibles y `page=2&pageSize=1`, responde con el segundo evento y total 3. Empates de fecha se ordenan por ID del evento.
- **CA-058-05:** Una página o tamaño fuera del rango devuelve `400` con `ProblemDetails`.

## 6. Dependencias y riesgos

- Depende de `IFriendRequestRepository` (Spec 050), `IStoreReviewRepository` (Spec 056) y `IOfferLikeRepository` (Spec 057).
- La paginación limita la respuesta. Las búsquedas por autor y las uniones para consultar comercios y ofertas usan índices.
