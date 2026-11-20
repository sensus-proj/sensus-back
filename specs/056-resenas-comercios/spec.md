# Spec 056 — Reseñas y calificaciones de comercios (`StoreReview`)

| Campo | Valor |
|---|---|
| Estado | Parcial (retro-spec) |
| Versión | 1.0.0 |
| Fecha | 2026-11-19 |
| Autor | Equipo Sensus |
| Depende de | [001](../001-registro-usuario/spec.md), [005](../005-activacion-comerciante/spec.md), [006](../006-consulta-mi-comercio/spec.md), [017](../017-moderacion-multimodal/spec.md), [052](../052-sesion-invitado/spec.md), [059](../059-moderacion-texto-filtro-groserias/spec.md) |

## 1. Contexto y objetivo

Permitir que los usuarios registrados evalúen cualquier comercio activo con una calificación de 1 a 5 estrellas y un comentario escrito (modelo estilo Play Store), reaccionen con «Me gusta» a las reseñas existentes y respondan en hilo sobre una reseña. Al mismo tiempo, la calificación promedio real (`AverageRating`) y el conteo de reseñas (`TotalReviews` / `ReviewCount`) reemplazan los valores simulados en el perfil del comerciante y en los listados de comercios.

## 2. Alcance y exclusiones

### Incluye
- Consulta del resumen de calificaciones de un comercio (`AverageRating` redondeado a 1 decimal, `TotalReviews`, desglose de 5 a 1 estrellas y lista ordenada de reseñas con fecha UTC, autor, avatar, likes y respuestas) mediante `GET /api/v1/merchants/{merchantId}/reviews`.
- Creación de reseña de comercio (1 a 5 estrellas + comentario de 1 a 600 caracteres moderado) mediante `POST /api/v1/merchants/{merchantId}/reviews`.
- Alternancia idempotente de «Me gusta» sobre una reseña (`POST /api/v1/merchants/reviews/{reviewId}/like`).
- Publicación de respuesta moderada (1 a 500 caracteres) sobre una reseña (`POST /api/v1/merchants/reviews/{reviewId}/replies`).
- Cálculo dinámico de `AverageRating` y `ReviewCount` en `ComercioDto` para `GET /api/v1/merchants/me` y `GET /api/v1/merchants/nearby`, e invalidación de caché de comercio tras nuevas reseñas.

### Excluye
- Reseñas individuales por producto u oferta.
- Moderación de reseñas por parte de administradores (cubierta parcialmente por moderación preventiva).
- Paginación con cursor en el listado completo de reseñas (registrada como brecha técnica `FR-056-07`).

## 3. Actores y reglas de negocio

| Regla | Descripción |
|---|---|
| `RN-056-01` | Solo cuentas autenticadas registradas (`RegisteredUserPolicy`) pueden publicar reseñas, responder comentarios o dar «Me gusta». Los invitados (`Invitado`) solo pueden consultar el resumen en modo lectura. |
| `RN-056-02` | La calificación debe estar entre `1` y `5` estrellas inclusive. |
| `RN-056-03` | El comentario de reseña debe tener entre `1` y `600` caracteres una vez recortados espacios; una respuesta debe tener entre `1` y `500` caracteres. |
| `RN-056-04` | Tanto los comentarios de reseña como las respuestas pasan obligatoriamente por `ContentModerationHelper` (`ProfanityFilter` + `ITextModerationService` fail-closed) antes de persistirse. |
| `RN-056-05` | Cada reseña almacena autor (`AuthorUserId`, `AuthorName`, `AuthorPhotoUrl`) y marca temporal UTC (`CreatedAtUtc`) para mostrar nombre, avatar, fecha y año en cliente. |

## 4. Requisitos funcionales y no funcionales

| ID | Tipo | Requisito | Prioridad / Métrica | Estado |
|---|---|---|---|---|
| `FR-056-01` | FR | Exponer `GET /api/v1/merchants/{merchantId}/reviews` accesible a usuarios registrados e invitados con promedio real, desglose 5..1 estrellas y reseñas ordenadas | Must | Implementado |
| `FR-056-02` | FR | Permitir publicar una reseña de 1 a 5 estrellas y comentario (1–600 caracteres) vía `POST /api/v1/merchants/{merchantId}/reviews` solo a usuarios registrados | Must | Implementado |
| `FR-056-03` | FR | Alternar «Me gusta» de un usuario registrado sobre una reseña vía `POST /api/v1/merchants/reviews/{reviewId}/like` devolviendo el conteo actualizado | Must | Implementado |
| `FR-056-04` | FR | Permitir responder en hilo a una reseña (1–500 caracteres) vía `POST /api/v1/merchants/reviews/{reviewId}/replies` solo a usuarios registrados | Must | Implementado |
| `FR-056-05` | FR | Moderar todo comentario y respuesta mediante `ProfanityFilter` y `ITextModerationService` (*fail-closed*) antes de guardar en MongoDB | Must | Implementado |
| `FR-056-06` | FR | Poblar `AverageRating` y `ReviewCount` reales en `ComercioDto` (`GET /api/v1/merchants/me` y `/nearby`) e invalidar caché de comercio al crear reseña | Must | Implementado |
| `FR-056-07` | FR | Aplicar paginación `PagedResult` (`pageSize` 20/100) al listado de reseñas de un comercio cuando supere el umbral inicial | Should | Pendiente (brecha retro-spec) |
| `NFR-056-01` | NFR | Seguridad y límites de tasa: lectura con `read-heavy`, escritura con `sensitive-write` y errores estándar RFC 7807 `ProblemDetails` | 100 % endpoints | Implementado |
| `NFR-056-02` | NFR | Persistencia e índices: colección `store_reviews` con índice compuesto `(MerchantId asc, CreatedAtUtc desc)` | Sin full-scan por comercio | Implementado |
| `NFR-056-03` | NFR | Privacidad: excluir correo electrónico y credenciales de autores en `StoreReviewDto` y `StoreReviewReplyDto` | 0 campos PII sensibles | Implementado |

## 5. Criterios de aceptación

- **CA-056-01:** Un usuario registrado que publica calificación `5` y comentario limpio recibe `200 OK` con `StoreReviewsSummaryDto` actualizado; el promedio y el conteo en `GET /api/v1/merchants/me` reflejan la nueva reseña.
- **CA-056-02:** Si la calificación está fuera de `[1, 5]` o el comentario contiene lenguaje ofensivo, el endpoint responde `400 Bad Request` (`ProblemDetails`) sin persistir datos.
- **CA-056-03:** Un token de invitado (`Invitado`) o ausencia de token recibe `403 Forbidden` / `401 Unauthorized` en los endpoints de escritura (`POST`).
- **CA-056-04:** Pulsar dos veces `POST /api/v1/merchants/reviews/{reviewId}/like` alterna `IsLikedByMe` entre `true` y `false` incrementando o decrementando `LikesCount`.
- **CA-056-05:** Responder a una reseña inexistente responde `404 Not Found`.

## 6. Dependencias y riesgos

- Depende de la colección `store_reviews` en MongoDB Atlas y de `ITextModerationService` (Spec 059).
- Riesgo de abuso o spam en comentarios: mitigado mediante `SensitiveWritePolicy` y moderación fail-closed.
