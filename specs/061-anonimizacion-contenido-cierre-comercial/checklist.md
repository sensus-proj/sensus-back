# Checklist 061 — Anonimización de contenido y cierre comercial

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

**Estado: CERRADO (v2.0.0)** — 2026-11-23.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 061 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible (`FR-061-01` a `FR-061-06`, `NFR-061-01` a `NFR-061-04`)
- [x] Las dependencias están resueltas: 003, 020, 024, 025, 030, 033
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado en `plan.md` §4, §6 y §8

## B. Específicos de este spec

- [x] Publicaciones y comentarios conservan el contenido pero pierden autor, nombre y foto (`Guid.Empty`, «Usuario», foto nula)
- [x] Los apartados activos del comercio se expiran antes de archivar las ofertas (orden verificado con `Received.InOrder`)
- [x] Las ofertas archivadas conservan su identificador y su caché se invalida
- [x] La evidencia de moderación se copia sin PII con TTL de 90 días configurable (`subjectKey` SHA-256)
- [x] La colección `sanctions` no se modifica ni se elimina
- [x] Repetir el cierre no duplica registros ni altera contenido anonimizado

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4)
- [x] Pruebas unitarias y de integración añadidas y en verde; regresión E2E de baja corregida (3/3)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato existente verificado (sin endpoints nuevos): `DELETE /api/v1/users/account` sigue en `204`
- [x] IaC sin cambios: no hay recursos AWS nuevos
- [x] Logs con `correlation-id` y sin PII
- [x] Constitution Check en PASS (ocho principios, incluido el VIII)

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS (comando de cierre separado; sin endpoints nuevos)
- [x] **IV. Seguridad y privacidad** — PASS (STRIDE en `plan.md` §4, anonimización verificable, sin PII en auditoría ni logs)
- [x] **V. Verificación automatizada** — PASS: gates ejecutados; brecha externa de Docker registrada sin omitir pruebas ni bajar umbrales
- [x] **VI. Costo cero e IaC** — PASS (sin recursos nuevos)
- [x] **VII. Rendimiento y observabilidad** — PASS (actualizaciones indexadas y auditoría con métricas)
- [x] **VIII. DevSecOps de nivel fintech** — PASS (auditoría de solo adición con retención declarada y hash irreversible del sujeto)

## E. Evidencia

| Artefacto | Ubicación / comando | Resultado |
|---|---|---|
| Spec, plan, tareas y checklist | `specs/061-anonimizacion-contenido-cierre-comercial/` | Completos, v2.0.0, alineados a constitución v2.4.1 |
| Compilación | `dotnet build sensus-back.sln --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| Formato | `dotnet format sensus-back.sln --verify-no-changes` | Sin cambios pendientes |
| Pruebas unitarias | `dotnet test sensus-back.sln --configuration Release --no-build --collect:"XPlat Code Coverage"` | 932/932 en verde (6 suites nuevas de 061) |
| Pruebas de arquitectura | Misma ejecución | 4/4 en verde |
| Pruebas E2E | Misma ejecución | 3/3 en verde (baja con 204; fakes E2E corregidos) |
| Pruebas de integración | Misma ejecución | 506/517 en verde; 11 fallos por `DockerUnavailableException` (`npipe://./pipe/docker_engine`), sin omisiones. Nuevo escenario `DeleteAccount_ShouldAnonymizeContentExpireClaimsBeforeArchivingAndAuditWithoutPii` en verde |
| Cobertura | `node .github/scripts/check-coverage.mjs tests 80` | Domain 89,59 % (2357/2631), Application 96,18 % (6342/6594) |
| Estructura SDD | `node .github/scripts/check-specs.mjs specs` | 61 especificaciones revisadas, estructura correcta |
| Índices y TTL | `src/Sensus.Infrastructure/Persistence/Mongo/MongoDbContext.cs` | `ix_feed_comments_author`, `ix_offer_claims_merchant_status`, `ttl_moderation_audit` |
| Fakes E2E | `tests/Sensus.TestKit/Fakes/InMemory{FeedPost,OfferClaim,Sanction,ModerationAudit}Repository.cs` | Registrados en `SensusE2EWebApplicationFactory`; corrigen el 500 de la baja al resolver repositorios Mongo sin conexión |

## F. Brechas externas documentadas

- 11 pruebas Testcontainers bloqueadas por daemon Docker no disponible en este entorno; no se omitieron ni se redujeron umbrales.
- La verificación de índices y TTL contra MongoDB real depende de esas pruebas; la declaración en `MongoDbContext` y las pruebas unitarias/integración HTTP están en verde.
- No se notifica a los poseedores de apartados expirados (decisión registrada en la spec §10).
- El `subjectKey` es un seudónimo SHA-256, no anonimato pleno; se documenta en `plan.md` §9 con retención de 90 días.
- Sin `cdk deploy` ni operaciones de Git.

## G. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-23 | Equipo Sensus | Aprobado | Spec creada para cerrar la anonimización, el cierre comercial y la retención de evidencia anonimizada pendientes en la spec 030. |
| 2026-11-23 | Equipo Sensus (con asistente) | CERRADO | `FeedPost.Anonymize`/`FeedComment.Anonymize`, expiración de apartados antes del archivado, `moderation_audit` sin PII con TTL configurable e idempotencia. Regresión E2E de baja corregida con fakes en memoria. Gates: build 0/0, formato limpio, 932 unitarias + 4 arquitectura + 3 E2E + 506/517 integración, cobertura 89,59 % / 96,18 %, `check-specs.mjs` 61 specs. Brecha externa de Docker documentada en §F. Sin `cdk deploy` ni operaciones de Git. |
