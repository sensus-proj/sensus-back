# Spec 057 — Me gusta en ofertas (`OfferLike`)

| Campo | Valor |
|---|---|
| Estado | Implementado (retro-spec) |
| Versión | 1.0.0 |
| Fecha | 2026-11-19 |
| Autor | Equipo Sensus |
| Depende de | [001](../001-registro-usuario/spec.md), [007](../007-ofertas-cercanas/spec.md), [032](../032-detalle-oferta-y-vistas/spec.md), [034](../034-creacion-oferta/spec.md), [052](../052-sesion-invitado/spec.md) |

## 1. Contexto y objetivo

Permitir que usuarios registrados expresen y retiren «Me gusta» sobre las publicaciones de productos/ofertas activas de los comercios (`OfferLike`), y exponer el conteo total público de `LikesCount` junto con la bandera personal `IsLikedByMe` tanto en el detalle de oferta como en los listados de ofertas cercanas e inventario del comerciante.

> **Diferencia explícita con Spec 039 (`039-me-gusta-publicaciones`):** La Spec 039 cubre publicaciones sociales genéricas del feed comunitario (`POST /api/v1/posts/{id}/like`). La Spec 057 cubre exclusivamente productos/ofertas comerciales en el catálogo (`Offer` / `offer_likes` / `POST /api/v1/offers/{id}/like`).

## 2. Alcance y exclusiones

### Incluye
- Entidad `OfferLike` con vínculo único `(UserId, OfferId)` y fecha UTC.
- Endpoint `POST /api/v1/offers/{id}/like` que alterna (agrega o elimina) el «Me gusta» del usuario autenticado y devuelve `OfferLikeStatusDto` (`OfferId`, `LikesCount`, `IsLikedByMe`).
- Exposición de `LikesCount` y `IsLikedByMe` en `OfferDetailDto` (`GET /api/v1/offers/{id}`), `NearbyOfferDto` (`GET /api/v1/offers/nearby`) y `OfferDto` (`GET /api/v1/merchants/my-offers`).
- Invalidación de caché de detalle de oferta (`OfferCacheKeys.Detail(offer.Id)`) al alternar «Me gusta».
- Protección con `RegisteredUserPolicy` (impide mutaciones a invitados) y `SensitiveWritePolicy`.

### Excluye
- Me gusta de publicaciones de feed comunitario (`Spec 039`).
- Listado paginado de qué usuarios individuales dieron «Me gusta» (solo se expone el conteo total `LikesCount` y la actividad de amigos en Spec 058).

## 3. Actores y reglas de negocio

| Regla | Descripción |
|---|---|
| `RN-057-01` | Solo usuarios registrados (`RegisteredUserPolicy`) pueden alternar «Me gusta»; cualquier usuario (incluidos invitados en solo lectura) puede consultar el conteo `LikesCount` en `OfferDetailDto` y `NearbyOfferDto` (con `IsLikedByMe = false` para invitados). |
| `RN-057-02` | La relación `(UserId, OfferId)` es única: si existe un registro, la siguiente llamada lo elimina y decrementa el conteo; si no existe, lo inserta. |
| `RN-057-03` | No se puede dar «Me gusta» a ofertas inexistentes o archivadas (`OfferStatus.Archived`), devolviendo `404 Not Found`. |
| `RN-057-04` | La creación de `OfferLike` registra `CreatedAtUtc` para ordenar la actividad social en Comunidad (Spec 058). |

## 4. Requisitos funcionales y no funcionales

| ID | Tipo | Requisito | Prioridad / Métrica | Estado |
|---|---|---|---|---|
| `FR-057-01` | FR | Exponer `POST /api/v1/offers/{id}/like` para usuarios registrados con alternancia idempotente y respuesta `OfferLikeStatusDto` | Must | Implementado |
| `FR-057-02` | FR | Incluir `LikesCount` e `IsLikedByMe` reales en `GET /api/v1/offers/{id}`, `GET /api/v1/offers/nearby` y `GET /api/v1/merchants/my-offers` | Must | Implementado |
| `FR-057-03` | FR | Garantizar unicidad `(UserId, OfferId)` mediante índice único compuesto en MongoDB y rechazar ofertas inexistentes o archivadas con `404` | Must | Implementado |
| `FR-057-04` | FR | Invalidar caché de detalle (`OfferCacheKeys.Detail(offer.Id)`) tras alternar «Me gusta» sin alterar `OfferCategory` ni handlers de creación/edición | Must | Implementado |
| `NFR-057-01` | NFR | Seguridad y control de abuso: `RegisteredUserPolicy`, `SensitiveWritePolicy` y respuesta `403` para invitados | 100 % endpoints de escritura | Implementado |
| `NFR-057-02` | NFR | Rendimiento: índice único `(UserId, OfferId)` e índice `(OfferId, CreatedAtUtc desc)` en colección `offer_likes` | Sin full-scan en conteo por oferta | Implementado |
| `NFR-057-03` | NFR | Privacidad: nunca exponer lista de `UserId` o correos en `OfferDetailDto` o `OfferLikeStatusDto` | 0 PII en DTOs públicos | Implementado |

## 5. Criterios de aceptación

- **CA-057-01:** El primer `POST /api/v1/offers/{id}/like` por un usuario registrado devuelve `200 OK` con `isLikedByMe: true` y `likesCount: 1`; el segundo devuelve `isLikedByMe: false` y `likesCount: 0`.
- **CA-057-02:** Consultar `GET /api/v1/offers/{id}` o `GET /api/v1/offers/nearby` refleja el mismo `likesCount` e `isLikedByMe` para el usuario actual.
- **CA-057-03:** Un invitado que llama `POST /api/v1/offers/{id}/like` recibe `403 Forbidden`; sin token recibe `401 Unauthorized`.
- **CA-057-04:** Llamar sobre una oferta archivada o inexistente devuelve `404 Not Found`.

## 6. Dependencias y riesgos

- Depende de `offer_likes` en MongoDB Atlas y `ICacheService` (Spec 048).
- Riesgo de concurrencia al pulsar repetidamente: mitigado con el índice único `(UserId, OfferId)` y `SensitiveWritePolicy`.
