# Plan 058 — Actividad de amigos en comunidad (`CommunityEndpoints`)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec alineada a implementación |
| Versión | 1.0.0 |
| Constitution Check | Paginación verificada en local. Escaneos de CI pendientes. |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

`CommunityEndpoints.cs` mapea `GET /api/v1/community/friends-activity` con MediatR. El handler resuelve las amistades aceptadas del usuario actual y delega el conteo, página y proyección a `ICommunityActivityReader`.

## 2. Cambios por capa

Para `FR-058-04` la API devuelve `PagedResult`. Application resuelve las amistades aceptadas y entrega al repositorio los IDs permitidos, más la cuenta autenticada. MongoDB une las tres fuentes con `$unionWith`, filtra autores y recursos visibles, y devuelve total y página con `$facet`. Las respuestas se ordenan por su propia fecha y se excluye el texto de la reseña original cuando su autor no es visible. Los índices por autor de reseña, autor de respuesta y autor del «Me gusta» evitan recorridos completos. La ficha y el contrato de salida contienen solo datos autorizados del evento. Costo incremental estimado de AWS de 0 USD.

| Capa | Cambio |
|---|---|
| Domain | Amistades aceptadas, likes, reseñas y respuestas |
| Application | `GetFriendsCommunityActivityQuery`, validador, `ICommunityActivityReader` y `CommunityActivityItemDto` |
| Infrastructure | Lectura consolidada en `MongoCommunityActivityReader` |
| Api | `CommunityEndpoints.cs` mapeado en `Program.cs` |
| Tests | `CommunityEndpointsTests`, `MongoCommunityActivityTests`, `CustomWebApplicationFactory` |

## 3. Modelo de datos e índices

- Reutiliza colecciones `friend_requests` (Spec 050), `offer_likes` (Spec 057) y `store_reviews` (Spec 056).
- Las tres ramas filtran por autor con índice, combinan resultados y ordenan por fecha descendente e ID de evento.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Acceso de no amigos o invitados a actividad social | Divulgación / Elevación de privilegios | `RegisteredUserPolicy` + filtro estricto `FriendRequestStatus.Accepted` | Bajo |
| Solicitud de tamaño de página abusivo | Denegación de servicio | Validación 1–100, índices por autor y `ReadHeavyPolicy` | Bajo |
| Fuga de correo electrónico o dirección residencial en actividad | Divulgación de información | `CommunityActivityItemDto` solo incluye nombre, foto, título del objetivo, calificación y comentario | Bajo |

## 5. Observabilidad y rendimiento

- Una agregación devuelve el total y hasta 100 eventos. La consulta de perfiles se limita a la página.

## 6. Costo AWS

- 0.00 USD incrementales.

## 7. Estrategia de pruebas

- HTTP: identidad registrada, paginación inválida y exclusión de amistades pendientes.
- MongoDB real: mezcla de likes, reseñas y respuestas, orden cronológico y exclusión de terceros y borradores.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec `058-actividad-amigos-comunidad` separada explícitamente de 024–026 |
| II. Clean Architecture | PASS | Sin violaciones de dependencias entre capas |
| III. Calidad y pruebas | PASS | Query MediatR + `CommunityActivityItemDto` |
| IV. Seguridad y moderación | PASS | `RegisteredUserPolicy`, `ReadHeavyPolicy`, solo amigos aceptados |
| V. Cobertura y pruebas reales | PASS | Unitarias + integración real |
| VI. Costo AWS | PASS | 0 USD |
| VII. Paginación e índices | PASS local | `PagedResult` y consulta indexada para las tres fuentes |
| VIII. DevSecOps / STRIDE | Pendiente | STRIDE evaluado. Faltan escaneos bloqueantes de CI. |

## 9. Riesgos de implementación

- Si una oferta o comercio fue archivado o eliminado después de la actividad, el handler debe aplicar respaldo seguro sin lanzar `NullReferenceException` (`NFR-058-03`).

## 10. Orden de implementación

1. `T058-01` Documentar `CommunityEndpoints`, `GetFriendsCommunityActivityQuery` y `CommunityActivityItemDto`
2. `T058-02` Publicar `contracts/openapi.yaml`
3. `T058-03` Trazabilidad `FR-058-*` y guardas ante nulos de repositorios/ofertas eliminadas
4. `T058-04` Pruebas HTTP y MongoDB en `CommunityEndpointsTests` y `MongoCommunityActivityTests`
5. `T058-05` Respuesta paginada con `PagedResult` (`FR-058-04`)
