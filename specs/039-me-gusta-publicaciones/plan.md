# Plan 039 — Me gusta en publicaciones

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Implementado. Visibilidad tomada del contrato persistido de la Spec 024 (`IFeedPostVisibilityLookup`). |
| Versión | 1.0.0 |
| Constitution Check | PASS salvo Principio VIII (escaneos de CI pendientes) |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Permitir que un usuario marque o desmarque su me gusta en una publicación visible, con semántica de alternancia y conteo consistente.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Regla de me gusta único por usuario |
| Application | ToggleLikeCommand y cálculo del conteo |
| Infrastructure | Índice único postId + userId y contador atómico |
| Api | Endpoint POST /api/v1/feed/posts/{id}/like |

## 3. Modelo de datos e índices

- Colección `feed_post_likes` (configurable en `MongoDbSettings:FeedPostLikesCollectionName`) con índice único `ux_feed_post_likes_post_user` sobre `postId + userId` (`FR-039-04`).
- **Decisión de implementación (2026-11-25).** El conteo se deriva de los registros únicos con `countDocuments` sobre el prefijo `postId` del mismo índice; no se mantiene un contador desnormalizado en `feed_posts`. Así el conteo nunca es negativo ni se desincroniza ante fallos parciales, sin exigir transacciones multi-documento (que requieren replica set también en local). Ver `NFR-039-02`.
- La intención `liked` se aplica de forma atómica: `liked=true` inserta y una inserción duplicada (petición repetida o concurrente) se resuelve como "ya existía"; `liked=false` elimina con `deleteOne`. Una misma intención solo produce una transición (`FR-039-05`, `CA-039-04`).
- La visibilidad se verifica antes de escribir con `IFeedPostVisibilityLookup` (lista de permitidos: `status = Published` y `isDeleted = false`). Un me gusta que entre en carrera con una eliminación queda sobre una publicación no visible y no se expone.

## 4. Seguridad

- El usuario proviene del token
- Sin exponer quién reaccionó

| STRIDE | Control previsto |
|---|---|
| Suplantación y elevación de privilegios | JWT y actor obtenido exclusivamente de la sesión. |
| Manipulación | Validar UUID e intención booleana. Proteger el índice único y el contador mediante escritura atómica. |
| Repudio | Registrar eventos de seguridad con correlación y sin contenido ni credenciales. |
| Divulgación | Responder 404 uniforme cuando la publicación sea inexistente o no visible. |
| Denegación de servicio | Limitar por usuario autenticado con un umbral configurable de 60 solicitudes por hora conforme a la Spec 013 (política `post-likes`, `RateLimiting:PostLikes`). El limitador se movió después de `UseAuthentication` en `Program.cs`: antes particionaba todas las políticas "por usuario" por IP. |

## 5. Observabilidad y rendimiento

- Métricas de me gusta y de conflictos de concurrencia

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de alternancia
- Integración de concurrencia y 404

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-039-01` y 5 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |
| VIII | Pendiente | STRIDE en §4. Permisos (401/403), visibilidad (404) y límite (429) probados; faltan los escaneos bloqueantes de CI y la ejecución de las pruebas de MongoDB. |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Un contador desincronizado rompe la confianza en la métrica
- El POST con semántica de alternancia no es idempotente por diseño

## 10. Orden de implementación

1. `T039-01` Actualizar y validar el contrato OpenAPI del spec
2. `T039-02` Implementar: Regla de me gusta único por usuario
3. `T039-03` Implementar: ToggleLikeCommand y cálculo del conteo
4. `T039-04` Implementar: Índice único postId + userId y contador atómico
5. `T039-05` Implementar: Endpoint POST /api/v1/feed/posts/{id}/like
6. `T039-06` Modelar la unicidad del me gusta
7. `T039-07` Implementar el índice único y el contador atómico
8. `T039-08` Probar la alternancia y el 404
9. `T039-09` Implementar las pruebas de integración del flujo completo
10. `T039-10` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
11. `T039-11` Registrar evidencias del Constitution Check
