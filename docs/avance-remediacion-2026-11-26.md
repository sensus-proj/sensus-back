# Avance de ejecución — Plan de remediación Sensus (2026-11-26)

## RESUMEN EJECUTIVO (15 líneas)
1. **Hecho completo (código, pruebas locales y Docker):** Bloques **A1–A4** (fail-closed `ContentModerationHelper`, `ISensitiveRequest`, rechazo `ImageUrl` en feed + móvil, límite 20 ofertas), **D1** (Testcontainers 726/726 en verde), **D2** (STRIDE + `G-11` cerrada) y **C1–C5** (cuarentena `FeedPost`, `202 Accepted` + worker de ofertas, moderación previa + `type=authenticated` + móvil sin bypass, JWT 15 min + refresh rotatorio en backend/móvil `G-06`, y TOTP RFC 6238 `HMACSHA1` sin `Otp.NET` + step-up `amr=mfa` `G-05` Spec 062).
2. **Estado global verificado al cierre del Bloque C:** compilación solución e `infra` `0/0` (`-warnaserror`), formato limpio, **1215/1215** unitarias (Domain **90.09 %**, Application **88.40 %**), **6/6** arquitectura, **3/3** E2E, **726/726** integración, **62/62** specs válidas, **5/5** evals deterministas y móvil (`movil_Rodrigo` sobre `develop`) `assembleDebug testDebugUnitTest` **BUILD SUCCESSFUL**; más **26/26** pruebas JS tras añadir `load-smoke`.
3. **Hecho en código / workflow y pendiente en la nube (Bloque B):** `environment: production` (`B1`), roles OIDC + fallback en workflows (`B2`), monitoreo SES (`B3`), `Cdklabs.CdkNag` 3.1.0 (`app.Synth()` 0 errores) + CycloneDX/atestación SLSA 2 (`B4`), y workflow `dast-zap.yml` + `.zap/rules.tsv` + `mcp_config.example.json` (`B5`).
4. **Solo preparado (no ejecutado por horario nocturno y restricción de producción):** **D3** (push real FCM en 2 celulares), **D4** (humo de carga `<= 5 rps` sin `/api/v1/ai/*` mediante `.github/scripts/load-smoke.mjs`) y corrida diurna de **ZAP (`dast-zap.yml`)**.
5. **Parcial / externo:** consulta de AWS IAM/SSM omitida porque la sesión local expiró (`aws: Your session has expired`); migración histórica de activos públicos previos en Cloudinary y retiro de `/users/photo-signature` dejados para confirmación; `G-12` (Figma) verificada en `.gitignore` pero requiere que Rodrigo revoque su token en Figma.
6. **Sin commits ni operaciones destructivas:** todos los cambios locales de backend (`Back_Rocio` sin commit) y móvil (`movil_Rodrigo` sincronizada sobre `develop` sin commit) esperan revisión y PR de Rodrigo.
7. **Qué revisar primero (decisiones provisionales):**
8. - **`A3/C3` (compatibilidad móvil):** se conservó el alias opcional `mediaId` en `FeedEndpoints` y el endpoint legado `/users/photo-signature` durante la transición de clientes. Confirmar fecha de retiro y estrategia de migración de assets públicos antiguos en Cloudinary.
9. - **`B2` (`SensusInfraStack.GitHubOidc.cs`):** se asumió creación de proveedor OIDC con parámetro opcional `-c existingGithubOidcProviderArn=<ARN>`; revisar `aws iam list-open-id-connect-providers` tras `aws login` antes del primer `cdk deploy`.
10. - **`C5` (`AdminOnlyPolicy`):** *step-up* estricto (`amr=mfa`, recencia ≤ 15 min) en todas las rutas administrativas sin periodo de gracia si un admin aún no ha enrolado TOTP (solo permite `/auth/mfa/enroll` y `/verify`).
11. - **`A4` (`OfferLimit`):** validación de límite por conteo indexado sin transacciones multi-documento en MongoDB M0.
12. **Qué le toca hacer a Rodrigo (de día, 07:00–22:30 CST, en orden):**
13. - **(1)** Revocar token viejo de Figma en Web y exportar `FIGMA_API_KEY`; **(2)** en GitHub crear environment `production` con aprobador; **(3)** `aws login`, consultar OIDC/SES, crear `/sensus/prod/Mfa/EncryptionKey` (32 B Base64 `SecureString`); **(4)** hacer commit/PR `Back_Rodrigo → develop → main` y probar recuperación por Gmail; **(5)** tras despliegue CDK, cargar secrets `AWS_DEPLOY_ROLE_ARN` / `AWS_IAC_ROLE_ARN` + SSM `/sensus/prod/SesEmailSettings/ConfigurationSetName`, retirar llaves IAM viejas; **(6)** correr `dast-zap.yml`, D3 (push en 2 celulares) y D4 (`node .github/scripts/load-smoke.mjs --base-url https://zqqdwovx81.execute-api.us-east-2.amazonaws.com --rps 5 --duration-sec 120`).
14. **Dependencias nuevas añadidas:** únicamente `Cdklabs.CdkNag` `3.1.0` (Apache-2.0) y actualización de `Constructs` `10.5.0 → 10.5.1` en `Sensus.Infra`; **cero** paquetes nuevos en `src/` ni herramientas globales (RFC 6238 y `load-smoke.mjs` 100 % nativos).
15. **Próximo hito al despertar:** revisar decisiones provisionales, ejecutar la lista §5.Z/D3/D4 de día y cerrar `G-12` y `G-13` tras medir p95 en vivo.

Registro cronológico generado durante la ejecución autónoma nocturna conforme a [`docs/plan-ejecucion-remediacion-2026-11-26.md`](plan-ejecucion-remediacion-2026-11-26.md).

---

## Bloque A — Correcciones rápidas de código (A1–A4)

1. **Resumen en una línea:** Cerrados los 4 puntos del Bloque A (**A1/H4**, **A2/H1**, **A3/H3 backend + móvil**, **A4/`FR-034-05`**); en la suite de integración quedaron 2 fallos de contenedores (`DynamoDbLocalBudgetCounterStoreTests` y `MongoFeedSocialIntegrationTests`) que corresponden directamente al punto **D1**.

2. **Tabla por punto:**

| Punto | Horario (CST) | Estado | Evidencia (pruebas y conteos) | Qué falta |
|---|---|---|---|---|
| **A1** — Moderación *fail-closed* en `ContentModerationHelper` (H4, Spec 017) | 00:33 – 00:38 | **Hecho** | `ContentModerationHelperTests`: 5 pruebas rojas confirmadas antes del arreglo (`4` fallos/`27`); suite unitaria tras corrección **1180/1180** en verde. `specs/017-moderacion-multimodal/*` v1.3.0 (`FR-017-09`, `CA-017-07`, `T017-15`) + `check-specs` **61/61**. | Nada en código. Falta commit/PR por Rodrigo. |
| **A2** — Redacción de PII/prompts en logs (`ISensitiveRequest`, H1, Spec 014) | 00:38 – 00:44 | **Hecho** | Pruebas rojas (`LoggingBehaviorTests` + regla `ArchitectureTests`) confirmadas en rojo antes de marcar 21 comandos/queries; tras el cambio: `LoggingBehaviorTests` **4/4**, `ArchitectureTests` **6/6**. `specs/014-logs-estructurados-correlacion/*` v1.2.0 (`FR-014-05`, `CA-014-04`, `T014-08`). | Validar ausencia de payloads en CloudWatch tras despliegue. |
| **A3** — Rechazo de `imageUrl` externa JSON y validación de `mediaAssetId` en feed (H3, Spec 024 + móvil) | 00:45 – 00:56 | **Hecho** | Pruebas rojas `CreateFeedPost_RejectsExternalImageUrl` y `CreatePost_ReturnsBadRequest_WhenExternalImageUrlSentInJson` confirmadas antes del fix; tras arreglo: `FeedPostsTests` **17/17**, `FeedPostEndpointsTests` **14/14**. Móvil (`movil_Rodrigo` sobre `develop` fast-forward): `FeedDtos.kt` + `FeedAvailabilityApiTest.kt`, `gradlew.bat assembleDebug testDebugUnitTest --no-daemon` **BUILD SUCCESSFUL** (5m 39s). `specs/024-publicaciones-feed/*` v2.2.0 (`FR-024-08`, `T024-10`) + OpenAPI. | Revisar decisión provisional del alias `mediaId` y publicar nueva versión móvil antes de retirar alias. |
| **A4** — Límite configurable de 20 ofertas activas por comercio (`FR-034-05`, Spec 034) | 00:52 – 01:02 | **Hecho** | 4 pruebas rojas confirmadas antes del arreglo (`3` unitarias en `CreateOffer`/`ChangeOfferStatus`/`RepublishOffer` + `1` HTTP `409`). Tras implementación (`OfferLimitSettings` `[1,1000]`, índice `ix_offers_merchant_status_ends_at`, `MongoOfferRepository.CountActiveByMerchantAsync`, handlers + OpenAPI `409`): **33/33** unitarias A4, **7/7** `CreateOfferEndpointsTests`, **1/1** Testcontainers `MongoOfferActiveCountIntegrationTests` contra MongoDB 7.0 real. `specs/034-creacion-oferta/*` v1.1.0 (`T034-18`) + `requirements.md`. | Revisar decisión provisional sobre carrera mínima en concurrencia extrema (+1 sin transacción distribuida en M0). |

3. **Resultados de verificación global §6 (cierre Bloque A — 01:09 CST):**

| Verificación | Resultado real |
|---|---|
| `dotnet build sensus-back.sln -warnaserror` | **0 advertencias, 0 errores** |
| `dotnet format sensus-back.sln --verify-no-changes` | **Limpio (0 cambios)** |
| `dotnet test tests/Sensus.Application.UnitTests` | **1198 / 1198 superadas** (`0` fallos, `0` omitidas) |
| Cobertura `check-coverage.mjs` (umbral 80 %) | **Domain 90.84 %** (`3193/3515`) · **Application 89.44 %** (`7607/8505`) — **OK** |
| `dotnet test tests/Sensus.ArchitectureTests` | **6 / 6 superadas** |
| `dotnet test tests/Sensus.E2ETests` | **3 / 3 superadas** |
| `dotnet test tests/Sensus.IntegrationTests` | **696 / 698 superadas**, **2 fallos** (`DynamoDbLocalBudgetCounterStoreTests.Guard_RecordsSpendAndRejectsQuotaBudgetAndInvalidCounters` por `ResourceInUseException: Cannot create preexisting table`; `MongoFeedSocialIntegrationTests.Likes_AddRemoveAreIdempotentAndCountNeverNegative` por `Container exited with code 48`) |
| `node .github/scripts/check-specs.mjs specs` | **61 / 61 specs conformes** |
| `node --test run-ai-evals.test.mjs post-deploy-smoke.test.mjs` | **21 / 21 superadas** (`13` + `8`) |
| `node .github/scripts/run-ai-evals.mjs --mode deterministic` | **5 / 5 PASS (100.0 %)** |
| `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror` | **0 advertencias, 0 errores** |
| Móvil (`movil_Rodrigo`: `assembleDebug testDebugUnitTest`) | **BUILD SUCCESSFUL** |

4. **Decisiones provisionales del ejecutor (Bloque A):**
   - **A3 (`FeedEndpoints.cs` / `FeedDtos.kt`):** Se conservó la propiedad opcional `Guid? MediaId` como alias compatible hacia atrás junto con la propiedad canónica `Guid? MediaAssetId` en el DTO HTTP del backend (mientras en móvil se agregó `@SerialName("mediaAssetId")`). *Alternativa descartada:* exigir exclusivamente `mediaAssetId` rompiendo versiones móviles ya instaladas en producción. Revisar con Rodrigo cuándo retirar `mediaId`.
   - **A4 (`MongoOfferRepository` + handlers de oferta):** Verificación previa por conteo indexado (`CountDocumentsAsync` sobre `ix_offers_merchant_status_ends_at`) antes de moderar y persistir, aceptando una ventana transitoria mínima de concurrencia entre 2 publicaciones en el mismo milisegundo. *Alternativa descartada:* transacciones multi-documento en MongoDB Atlas M0 o contador paralelo sujeto a desincronización al expirar ofertas por reloj.

5. **Pendientes / acciones para Rodrigo:**
   - Revisar `git diff` en backend (`Back_Rodrigo`) y móvil (`movil_Rodrigo`) antes de hacer commit/PR cuando termine la jornada.
   - Confirmar las dos decisiones provisionales de Bloque A (retiro futuro del alias `mediaId` y riesgo aceptado de concurrencia en `CountActiveByMerchantAsync`).

---

## Bloque D (parcial programado) — Testcontainers (D1) y STRIDE (D2)

1. **Resumen en una línea:** Completados **D1** (0 fallos en las 698 pruebas de integración incluyendo todas las suites Testcontainers de MongoDB, Redis y DynamoDB Local) y **D2** (matrices STRIDE completas S/T/R/I/D/E en `001`–`004`, `029`, `030`/`060`/`061`, `033`, `034`; brecha **G-11** cerrada en `constitution.md`).

2. **Tabla por punto:**

| Punto | Horario (CST) | Estado | Evidencia (pruebas y conteos) | Qué falta |
|---|---|---|---|---|
| **D1** — Testcontainers pendientes y DynamoDB Local | 01:09 – 01:24 | **Hecho** | `docker info`: Server `27.4.0`. Corregido `DynamoDbLocalBudgetCounterStoreTests` (tabla única `sensus-sensi-budget-{Guid}`, timeout `15s`, espera `ListTablesAsync` e idempotencia ante retry), `MongoFeedSocialIntegrationTests` y `MongoOfferDiscoveryIntegrationTests` (`IClassFixture` con BD única + reintento ante `exitCode 48` de `mongo:7.0`). Cerradas `T039-09` (Spec 039) y `T026-08` (Spec 026). Corrida integral: **698 / 698** pruebas de integración en verde (`0` fallos). | Nada. |
| **D2** — Modelado de amenazas STRIDE (G-11) | 01:13 – 01:18 | **Hecho** | Añadidas/completadas tablas `Amenaza | Escenario | Control existente (con archivo/prueba) | Riesgo residual` con las 6 letras `S/T/R/I/D/E` en `plan.md` de `001`, `002`, `003`, `004`, `029`, `030` (+ `060`, `061`), `033` y `034`. `constitution.md` Apéndice F: `G-11` -> **Cerrada**. `check-specs` **61 / 61** conformes. | Nada. |

3. **Resultados de verificación global §6 (cierre D1+D2 — 01:24 CST):**

| Verificación | Resultado real |
|---|---|
| `dotnet build sensus-back.sln -warnaserror` | **0 advertencias, 0 errores** |
| `dotnet format sensus-back.sln --verify-no-changes` | **Limpio (0 cambios)** |
| `dotnet test tests/Sensus.Application.UnitTests` | **1198 / 1198 superadas** (`0` fallos) |
| Cobertura `check-coverage.mjs` (umbral 80 %) | **Domain 90.84 %** (`3193/3515`) · **Application 89.44 %** (`7607/8505`) — **OK** |
| `dotnet test tests/Sensus.ArchitectureTests` | **6 / 6 superadas** |
| `dotnet test tests/Sensus.E2ETests` | **3 / 3 superadas** |
| `dotnet test tests/Sensus.IntegrationTests` | **698 / 698 superadas** (`0` fallos, `0` omitidas; Testcontainers MongoDB + Redis + DynamoDB Local en verde) |
| `node .github/scripts/check-specs.mjs specs` | **61 / 61 specs conformes** |
| `node --test run-ai-evals.test.mjs post-deploy-smoke.test.mjs` | **21 / 21 superadas** (`13` + `8`) |
| `node .github/scripts/run-ai-evals.mjs --mode deterministic` | **5 / 5 PASS (100.0 %)** |
| `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror` | **0 advertencias, 0 errores** |

4. **Decisiones provisionales del ejecutor (D1+D2):**
   - **D1 (`MongoFeedSocialIntegrationTests`, `MongoOfferDiscoveryIntegrationTests`):** En lugar de levantar 1 contenedor Docker nuevo por cada `[Fact]` en paralelo (lo que provocaba colisiones de socket en el *entrypoint* dual de `mongo:7.0`, `exitCode 48`), se compartió 1 contenedor por clase vía `IClassFixture`, conservando una base de datos única (`Guid`) creada y eliminada por cada prueba e incluyendo 1 reintento en el arranque del contenedor. *Alternativa descartada:* desactivar ejecución paralela de toda la solución xUnit (degradaría tiempos de CI).

5. **Pendientes / acciones para Rodrigo:**
   - Ninguna acción manual extra para D1 o D2 (solo incluir en commit/PR final).

---

## Bloque B — CI e infraestructura DevSecOps (B1–B5)

1. **Resumen en una línea:** Implementados en código, IaC y workflows los 5 puntos del Bloque B (**B1** environment `production`, **B2** OIDC GitHub→AWS, **B3** monitoreo SES, **B4** `Cdklabs.CdkNag` 3.1.0 + SBOM `CycloneDX 6.3.2`/SLSA 2, **B5** ZAP DAST baseline + revisión `G-12`); las operaciones que requieren GitHub UI, AWS `login`/`cdk deploy` o ejecución diurna quedan documentadas para Rodrigo.

2. **Tabla por punto:**

| Punto | Horario (CST) | Estado | Evidencia (pruebas y conteos) | Qué falta |
|---|---|---|---|---|
| **B1** — Aprobación manual `production` (`FR-045-19`, Spec 045) | 01:25 – 01:38 | **Hecho en código** | `deploy-serverless.yml` con `environment: production` y URL pública; `T045-20` + `specs/requirements.md`. | Rodrigo debe crear el environment `production` en GitHub Settings con revisores y rama `main`. |
| **B2** — OIDC AWS en lugar de llaves (`FR-045-20`, Spec 045) | 01:25 – 01:38 | **Hecho en código** | `SensusInfraStack.GitHubOidc.cs` (`sensus-github-deploy-<env>`, `sensus-github-iac-readonly-<env>`, outputs); `deploy-serverless.yml` y `ci.yml` con `configure-aws-credentials@7474bc4...` (v4.3.1). Compilación y síntesis `Sensus.Infra` `0/0`. | Rodrigo debe verificar `aws iam list-open-id-connect-providers`, desplegar CDK y cargar `AWS_DEPLOY_ROLE_ARN` / `AWS_IAC_ROLE_ARN` antes de retirar llaves. |
| **B3** — Monitoreo SES rebotes/quejas (`FR-029-14`, Spec 029) | 01:25 – 01:38 | **Hecho en código** | `SensusInfraStack.Email.cs` (`CfnConfigurationSet` + `CloudWatchDestination` + alarma SNS 8/10 Free Tier + permiso `ses:SendEmail`); `SesEmailSenderTests` verifica envío de `ConfigurationSetName`. `T029-24`. | Tras `cdk deploy`, Rodrigo debe crear `/sensus/prod/SesEmailSettings/ConfigurationSetName` = `sensus-transactional-prod` en SSM. |
| **B4** — `cdk-nag` y SBOM CycloneDX / SLSA 2 (`FR-047-19`, `FR-047-20`, `G-03`, `G-04`) | 01:27 – 01:39 | **Hecho en código** | Nueva dep. NuGet: **`Cdklabs.CdkNag` 3.1.0** (Apache-2.0, costo 0 USD) y **`Constructs` 10.5.1** (Apache-2.0, costo 0 USD); `SensusNightSwitchFunction` actualizado a `PYTHON_3_14`; `app.Synth()` local con `AwsSolutionsChecks` pasó con **0 errores** (`exitCode 0`). `CycloneDX 6.3.2` + `attest-build-provenance@4d10147...` en workflows. | Verificar `cdk synth` y artefactos SBOM/atestación en la primera corrida real de CI/CD. |
| **B5** — ZAP DAST pasivo + revisión Figma (`FR-047-21`, `G-02`, `G-12`) | 01:27 – 01:39 | **Preparado** | `.github/workflows/dast-zap.yml` (`zaproxy/action-api-scan@77dfa9a...` v0.9.0, lunes 11:00 CDMX / manual, OpenAPI extraído localmente en `Development`) + `.zap/rules.tsv`; `T047-23`. Móvil: confirmado `.gitignore` para `mcp_config.json` y `.agent/mcp_config.json` sin leer secretos; `mcp_config.example.json` usa `${FIGMA_API_KEY}`. | Rodrigo debe disparar `dast-zap.yml` de día tras el PR y revocar la llave de Figma en Settings → Personal access tokens (`G-12`). |

3. **Resultados de verificación global §6 (cierre Bloque B — 01:45 CST):**

| Verificación | Resultado real |
|---|---|
| `dotnet build sensus-back.sln -warnaserror` | **0 advertencias, 0 errores** |
| `dotnet format sensus-back.sln --verify-no-changes` & `infra` | **Limpos (0 cambios)** |
| `dotnet test tests/Sensus.Application.UnitTests` | **1198 / 1198 superadas** (`0` fallos) |
| Cobertura `check-coverage.mjs` (umbral 80 %) | **Domain 90.84 %** (`3193/3515`) · **Application 89.44 %** (`7607/8505`) — **OK** |
| `dotnet test tests/Sensus.ArchitectureTests` | **6 / 6 superadas** |
| `dotnet test tests/Sensus.E2ETests` | **3 / 3 superadas** |
| `dotnet test tests/Sensus.IntegrationTests` | **698 / 698 superadas** (`0` fallos) |
| `node .github/scripts/check-specs.mjs specs` | **61 / 61 specs conformes** |
| `node --test run-ai-evals.test.mjs post-deploy-smoke.test.mjs` | **21 / 21 superadas** |
| `node .github/scripts/run-ai-evals.mjs --mode deterministic` | **5 / 5 PASS (100.0 %)** |
| `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror` + `app.Synth()` (`CdkNag`) | **0 advertencias, 0 errores; 0 hallazgos sin reconocer** |

4. **Dependencias nuevas introducidas (Bloque B):**
   - `Cdklabs.CdkNag` `3.1.0` en `infra/src/Sensus.Infra/Sensus.Infra.csproj` — licencia Apache-2.0, costo 0 USD (justificada en `specs/047-ci-cd-pipeline/plan.md`).
   - `Constructs` `10.5.0` → `10.5.1` (misma librería base de CDK, requerida por `CdkNag 3.1.0`), licencia Apache-2.0, costo 0 USD.
   - En CI únicamente: `CycloneDX` `6.3.2` (herramienta .NET para generar `bom.json`).

5. **Decisiones provisionales del ejecutor (Bloque B):**
   - **B2 (`SensusInfraStack.GitHubOidc.cs`):** Como las credenciales locales de AWS habían expirado (`aws: [ERROR]: Your session has expired`), no se pudo comprobar si ya existía `token.actions.githubusercontent.com` en IAM. Se implementó creación por defecto con opción de contexto `-c existingGithubOidcProviderArn=arn:...` para importarlo sin choque si ya existe. *Alternativa descartada:* asumir que no existe sin vía de importación.
   - **B4 (`SensusInfraStack.CacheAndSchedule.cs`):** Se actualizó `SensusNightSwitchFunction` de `PYTHON_3_12` a `PYTHON_3_14` para resolver `AwsSolutions-L1` directamente en lugar de suprimirlo; los demás hallazgos de Free Tier (p. ej. `VPC7`, `APIG1`, `APIG4`, `DDB3`, `SNS2`, `IAM4`/`IAM5`) se reconocieron explícitamente con justificación escrita.
   - **B5 (`dast-zap.yml`):** El esquema `/openapi/v1.json` se extrae arrancando `Sensus.Api` en memoria durante el job en lugar de exponer Swagger en producción o mantener un archivo OpenAPI estático paralelo.

6. **Pendientes / acciones exactas para Rodrigo (Bloque B):**
   1. En GitHub (`sensus-proj/sensus-back` → Settings → Environments): crear **`production`**, marcar **Required reviewers** (tu usuario) y **Deployment branches → Selected branches → `main`**.
   2. Ejecutar `aws login` en consola local y luego:
      `aws iam list-open-id-connect-providers`
      Si aparece `token.actions.githubusercontent.com`, pasar `-c existingGithubOidcProviderArn=<ARN>` al primer despliegue CDK.
   3. Ejecutar el flujo de PR `Back_Rodrigo → develop → main` (con las llaves actuales aún activas en el fallback transitorio) para desplegar los roles OIDC y el configuration set de SES; copiar `GitHubDeployRoleArn` → Secret `AWS_DEPLOY_ROLE_ARN` y `GitHubIacReadOnlyRoleArn` → Secret `AWS_IAC_ROLE_ARN`.
   4. Tras validar un despliegue exitoso usando OIDC, eliminar los secretos heredados `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` de GitHub y desactivar la llave IAM de `github-cdk-deployer`.
   5. Tras el despliegue CDK: crear parámetro SSM `/sensus/prod/SesEmailSettings/ConfigurationSetName` = `sensus-transactional-prod` (String estándar).
   6. Revocar en Figma (Settings → Personal access tokens) la llave usada históricamente en móvil (`G-12`) y exportar la nueva en la variable de entorno local `FIGMA_API_KEY`.
   7. De día (`07:00–22:30` CST) tras el despliegue, disparar manualmente en GitHub Actions el workflow **DAST - OWASP ZAP Baseline** (`dast-zap.yml`) y verificar artefacto limpio.

---

## Bloque C — Seguridad de aplicación, moderación y autenticación (C1–C5)

1. **Resumen en una línea:** Completados en backend y móvil los 5 puntos del Bloque C (**C1** cuarentena `FeedPost` + purga 30 d, **C2** `202 Accepted` y worker de re-moderación de ofertas, **C3/H2** moderación previa a Cloudinary + `type=authenticated` + retiro de bypass en móvil, **C4/`G-06`** JWT 15 min + `RefreshToken` rotatorio en backend y móvil, **C5/`G-05`** TOTP RFC 6238 `HMACSHA1` sin `Otp.NET` + AES-256-GCM + *step-up* `AdminOnlyPolicy`); todas las puertas de verificación de §6 pasaron al 100 % tras C5.

2. **Tabla por punto:**

| Punto | Horario (CST) | Estado | Evidencia (pruebas y conteos) | Qué falta |
|---|---|---|---|---|
| **C1** — Cuarentena *fail-closed*, auditoría `FeedPost` y purga 30 d (Specs 024 / 017 / 021) | 01:46 – 02:02 | **Hecho** | `FeedPostsTests\|NearbyFeedTests\|PurgeQuarantinedMediaCommandHandlerTests` **53/53**; `FeedPostEndpointsTests\|MongoFeedSocialIntegrationTests` **25/25** (incluye Red→Green en cuarentena y purga Testcontainers); `OpenApiContractTests` **1/1**. `202 Accepted` con `Location` cuando queda en `Quarantined` y sin subida a Cloudinary ni URL pública. | Nada en código. Commit/PR pendiente por Rodrigo. |
| **C2** — Moderación pendiente `202 Accepted` en ofertas y worker de re-moderación (`FR-034-07`, Spec 034) | 02:02 – 02:19 | **Hecho** | `OfferStatus.PendingModeration`/`Rejected`, `OfferModerationRetryBackgroundService`, índice `ix_offers_pending_moderation_retry` y ocultamiento público en detalle/feed/likes/favoritos/Sensi. `UnitTests` **122/122**; `IntegrationTests` (`CreateOfferEndpointsTests`, `MongoOfferActiveCountIntegrationTests`, `OpenApiContractTests`) **11/11**; `specs/034` v1.3.0. | Validar ejecución del worker en entorno real tras despliegue. |
| **C3** — Moderar antes de Cloudinary (`type=authenticated`) y móvil `movil_Rodrigo` (H2, Spec 022) | 02:19 – 02:31 | **Hecho** | `UploadMediaCommandHandler` modera bytes antes de `_mediaStorage.UploadImageAsync` (cero subida en `Quarantined`/`Rejected`), `CloudinaryMediaStorage` firma con `type=authenticated` y `/api/v1/media/upload` suma `SensitiveWritePolicy`. En móvil (`movil_Rodrigo`): eliminados fallback firmado directo y llamada a `/media/moderate`; foto de perfil enrutada vía `POST /api/v1/media/upload`. Backend unit **95/95**, integración **27/27**; móvil `testDebugUnitTest` **BUILD SUCCESSFUL**. | Autorización y migración futura de activos públicos previos en Cloudinary; revisar decisión provisional sobre `/users/photo-signature`. |
| **C4** — JWT ≤ 15 min + *refresh token* rotatorio en backend y móvil (`G-06`, Spec 002) | 02:31 – 02:41 | **Hecho** | `JwtSettings:ExpiryMinutes=15` (`[1..15]`), colección `refresh_tokens` con TTL y `TryMarkAsUsedAsync` atómico + revocación de familia y rotación de `SecurityStamp` en reuso; móvil `TokenRefreshAuthenticator` con rotación single-flight en `NetworkClient`. Backend unit **18/18**, integración/Testcontainers **22/22**; móvil `TokenRefreshAuthenticatorTest` + `RetrofitRepositoriesTest` **BUILD SUCCESSFUL**. `G-06` **Cerrada en código**. | Probar flujo de expiración/refresh tras despliegue de nueva versión móvil. |
| **C5** — Segundo factor TOTP `Administrador` + *step-up* (`G-05`, Spec 062) | 02:41 – 03:04 | **Hecho en código** | RFC 6238 nativo con `HMACSHA1` de .NET (sin `Otp.NET`; 6 vectores SHA-1 Apéndice B en verde), secreto cifrado `AesGcm` 256 bits (`Mfa:EncryptionKey`), 8 recovery codes SHA-256 de un solo uso, desafío intermedio en login sin sesión operativa, `/auth/mfa/enroll`, `/auth/mfa/verify`, bloqueo al 5º fallo + auditoría `AdminMfa` y *step-up* `amr=mfa` + recencia `mfa_at` en `AdminOnlyPolicy`. Red→Green (`11→0` / `4→0` fallos iniciales → `12/12` unitarias + `66/66` integración + rutas Admin); `G-05` **Cerrada en código**. | Rodrigo debe provisionar `/sensus/prod/Mfa/EncryptionKey` (32 bytes Base64 `SecureString`) en AWS SSM antes de que un Admin enrole TOTP en producción. |

3. **Resultados de verificación global §6 (cierre Bloque C — 03:14 CST):**

| Verificación | Resultado real |
|---|---|
| `dotnet build sensus-back.sln -warnaserror` | **0 advertencias, 0 errores** |
| `dotnet format sensus-back.sln --verify-no-changes` & `infra` | **Limpios (0 cambios)** |
| `dotnet test tests/Sensus.Application.UnitTests` | **1215 / 1215 superadas** (`0` fallos, `0` omitidas) |
| Cobertura `check-coverage.mjs` (umbral 80 %) | **Domain 90.09 %** (`3344/3712`) · **Application 88.40 %** (`8187/9261`) — **OK** |
| `dotnet test tests/Sensus.ArchitectureTests` | **6 / 6 superadas** |
| `dotnet test tests/Sensus.E2ETests` | **3 / 3 superadas** |
| `dotnet test tests/Sensus.IntegrationTests` | **726 / 726 superadas** (`0` fallos, `0` omitidas) |
| `node .github/scripts/check-specs.mjs specs` | **62 / 62 specs conformes** |
| `node --test run-ai-evals.test.mjs post-deploy-smoke.test.mjs` | **21 / 21 superadas** |
| `node .github/scripts/run-ai-evals.mjs --mode deterministic` | **5 / 5 PASS (100.0 %)** |
| `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror` | **0 advertencias, 0 errores** |
| Móvil (`movil_Rodrigo`: `assembleDebug testDebugUnitTest --no-daemon`) | **BUILD SUCCESSFUL** (3m 56s) |

4. **Dependencias nuevas introducidas (Bloque C):** Ninguna (C5 implementa TOTP RFC 6238 con `HMACSHA1` nativo de .NET, sin `Otp.NET`).

5. **Decisiones provisionales del ejecutor (Bloque C):**
   - **C1 (`Spec 024`):** La extensión de `FeedPost` conserva compatibilidad hacia atrás; en cuarentena no se sube imagen a Cloudinary (`ImageUrl = null`) y `202 Accepted` devuelve la ubicación del recurso.
   - **C3 (`Spec 022`):** Se conservó el endpoint legado `/api/v1/users/photo-signature` mientras coexisten clientes anteriores en producción; la app móvil ya sube foto de perfil por `POST /api/v1/media/upload`. *Pendiente Rodrigo:* confirmar retiro futuro de `/users/photo-signature` y autorizar estrategia de migración de imágenes públicas previas en Cloudinary.
   - **C4 (`Spec 002`):** Los campos `refreshToken`, `refreshExpiresAtUtc` son aditivos y opcionales en `AuthResponseDto` para mantener compatibilidad con versiones previas durante el despliegue.
   - **C5 (`Spec 062`):** `AdminOnlyPolicy` exige `amr=mfa` + recencia (`mfa_at ≤ 15 min`) en **todas** las rutas administrativas sin excepción de gracia para cuentas no enroladas; el enrolamiento inicial usa `AdminBasePolicy`. *Alternativa descartada:* eximir administradores sin MFA activo.

6. **Pendientes / acciones exactas para Rodrigo (Bloque C):**
   1. Provisionar en AWS SSM Parameter Store (us-east-2) antes del primer enrolamiento TOTP de Administrador en producción:
      - Nombre: `/sensus/prod/Mfa/EncryptionKey` · Tipo: `SecureString` · Valor: 32 bytes aleatorios codificados en Base64 (ej. generado con `[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`).
   2. Enrolar 2FA en la cuenta Administrador con `POST /api/v1/auth/mfa/enroll` y confirmar el primer código vía `POST /api/v1/auth/mfa/verify`, guardando los 8 códigos de recuperación fuera del repositorio.
   3. Revisar las decisiones provisionales de C3 (retiro futuro de `/users/photo-signature` y migración de activos Cloudinary públicos históricos) y C5 (`StepUpWindowMinutes = 15` sin exención de gracia).

---

## Preparación D3 y D4 (solo preparación; cero llamadas a producción)

| Punto | Horario (CST) | Estado | Evidencia (pruebas y conteos) | Qué falta (ejecución diurna de Rodrigo) |
|---|---|---|---|---|
| **D3** — Push real con FCM en 2 celulares (`027`/`033`) | 03:15 – 03:17 | **Preparado** | Runbook operativo listo abajo (sin tocar dispositivos ni API nocturna). | Ejecutar en producción entre `07:00` y `22:30` CST tras confirmar SSM `/sensus/prod/push/fcm-service-account`. |
| **D4** — Validación carga controlada y percentiles (`G-13`, Spec `046`/`050`) | 03:15 – 03:17 | **Preparado** | Creados [`.github/scripts/load-smoke.mjs`](../.github/scripts/load-smoke.mjs) y [`.github/scripts/load-smoke.test.mjs`](../.github/scripts/load-smoke.test.mjs) (Red→Green: `3` fallos → `5/5` locales; suite JS completa **26/26** superadas). Tasa fija en `1..5` rps y bloqueo estricto de `/api/v1/ai/*`. | Ejecutar contra producción de día (`07:00–22:30` CST) y registrar p50/p95/p99 en checklists y `G-13`. |

### Runbook D3 — Push real FCM en tus 2 celulares (de día, 07:00–22:30 CST)

1. Confirmar credencial en SSM y dispositivos conectados:
   - `aws ssm describe-parameters --parameter-filters Key=Name,Values=/sensus/prod/push/fcm-service-account --region us-east-2`
   - `adb devices` (verificar que los dos celulares visibles tienen la compilación de `movil_Rodrigo` apuntando a producción).
2. Iniciar sesión con tu cuenta en ambos teléfonos y validar permisos de notificación activos.
3. Probar los tres estados en cada equipo (primer plano, segundo plano y detenida tras `adb -s <id> shell am force-stop sensus.com`) disparando un apartado de una oferta propia de prueba.
4. Registrar llegada del push (`adb -s <id> shell dumpsys notification --noredact | findstr sensus`) y anotar la evidencia en [`specs/027-registro-dispositivo/checklist.md`](../specs/027-registro-dispositivo/checklist.md) y [`specs/033-apartado-con-qr/checklist.md`](../specs/033-apartado-con-qr/checklist.md).

### Runbook D4 — Carga controlada <= 5 rps sin `/api/v1/ai/*` (de día, 07:00–22:30 CST)

```powershell
node .github/scripts/load-smoke.mjs `
  --base-url https://zqqdwovx81.execute-api.us-east-2.amazonaws.com `
  --rps 5 `
  --duration-sec 120 `
  --max-p95-ms 700 `
  --out $env:TEMP\sensus-load-smoke.md
```

- Este comando usa sesión de invitado en `/health/ready`, `/api/v1/offers/nearby` y `/api/v1/feed/nearby`, rechaza automáticamente cualquier ruta `/api/v1/ai/*` y reporta `p50`, `p95`, `p99` y tasa de errores sin registrar cuerpos HTTP ni tokens.
- Al concluir, compara los `p95` obtenidos contra los objetivos NFR (Specs 033, 048, 050) y actualiza `G-13` en [`constitution.md`](../constitution.md).

---

