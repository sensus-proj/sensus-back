# Checklist 039 — Me gusta en publicaciones

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 039 está aprobada y sin preguntas bloqueantes. La intención explícita `liked` quedó definida en §10.
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 024, 013. La visibilidad usa `IFeedPostVisibilityLookup` sobre el contrato persistido de la 024 (`status = Published` y `isDeleted = false`).
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado (`plan.md` §4–§6)

## B. Específicos de este spec

- [x] Un usuario solo puede dar un me gusta por publicación. Índice único `ux_feed_post_likes_post_user`.
- [x] El conteo nunca es negativo. Se deriva de registros únicos y se acota con `FeedPostLike.ClampCount`.
- [x] La operación es consistente entre dispositivos. Prueba de concurrencia `Likes_ParallelLikesFromSameUser_LeaveOneRecordAndCountNPlusOne` verificada contra MongoDB 7.0 real (`Testcontainers`).
- [x] Solo se reacciona a contenido visible. 404 uniforme probado en unitarias e integración.

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (5/5)
- [x] Pruebas unitarias y de integración añadidas y en verde (incluidas las pruebas de persistencia con Testcontainers contra MongoDB 7.0 real).
- [x] Cobertura ≥ 80 % en Domain y Application (Domain 90,9 %, Application 89,2 % con pruebas unitarias)
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml` v1.1.0)
- [x] IaC actualizada y `cdk diff` revisado si aplica. No aplica: sin recursos AWS nuevos.
- [x] Logs con correlation-id y sin PII. Solo se registran `postId`, intención y transición.
- [x] Revisión de seguridad: sin secretos, autorización verificada (401, 403 invitado, 404, 429)
- [ ] Constitution Check en PASS o brecha registrada. Principio VIII pendiente de los escaneos de CI.

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS (`MongoFeedSocialIntegrationTests` ejecutadas y en verde contra MongoDB 7.0 real)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS. `NFR-039-01` (p95 < 300 ms) pendiente de prueba de carga.
- [ ] **VIII. DevSecOps de nivel fintech** — STRIDE en `plan.md` §4; faltan los escaneos bloqueantes de CI

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Feed/FeedPostLikesTests.cs` | ☑ |
| Pruebas de integración (endpoint) | `tests/Sensus.IntegrationTests/Feed/FeedPostLikeEndpointsTests.cs` | ☑ |
| Pruebas de integración (MongoDB) | `tests/Sensus.IntegrationTests/Persistence/MongoFeedSocialIntegrationTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-25 | Rocio Cruz | Backend completado | Build `-warnaserror` sin advertencias. Unitarias 1174/1174, arquitectura 5/5, E2E 3/3. Integración 643/677: las 34 fallas son `DockerUnavailableException` (Testcontainers), incluidas 3 de esta spec. `dotnet format --verify-no-changes` limpio. El limitador se movió después de `UseAuthentication` para particionar por usuario. |
| 2026-11-26 | Ejecutor nocturno (D1) | `T039-09` cerrada | `MongoFeedSocialIntegrationTests` ejecutadas con Docker/Testcontainers contra MongoDB 7.0 real en verde (unicidad, idempotencia y concurrencia `CA-039-04`). |
