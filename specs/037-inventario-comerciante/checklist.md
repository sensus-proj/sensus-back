# Checklist 037 — Inventario de ofertas del comerciante

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 037 está aprobada y sin preguntas bloqueantes (la pregunta sobre desfase quedó resuelta: métricas en tiempo real)
- [x] Los requisitos tienen identificador y criterio medible (`FR-037-01` a `FR-037-06`, `NFR-037-01` a `NFR-037-03`)
- [x] Las dependencias están resueltas: 034, 033, 010
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado en `plan.md` §4, §6 y §8

## B. Específicos de este spec

- [x] Solo se listan ofertas propias: `merchantId` del token y pruebas de aislamiento
- [x] La paginación es estándar: 20 por defecto, 100 máximo, 400 fuera de rango
- [x] Las métricas no exponen datos personales: solo totales y proyección sin `userId`/`codeHash`
- [x] La consulta usa índices declarados: `ix_offers_merchant_created`, `ix_offers_merchant_status_created` e `ix_offer_claims_offer_status`
- [x] Las métricas son en tiempo real: vistas desde `Offer.ViewCount` y apartados con conteo batch

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4)
- [x] Pruebas unitarias y de integración añadidas y en verde (16 unitarias nuevas y 9 de integración nuevas; la suite global queda en 498/507 por 9 fallos Testcontainers ajenos a esta spec)
- [x] Cobertura ≥ 80 % en Domain y Application: 89,35 % y 96,16 %
- [x] Contrato OpenAPI actualizado a v2.0.0 con paginación, métricas y errores 400/401/403
- [x] IaC actualizada y `cdk diff` revisado si aplica: no aplica, sin recursos ni secretos nuevos
- [x] Logs con `correlation-id` y sin PII: `LoggingBehavior`/`PerformanceBehavior` y errores `ProblemDetails`
- [x] Revisión de seguridad: autorización `MerchantOnlyPolicy`, validación de borde, sin secretos y STRIDE en `plan.md` §4
- [x] Constitution Check en PASS (ocho principios, incluido el VIII) en `plan.md` §8

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS: 16 unitarias y 9 de integración nuevas en verde; cobertura por encima del umbral
- [x] **VI. Costo cero e IaC** — PASS: sin costo incremental ni cambios en `infra/`
- [x] **VII. Rendimiento y observabilidad** — PASS: paginación 20/100, índices declarados, conteo batch por página y p95 < 600 ms verificado
- [x] **VIII. DevSecOps de nivel fintech** — PASS: STRIDE, controles ASVS/OWASP, sin secretos ni PII y escaneos bloqueantes del pipeline

## E. Evidencia

| Artefacto | Ubicación / comando | Resultado |
|---|---|---|
| Spec, plan, tareas y checklist | `specs/037-inventario-comerciante/` | Completos, v2.0.0, alineados a constitución v2.4.1 |
| Compilación | `dotnet build sensus-back.sln --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| Formato | `dotnet format sensus-back.sln --verify-no-changes` | Sin cambios pendientes (`FORMAT_OK`) |
| Pruebas unitarias | `dotnet test sensus-back.sln --configuration Release --no-build --collect:"XPlat Code Coverage"` | 920/920 en verde |
| Pruebas de arquitectura | Misma ejecución | 4/4 en verde |
| Pruebas E2E | Misma ejecución | 3/3 en verde |
| Pruebas de integración | Misma ejecución | 498/507 en verde; 9 fallos por `DockerUnavailableException` en `NotificationMongoRepositoryTests` y `MongoOfferDiscoveryIntegrationTests` (`npipe://./pipe/docker_engine`), sin omisiones ni umbrales reducidos |
| Cobertura | `node .github/scripts/check-coverage.mjs artifacts/coverage-final 80 Sensus.Domain Sensus.Application` | Domain 89,35 % (2299/2573), Application 96,16 % (6236/6485) |
| Estructura SDD | `node .github/scripts/check-specs.mjs specs` | 59 especificaciones revisadas, estructura correcta |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | v2.0.0 vigente |
| Handler y validador | `GetMerchantOffersQueryHandlerTests` (11) y `GetMerchantOffersQueryValidatorTests` (5) | En verde |
| Endpoint | `MerchantInventoryEndpointTests` (9, incluido p95 < 600 ms) | 9/9 en verde |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `GET /api/v1/merchants/my-offers` ordenado por fecha descendente con índice por MerchantId. Faltan métricas de apartados, canjes y vistas. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-23 | Equipo Sensus | CERRADO | Implementadas paginación 20/100, filtro por estado en MongoDB, `MerchantOfferDto` con apartados/canjes/vistas en tiempo real, conteo batch con `Filter.In`, tres índices, validador y errores 400/401/403. Gates: build 0/0, formato limpio, 920 unitarias + 4 arquitectura + 3 E2E + 498/507 integración, cobertura 89,35 % / 96,16 %, `check-specs.mjs` 59 specs. Brecha externa documentada: 9 pruebas Testcontainers siguen bloqueadas por Docker no disponible; no se desactivaron. Sin operaciones de Git. |
