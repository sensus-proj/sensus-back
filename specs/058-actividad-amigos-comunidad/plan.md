# Plan 058 — Actividad de amigos en comunidad (`CommunityEndpoints`)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec alineada a implementación |
| Versión | 1.0.0 |
| Constitution Check | FAIL (retro-spec con brecha registrada `FR-058-04`) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

`CommunityEndpoints.cs` mapea `GET /api/v1/community/friends-activity`, despachando `GetFriendsCommunityActivityQuery` mediante MediatR. El handler resuelve las amistades aceptadas del usuario actual, consulta de manera acotada los likes de ofertas (`IOfferLikeRepository.GetByUserIdsAsync`) y reseñas/respuestas recientes (`IStoreReviewRepository.GetRecentActivityByUserIdsAsync`), enriquece nombres de comercio/producto/autor evitando PII sensible y ordena cronológicamente de más reciente a más antiguo.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Métodos `GetByUserIdsAsync` en `IOfferLikeRepository` y `GetRecentActivityByUserIdsAsync` en `IStoreReviewRepository` |
| Application | `GetFriendsCommunityActivityQuery`, `GetFriendsCommunityActivityQueryHandler`, `CommunityActivityItemDto` |
| Infrastructure | Implementaciones de consulta en `MongoOfferLikeRepository` y `MongoStoreReviewRepository` |
| Api | `CommunityEndpoints.cs` mapeado en `Program.cs` |
| Tests | `SocialHandlersTests`, `SocialEndpointsIntegrationTests`, `MongoSocialRepositoriesTests`, `CustomWebApplicationFactory` |

## 3. Modelo de datos e índices

- Reutiliza colecciones `friend_requests` (Spec 050), `offer_likes` (Spec 057) y `store_reviews` (Spec 056).
- Consultas filtran por `$in` sobre `visibleActorIds` ordenado por `CreatedAtUtc desc` con `Limit(effectiveLimit)`.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Acceso de no amigos o invitados a actividad social | Divulgación / Elevación de privilegios | `RegisteredUserPolicy` + filtro estricto `FriendRequestStatus.Accepted` | Bajo |
| Solicitud de `limit` abusivo (e.g. `1000000`) para causar DoS | Denegación de servicio | `Math.Clamp(request.Limit, 1, 100)` + `ReadHeavyPolicy` | Bajo |
| Fuga de correo electrónico o dirección residencial en actividad | Divulgación de información | `CommunityActivityItemDto` solo incluye nombre, foto, título del objetivo, calificación y comentario | Bajo |

## 5. Observabilidad y rendimiento

- Lecturas paralelizables o secuenciales acotadas a `effectiveLimit <= 100` entidades por tipo antes de proyectar.

## 6. Costo AWS

- 0.00 USD incrementales.

## 7. Estrategia de pruebas

- Unitarias (`Sensus.Application.UnitTests`): filtrado exclusivo de amigos aceptados + usuario propio, exclusión de desconocidos/pendientes y orden descendente.
- Integración (`Sensus.IntegrationTests`): `GET /api/v1/community/friends-activity` (200 con amigos y likes/reseñas, 401 sin token, 403 invitado).

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec `058-actividad-amigos-comunidad` separada explícitamente de 024–026 |
| II. Clean Architecture | PASS | Sin violaciones de dependencias entre capas |
| III. Calidad y pruebas | PASS | Query MediatR + `CommunityActivityItemDto` |
| IV. Seguridad y moderación | PASS | `RegisteredUserPolicy`, `ReadHeavyPolicy`, solo amigos aceptados |
| V. Cobertura y pruebas reales | PASS | Unitarias + integración real |
| VI. Costo AWS | PASS | 0 USD |
| VII. Paginación e índices | FAIL | Brecha `FR-058-04`: usa `limit` acotado `[1, 100]` en lugar de `PagedResult` |
| VIII. DevSecOps / STRIDE | PASS | STRIDE evaluado y privacidad de red social verificada |

## 9. Riesgos de implementación

- Si una oferta o comercio fue archivado o eliminado después de la actividad, el handler debe aplicar respaldo seguro sin lanzar `NullReferenceException` (`NFR-058-03`).

## 10. Orden de implementación

1. `T058-01` Documentar `CommunityEndpoints`, `GetFriendsCommunityActivityQuery` y `CommunityActivityItemDto`
2. `T058-02` Publicar `contracts/openapi.yaml`
3. `T058-03` Trazabilidad `FR-058-*` y guardas ante nulos de repositorios/ofertas eliminadas
4. `T058-04` Pruebas de integración en `SocialEndpointsIntegrationTests` y `MongoSocialRepositoriesTests`
5. `T058-05` Deuda futura: migrar respuesta a `PagedResult` (`FR-058-04`)
