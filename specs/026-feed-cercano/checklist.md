# Checklist 026 — Feed geolocalizado

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 026 está aprobada y sin preguntas bloqueantes. La decisión de ubicación del 2026-11-22 está en §10.
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 024, 007, 010. El contrato persistido de la 024 está en `Back_Rocio`; la eliminación marca `isDeleted` y `status = Deleted`, e invalida la caché del feed cercano.
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado (`plan.md` §4–§6)

## B. Específicos de este spec

- [x] El contenido no publicado nunca aparece. Lista de permitidos `status = Published` y `isDeleted = false`.
- [x] La consulta usa el índice. Se fija con `hint`; `Nearby_QueryPlanUsesDeclaredIndexWithoutCollectionScan` verificada en MongoDB 7.0 real (`IXSCAN` sin `COLLSCAN`).
- [x] El TTL de caché está documentado (`Redis:TtlByEntityMinutes:Feed`, 5 minutos; `plan.md` §3)
- [x] Las publicaciones sin ubicación quedan fuera. Sin campo `location`, el `2dsphere` no las indexa y el filtro no las devuelve.
- [x] La API acepta un radio de 5 km y devuelve 400 por encima del máximo (`CA-026-01`, `CA-026-05`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (5/5)
- [x] Pruebas unitarias y de integración añadidas y en verde (incluidas pruebas de persistencia con Testcontainers contra MongoDB 7.0 real).
- [x] Cobertura ≥ 80 % en Domain y Application (Domain 90,9 %, Application 89,2 % con pruebas unitarias)
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml` v1.1.0)
- [x] IaC actualizada y `cdk diff` revisado si aplica. No aplica: el índice lo crea `MongoDbContext` y no hay recursos AWS nuevos.
- [x] Logs con correlation-id y sin PII. No se registran coordenadas.
- [x] Revisión de seguridad: sin secretos, autorización verificada (401, 403 invitado, 400 de entrada)
- [ ] Constitution Check en PASS o brecha registrada. Principio VIII pendiente de los escaneos de CI.

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS. El DTO no expone coordenadas ni estado.
- [x] **V. Verificación automatizada** — PASS (`MongoFeedSocialIntegrationTests` ejecutadas y en verde contra MongoDB 7.0 real)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS. `NFR-026-01` (p95 < 300 ms con caché) pendiente de prueba de carga.
- [ ] **VIII. DevSecOps de nivel fintech** — STRIDE en `plan.md` §4; faltan los escaneos bloqueantes de CI

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Feed/NearbyFeedTests.cs` | ☑ |
| Pruebas de integración (endpoint) | `tests/Sensus.IntegrationTests/Feed/NearbyFeedEndpointsTests.cs` | ☑ |
| Pruebas de integración (MongoDB) | `tests/Sensus.IntegrationTests/Persistence/MongoFeedSocialIntegrationTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Ejecutor nocturno (D1) | `T026-08` cerrada | `MongoFeedSocialIntegrationTests` ejecutadas y en verde contra MongoDB 7.0 real (`Testcontainers`), validando índice `2dsphere` (`IXSCAN` sin `COLLSCAN`), radio y desempate estable. |
| 2026-11-25 | Rocio Cruz | Backend completado | Build `-warnaserror` sin advertencias. Unitarias 1174/1174, arquitectura 5/5, E2E 3/3. Integración 643/677: las 34 fallas son `DockerUnavailableException` (Testcontainers), incluidas 5 de esta spec. `dotnet format --verify-no-changes` limpio. Se añadió la ubicación opcional a `POST /api/v1/feed/posts` (contrato de la 024). |
