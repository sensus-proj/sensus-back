# Avance de cierre de specs — 2026-11-26

## RESUMEN EJECUTIVO

Se ejecutaron los bloques **A → B → C → D** de [`docs/plan-ejecucion-cierre-specs-2026-11-26.md`](plan-ejecucion-cierre-specs-2026-11-26.md) sin realizar commits, pushes ni llamadas a producción o AWS:
- **Bloque A (Hecho A1–A7):** `/auth/me` migrado a `CurrentUserDto` + `GetCurrentUserQuery` (002), retiro `410 Gone` de `/api/v1/users/photo-signature` y alias `mediaId` + validación exacta Cloudinary e índice `ix_media_assets_owner_secure_url` (003/022/024), limpieza móvil en `movil_Rodrigo` (`testDebugUnitTest` + `koverVerifyDebug` PASS), reintento único acotado ≤ 20 s en moderación OpenAI (017), `DefaultRouteSettings` 50/100 mitigando `G-10` (045), 6 pruebas Python de `night-switch` en CI (051) y 7 pruebas `deviceId` de logout (004).
- **Bloque B (Hecho B1, B3–B6; Parcial B2 por Docker en `019`):** cierre SDD de las 62 specs (`001`–`062`) con evidencia verificada en código/pruebas, nuevas pruebas en `MongoUserAndComercioRepositoryTests`, `AuthEndpointsTests`, `MerchantProfileEndpointsTests` y `MongoMediaAssetRepositoryTests`, Principio VIII PASS sobre CI `4a00d24`, `028` marcada en `backlog`, y brechas operativas etiquetadas exclusivamente con `§5.Z-1..13`.
- **Bloque C (Hecho):** redactados los 13 runbooks accionables (`§5.Z-1` a `§5.Z-13`) con comandos exactos y textos de evidencia listos para cerrar al ejecutarse de día.
- **Bloque D (Parcial por Docker post-reinicio):** `check-specs.mjs` **62/62**, `specs/requirements.md` (**635** requisitos), build `-warnaserror` `0/0`, `dotnet format` limpio, unitarias **1224/1224** (Domain **90.05 %**, Application **88.64 %**), arquitectura **6/6**, E2E **3/3**, `Sensus.Infra.Tests` **2/2**, JS **26/26**, Evals deterministas **5/5**, Python **6/6** e integración sin Docker **674/674** en verde tras la última edición (`757/757` antes del reinicio del equipo). Tras el reinicio del sistema, Docker Desktop quedó apagado (`DOCKER_EC=1`), por lo que quedan pendientes de encender Docker: la corrida de Testcontainers (incluido `QuarantineLifecycle*` de `019`) y `gitleaks`.
- **Siguiente acción para Rodrigo:** revisar cambios en `Back_Rodrigo` y `movil_Rodrigo`, encender Docker para re-correr Testcontainers + `gitleaks`, autorizar commits/PR y ejecutar `§5.Z-1` a `§5.Z-13` en horario diurno (`07:00–22:30 America/Mexico_City`).

---

## 1. Avance por punto (`A → B → C → D`)

| Punto | Estado | Evidencia (pruebas y conteos) | Qué falta |
|---|---|---|---|
| **A1** (`002`, `T002-09`) | **Hecho** | `CurrentUserDto`, `GetCurrentUserQuery`, `AuthEndpoints.cs`; `GetCurrentUserQueryHandlerTests` **3/3**, `AuthMeEndpointTests` **3/3**, `OpenApiContractTests` **1/1**; `openapi.yaml`, `tasks.md`, `checklist.md`, `requirements.md` | Rotación JWT en SSM (`§5.Z-5`) |
| **A2** (`003`, `022`, `024`) | **Hecho** | `410 Gone` (`Media.SignatureRetired`) en `POST /api/v1/users/photo-signature`, retiro alias `mediaId`, `IsApprovedOwnedUrlAsync` + `ix_media_assets_owner_secure_url`, `CloudinaryAccountSettings`; `UpdateProfile*` **20/20**, `ProfileEndpointsTests\|FeedPostEndpointsTests\|MongoMediaAssetRepositoryTests\|OpenApiContractTests` **45/45**, `E2ETests` **3/3** | — |
| **A3** (móvil `movil_Rodrigo`) | **Hecho** | `rg` `photo-signature\|PhotoSignature\|CloudinarySignedUploader` en `app\src` = **0** coincidencias; `check_secrets.py` (521), `check_sdd.py` (43), `check_layers.py` (359), `check_endpoints.py` (60) **PASS**; `:app:testDebugUnitTest :app:koverXmlReportDebug :app:koverVerifyDebug` **BUILD SUCCESSFUL** | `detekt` (3) y `ktlintTestSourceSetCheck` (1) fallan únicamente en 4 archivos `MyClaims*` modificados por otra sesión concurrente (intactos) |
| **A4** (`017`, `T017-03`, `T017-16`) | **Hecho** | `ModerationHttpRetryHelper`, `OpenAiSettingsValidator` (techo `<= 20 s`, `ValidateOnStart`, `TimeoutSeconds=8` en `appsettings.json`); Rojo→Verde: 5 fallos → **61/61** (`OpenAiTextModerationServiceTests`, `OpenAiImageModerationServiceTests`, `OpenAiSettingsValidatorTests`); `requirements.md` (`FR-017-10`, `NFR-017-03`) | — |
| **A5** (`045`, G-10, `T045-22`) | **Hecho** | `DefaultRouteSettings` (`ThrottlingRateLimit=50`, `ThrottlingBurstLimit=100`, contexto `apiThrottleRate`/`apiThrottleBurst`) en `SensusInfraStack.cs`; `Sensus.Infra.Tests` **2/2** (`Template.FromStack` + `AwsSolutionsChecks`); `G-10` actualizada en `constitution.md` | Despliegue en AWS (`§5.Z-1`); aprobación de costo WAF |
| **A6** (`051`, `T051-11`) | **Hecho** | `infra/assets/night-switch/test_index.py` **6/6 OK**, integrado en `.github/workflows/ci.yml` (`setup-python@a26af69...` + `unittest`) y `Sensus.Infra.Tests` | Verificación en AWS post-despliegue (`§5.Z-1`, `§5.Z-7`) |
| **A7** (`004`, `T004-11`) | **Hecho** | `LogoutDeviceEndpointsTests` **7/7** (sin `deviceId`, propio dejando hermanos activos, ajeno 204 sin tocar, inexistente 204, inválido `""`/`"   "`/`>100` → 400 keeping JWT vivo); `004` y `requirements.md` (`FR-004-05`) | — |
| **B1** (`001`, `002`, `003`, `005`, `006`, `031`) | **Hecho** | Añadidos `MongoUserAndComercioRepositoryTests`, `AuthEndpointsTests`, `MerchantProfileEndpointsTests` (`409`/`404`/`400`) y `SecurityPipelineTests` (`POST /api/v1/comercio/registro` 404); STRIDE añadido en `005`/`006`/`031`; run pre-reinicio Testcontainers+Endpoints **43/43 PASS** | Re-ejecutar Testcontainers tras encender Docker; rotación JWT (`§5.Z-5`) |
| **B2** (`008`, `013`, `015`, `016`, `019`, `022`, `027`, `034`) | **Parcial** | DoR/DoD/Constitution I–VIII cerrados con evidencia en todas (`027` deja casilla física `§5.Z-6`); añadida `QuarantineLifecycle_GetByIdQueryExpiredAndUpdate_WorksAgainstRealMongoDb` en `MongoMediaAssetRepositoryTests.cs` (compila `0/0`) | Ejecutar `QuarantineLifecycle_GetByIdQueryExpiredAndUpdate_WorksAgainstRealMongoDb` con Docker encendido (`DOCKER_EC=1` tras reinicio) |
| **B3** (`020`, `026`, `039`, `053`) | **Hecho** | Principio VIII PASS en `checklist.md` citando STRIDE, `ci.yml` en verde (`4a00d24`) y `dast-zap.yml` (`020` registra estado exacto de `G-02` y `G-04` Parcial móvil) | Ejecución diurna `dast-zap.yml` (`§5.Z-10`) y pendiente móvil `G-04` |
| **B4** (`017` `T017-04`/`T017-08`) | **Hecho** | Evidencia verificada en `CreateOfferCommandHandler.cs:114-188`, `ProcessPendingOfferModerationCommandHandler`, `UploadMediaCommandHandler`, `CreateFeedPostCommand`; `017/tasks.md` y `017/checklist.md` cerrados | — |
| **B5** (`034` `T034-07` y DoR) | **Hecho** | **37/37** unit (`ProcessPendingOfferModeration\|CreateOffer`) + **8/8** `CreateOfferEndpointsTests`; `034/tasks.md`, `034/checklist.md` y `requirements.md` (`FR-034-03`, `NFR-034-02` = `Implementado`) cerrados | — |
| **B6** (`028` backlog) | **Hecho** | `specs/028-webhooks-entrantes/tasks.md` (`Pendiente (backlog, specs/README.md)`) y `checklist.md` (`(backlog)`) sin marcar casillas | Decisión futura de producto |
| **C** (Runbooks `§5.Z-1..13`) | **Hecho** | 13 runbooks redactados en §5 de este reporte con comandos exactos, verificación y texto de cierre | Ejecución diurna por el usuario |
| **D** (Verificación global + SDD + memoria) | **Parcial** | `check-specs.mjs` **62/62**, `specs/README.md` y `memory.md` v3.7.13 actualizados, `specs/requirements.md` **635** requisitos; gates estáticos, unit, cobertura, arch, E2E, `Infra.Tests`, JS, evals, Python e integración sin Docker en verde | Encender Docker Desktop para correr Testcontainers completo (`Mongo*`, `Redis*`, `DynamoDbLocal*`) + `gitleaks` |

---

## 2. Estado final por spec (`001`–`062`)

| Spec | Tareas cerradas / total | Casillas cerradas / total | Pendiente y por qué |
|---|---|---|---|
| `001` | 9 / 9 | 25 / 26 | `§5.Z-5` (rotación operativa JWT en SSM) |
| `002` | 10 / 10 | 24 / 25 | `§5.Z-5` (rotación operativa JWT en SSM) |
| `003` | 11 / 11 | 24 / 24 | — |
| `004` | 11 / 11 | 24 / 24 | — |
| `005` | 8 / 8 | 25 / 25 | — |
| `006` | 6 / 6 | 24 / 24 | — |
| `007` | 13 / 13 | 30 / 30 | — |
| `008` | 7 / 7 | 24 / 24 | — |
| `009` | 8 / 8 | 24 / 24 | — |
| `010` | 7 / 7 | 23 / 23 | — |
| `011` | 8 / 8 | 23 / 23 | — |
| `012` | 7 / 7 | 23 / 23 | — |
| `013` | 7 / 7 | 24 / 24 | — |
| `014` | 8 / 8 | 23 / 23 | — |
| `015` | 7 / 7 | 24 / 24 | — |
| `016` | 8 / 8 | 23 / 23 | — |
| `017` | 16 / 16 | 27 / 27 | — |
| `018` | 8 / 8 | 22 / 22 | — |
| `019` | 9 / 9 | 23 / 23 | Nota: `QuarantineLifecycle*` pendiente de ejecución con Docker (`DOCKER_EC=1`) |
| `020` | 20 / 20 | 38 / 38 | Nota: `G-02` ejecución diurna (`§5.Z-10`); `G-04` Parcial en móvil |
| `021` | 10 / 10 | 24 / 24 | — |
| `022` | 13 / 13 | 25 / 25 | — |
| `023` | 7 / 7 | 24 / 24 | — |
| `024` | 12 / 12 | 33 / 33 | — |
| `025` | 11 / 11 | 24 / 24 | — |
| `026` | 10 / 10 | 26 / 26 | — |
| `027` | 13 / 16 | 26 / 27 | `T027-07`, `T027-12`, `T027-13` y casilla Android: `§5.Z-6` (FCM en SSM + 2 celulares) |
| `028` | 0 / 3 | 0 / 23 | `backlog` (`specs/README.md`) |
| `029` | 23 / 24 | 32 / 32 | `T029-23` `§5.Z-4` (salida sandbox SES + prueba app); `T029-24` nota `§5.Z-3` (SSM tras deploy) |
| `030` | 13 / 13 | 27 / 27 | `Escindida` en `060` y `061` (+ `FR-030-08` cerrado en `T030-13`) |
| `031` | 11 / 11 | 25 / 25 | — |
| `032` | 10 / 10 | 23 / 23 | — |
| `033` | 17 / 17 | 28 / 32 | `§5.Z-6` (3 casillas FCM real en 2 celulares) y `§5.Z-6`+`§5.Z-10` (1 casilla carga + FCM) |
| `034` | 19 / 19 | 30 / 30 | — |
| `035` | 12 / 12 | 25 / 25 | — |
| `036` | 14 / 14 | 23 / 23 | — |
| `037` | 10 / 10 | 26 / 26 | — |
| `038` | 9 / 9 | 30 / 30 | — |
| `039` | 11 / 11 | 25 / 25 | — |
| `040` | 11 / 11 | 24 / 24 | — |
| `041` | 12 / 12 | 25 / 25 | — |
| `042` | 11 / 11 | 16 / 18 | `§5.Z-10` (carga `NFR-042-01/02`) y `§5.Z-13` (aviso privacidad + FT-23) |
| `043` | 12 / 12 | 25 / 25 | — |
| `044` | 15 / 15 | 29 / 29 | Nota en `T044-13`: entrega física FCM sujeta a `§5.Z-6` |
| `045` | 22 / 22 | 30 / 30 | Verificación tras despliegue `§5.Z-1` y OIDC GitHub `§5.Z-2` documentadas en notas |
| `046` | 0 / 0 * | 22 / 22 | *Retro-spec sin filas `\| T046-xx \|` en `tasks.md` (implementación y checklist 22/22 cerrados) |
| `047` | 23 / 23 | 33 / 33 | — |
| `048` | 26 / 26 | 36 / 36 | — |
| `049` | 10 / 11 | 15 / 17 | `T049-10` + 2 casillas: `§5.Z-8` (turno real DynamoDB) y `§5.Z-11` (`cdk diff`) |
| `050` | 6 / 7 | 10 / 11 | `T050-07` + 1 casilla: `§5.Z-9` (validación en vivo post-despliegue) |
| `051` | 10 / 11 | 22 / 23 | `T051-08` + 1 casilla: `§5.Z-7` (invocar `stop`/`start` y comprobar alarma); `T051-09`/`10` notas `§5.Z-1` |
| `052` | 3 / 3 | 9 / 9 | — |
| `053` | 7 / 7 | 10 / 10 | — |
| `054` | 7 / 7 | 13 / 13 | — |
| `055` | 8 / 8 | 13 / 15 | `§5.Z-11` (`cdk diff`) y `§5.Z-13` (aviso privacidad + FT-23) |
| `056` | 0 / 0 * | 0 / 0 * | *Retro-spec sin tabla `T056` ni casillas `- [ ]` (cerrada e implementada con pruebas Mongo reales) |
| `057` | 0 / 0 * | 0 / 0 * | *Retro-spec sin tabla `T057` ni casillas `- [ ]` (cerrada) |
| `058` | 0 / 0 * | 0 / 0 * | *Retro-spec sin tabla `T058` ni casillas `- [ ]` (cerrada) |
| `059` | 0 / 0 * | 0 / 0 * | *Retro-spec sin tabla `T059` ni casillas `- [ ]` (cerrada) |
| `060` | 12 / 12 | 27 / 27 | — |
| `061` | 12 / 12 | 26 / 26 | — |
| `062` | 5 / 5 | 24 / 24 | Verificación en vivo de enrolamiento admin via `§5.Z-12` |

---

## 3. Verificación global (números reales observados tras la última edición)

### Backend (`Back_Rodrigo`)

| Comprobación | Resultado observado |
|---|---|
| `dotnet build sensus-back.sln -warnaserror` | **PASS** — `0` advertencias, `0` errores |
| `dotnet format sensus-back.sln --verify-no-changes` | **PASS** — `0` diferencias |
| `dotnet test tests/Sensus.Application.UnitTests --no-build --collect:"XPlat Code Coverage"` | **1224 / 1224 PASS** (`0` error, `0` omitidas) |
| `node .github/scripts/check-coverage.mjs <covDir> 80 Sensus.Domain Sensus.Application` | **PASS** — `Sensus.Domain`: **90.05 %** (`3341/3710`), `Sensus.Application`: **88.64 %** (`8237/9293`) |
| `dotnet test tests/Sensus.ArchitectureTests --no-build` | **6 / 6 PASS** (`0` error, `0` omitidas) |
| `dotnet test tests/Sensus.E2ETests --no-build` | **3 / 3 PASS** (`0` error, `0` omitidas) |
| `dotnet test tests/Sensus.IntegrationTests --no-build --filter "FullyQualifiedName!~Mongo&...&!~RedisCacheTestcontainersTests&!~DynamoDbLocal...&!~DynamoDbBudget..."` | **674 / 674 PASS** (`0` error, `0` omitidas). Las suites Testcontainers quedaron excluidas porque `docker info` devolvió `DOCKER_EC=1` tras el reinicio del equipo. *(Referencia previa al reinicio en Bloque A: `757/757 PASS`; en Bloque B1: `43/43 PASS` con Testcontainers.)* |
| `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror` | **PASS** — `0` advertencias, `0` errores |
| `dotnet test infra/tests/Sensus.Infra.Tests/Sensus.Infra.Tests.csproj` | **2 / 2 PASS** (síntesis CDK + `AwsSolutionsChecks` de `Cdklabs.CdkNag`) |
| `node .github/scripts/check-specs.mjs specs` | **PASS** — `Especificaciones revisadas: 62` · `Estructura SDD correcta.` |
| `node --test .github/scripts/load-smoke.test.mjs .github/scripts/run-ai-evals.test.mjs .github/scripts/post-deploy-smoke.test.mjs` | **26 / 26 PASS** (`0` fallos, `0` omitidas) |
| `node .github/scripts/run-ai-evals.mjs --mode deterministic` | **5 / 5 PASS (100.0 %)** — `QUALITY GATE EVALS: PASSED` |
| `python -B -m unittest discover -s infra/assets/night-switch -p "test_*.py" -v` | **6 / 6 OK** |
| Escaneo `gitleaks` en Docker (`zricethezav/gitleaks:v8.24.3`) | **Bloqueado por entorno** (`DOCKER_EC=1`: Docker Desktop Linux engine detenido tras el reinicio) |

### Móvil (`C:\Users\Rodrigo\Sensus\Sensus`, rama `movil_Rodrigo`, cierre A3)

| Comprobación | Resultado observado |
|---|---|
| `rg "photo-signature\|PhotoSignature\|CloudinarySignedUploader" app\src` | **0 coincidencias** (`EXITCODE=1`) |
| `python scripts/check_secrets.py` / `check_sdd.py` / `check_layers.py` / `check_endpoints.py` | **4 / 4 PASS** (`521` / `43` / `359` / `60`) |
| `./gradlew.bat :app:testDebugUnitTest :app:koverXmlReportDebug :app:koverVerifyDebug --no-daemon` | **BUILD SUCCESSFUL** |
| `detekt` y `ktlintTestSourceSetCheck` | Fallos preexistentes/concurrentes ajenos a A3 en `MyClaimsScreen.kt`, `MyClaimsUiState.kt`, `MyClaimsViewModel.kt`, `MyClaimsViewModelTest.kt` (sin modificar) |

---

## 4. Hallazgos

1. **Docker Desktop detenido tras reinicio del equipo (`DOCKER_EC=1`):** impide ejecutar localmente Testcontainers (`Mongo*`, `StoreReviewPaginationIntegrationTests`, `NotificationMongoRepositoryTests`, `RedisCacheTestcontainersTests`, `DynamoDbLocalBudgetCounterStoreTests`, `DynamoDbBudgetCounterStoreIntegrationTests`) y el contenedor `zricethezav/gitleaks:v8.24.3`.
   - *Afecta:* primera corrida real de la nueva prueba `MongoMediaAssetRepositoryTests.QuarantineLifecycle_GetByIdQueryExpiredAndUpdate_WorksAgainstRealMongoDb` (`specs/019-cuarentena-contenido/checklist.md`) y el escaneo local de secretos previo al commit.
   - *Propuesta:* iniciar Docker Desktop y ejecutar `dotnet test tests/Sensus.IntegrationTests` + `docker run --rm -v "C:/Users/Rodrigo/source/repos/sensus-back:/repo" zricethezav/gitleaks:v8.24.3 dir /repo --redact`.
2. **Retro-specs `056`–`059` y `046` sin filas `| TNNN-xx |` estándar:**
   - *Archivos:* `specs/056-resenas-comercio/{tasks,checklist}.md`, `specs/057-*/{tasks,checklist}.md`, `specs/058-*/{tasks,checklist}.md`, `specs/059-*/{tasks,checklist}.md`, `specs/046-asistente-sensi-ia/tasks.md`.
   - *Propuesta:* opcionalmente normalizar sus tablas de tareas/checklists al formato `_templates/` en un PR de documentación futuro si se desea auditoría numérica uniforme; `check-specs.mjs` ya las aprueba.
3. **Cambios concurrentes de `MyClaims*` y `Favorites*` en móvil (`movil_Rodrigo`):**
   - *Archivos:* `MyClaimsScreen.kt`, `MyClaimsUiState.kt`, `MyClaimsViewModel.kt`, `MyClaimsViewModelTest.kt`.
   - *Propuesta:* resolver esos 3 hallazgos de `detekt` y 1 de `ktlintTestSourceSetCheck` en esa historia antes de abrir PR en móvil.
4. **Brechas externas vigentes en `constitution.md` Apéndice F:** `G-07` (`CertificatePinner`), `G-08` (Play Integrity), `G-09` (`FLAG_SECURE`/biometría) y parte móvil de `G-04` corresponden al repositorio Android; `G-12` (clave Figma en móvil) sigue **Abierta y vencida** desde `2026-11-23`; `G-14` (IAM/RBAC en Valkey Serverless) y la aprobación de costo de WAF (`G-10`) siguen pendientes de decisión del responsable.

---

## 5. Runbooks de §5.Z (para ejecutar en horario diurno `07:00–22:30 America/Mexico_City`)

> Precondición general en esta máquina antes de comandos AWS/GitHub: validar sesión AWS (`aws sts get-caller-identity` o `aws login` si expiró) y operar en **07:00–22:30 (`America/Mexico_City`)**.

### §5.Z-1 — PR `Back_Rodrigo → develop → main` y despliegue en `production`

- **Prerrequisitos:** encender Docker Desktop y correr la verificación local completa; ejecutar `§5.Z-2` (al menos crear el environment `production` con revisor antes del merge a `main`, o mantener temporalmente `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` hasta completar `§5.Z-2`).
- **Comandos / Pasos:**
  ```powershell
  # 1. Confirmar test completo (con Docker Desktop encendido) y gitleaks antes de hacer commit
  dotnet test tests/Sensus.IntegrationTests
  docker run --rm -v "C:/Users/Rodrigo/source/repos/sensus-back:/repo" zricethezav/gitleaks:v8.24.3 dir /repo --redact
  ```
  Luego en Git (autorizado por ti): commit en `Back_Rodrigo` siguiendo Conventional Commits → PR `Back_Rodrigo → develop` (esperar CI `ci.yml` en verde: build, tests, `Sensus.Infra.Tests`, `test_index.py`, `gitleaks`, Snyk, SonarCloud, SBOM) → PR `develop → main` → aprobar el job en GitHub Actions **Run `deploy-serverless.yml`** (environment `production`).
- **Resultado esperado:** CI en verde sin vulnerabilidades High/Critical ni alertas nuevas de `AwsSolutionsChecks`; artefactos `sbom-backend-cyclonedx` y `sbom-deploy-cyclonedx` + atestación SLSA 2 generados; `cdk deploy` y `post-deploy-smoke.mjs` terminan con código `0`.
- **Qué cierra:** verificaciones tras el despliegue en **045** (`T045-18`, `T045-21`), **047** (SBOM/atestación/cdk-nag reales), **048** y **051** (`T051-09` permiso `CreateNatGateway` y `T051-10` alarma `sensus-night-switch-errors-prod`).
- **Texto exacto de evidencia para pegar:**
  `Verificado en CI/CD commit <SHA> sobre develop/main (corrida deploy-serverless <URL/ID>): AwsSolutionsChecks, SBOM CycloneDX, atestación SLSA 2, cdk deploy y post-deploy-smoke.mjs en verde.`

---

### §5.Z-2 — Crear environment `production` y migrar de llaves largas a OIDC

- **Prerrequisitos:** primer despliegue de `§5.Z-1` completado para que existan los roles `sensus-github-deploy-prod` y `sensus-github-iac-readonly-prod`. *(Si ya existe un proveedor OIDC para GitHub en la cuenta AWS, consultar `aws iam list-open-id-connect-providers` y desplegar con `-c existingGithubOidcProviderArn=<ARN>` en CDK.)*
- **Comandos / Pasos:**
  1. En GitHub → **Settings → Environments → New environment**: nombrar `production`, activar **Required reviewers** (añadirte) y limitar rama de despliegue a `main`.
  2. Obtener los ARNs de salida del stack desplegado:
     ```powershell
     aws cloudformation describe-stacks `
       --stack-name SensusBackendStack-prod `
       --region us-east-2 `
       --query "Stacks[0].Outputs[?OutputKey=='GitHubDeployRoleArn'||OutputKey=='GitHubIacReadOnlyRoleArn'].[OutputKey,OutputValue]" `
       --output table
     ```
  3. En GitHub → **Settings → Secrets and variables → Actions → Secrets**, crear `AWS_DEPLOY_ROLE_ARN` y `AWS_IAC_ROLE_ARN` con esos ARNs.
  4. Disparar un `workflow_dispatch` o push verificando en los logs de CI/CD que `Autenticar en AWS con OIDC` se ejecuta y pasa.
  5. Eliminar los secretos legacy `AWS_ACCESS_KEY_ID` y `AWS_SECRET_ACCESS_KEY`.
- **Resultado esperado:** despliegue e IaC read-only usan tokens temporales OIDC de STS (`maxSessionDuration: 1h`); cero llaves IAM estáticas en GitHub Secrets.
- **Qué cierra:** `specs/045-infraestructura-serverless/tasks.md` **`T045-20`** y **`T045-21`**.
- **Texto exacto de evidencia:**
  - `T045-20`: `Completada (environment production creado con revisor requerido para main; verificado en corrida <ID>)`
  - `T045-21`: `Completada (OIDC activo en corrida <ID> con AWS_DEPLOY_ROLE_ARN y AWS_IAC_ROLE_ARN; llaves AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY retiradas)`

---

### §5.Z-3 — Cargar `SesEmailSettings/ConfigurationSetName` en SSM tras desplegar CDK

- **Prerrequisitos:** despliegue de `§5.Z-1` completado (`SensusInfraStack.Email.cs` crea el configuration set `sensus-transactional-prod`).
- **Comandos:**
  ```powershell
  aws ssm put-parameter `
    --region us-east-2 `
    --name "/sensus/prod/SesEmailSettings/ConfigurationSetName" `
    --type "String" `
    --value "sensus-transactional-prod" `
    --overwrite
  ```
  Luego validar que no hay errores `ConfigurationSet` al reiniciar la Lambda o consultar parámetro:
  ```powershell
  aws ssm get-parameter --region us-east-2 --name "/sensus/prod/SesEmailSettings/ConfigurationSetName" --query "Parameter.Value"
  ```
- **Resultado esperado:** devuelve `"sensus-transactional-prod"` y los envíos de SES publican eventos de entrega/rebote/queja al SNS/CloudWatch configurado.
- **Qué cierra:** nota pendiente de `T029-24` en `specs/029-recuperacion-password/tasks.md`.
- **Texto exacto de evidencia:**
  `Hecho (parámetro SSM /sensus/prod/SesEmailSettings/ConfigurationSetName=sensus-transactional-prod creado y verificado el <FECHA>)`

---

### §5.Z-4 — Esperar salida del sandbox SES y probar recuperación de contraseña desde app

- **Prerrequisitos:** caso de soporte AWS SES aprobado (salida de sandbox) y `§5.Z-1` + `§5.Z-3` aplicados.
- **Comandos / Pasos:**
  1. Verificar cuota de producción SES (sandbox levantado):
     ```powershell
     aws sesv2 get-account --region us-east-2 --query "{ProductionAccessEnabled:ProductionAccessEnabled,SendQuota:SendQuota}"
     ```
  2. Desde la app móvil en un dispositivo real (o cuenta de correo externa no verificada en SES), solicitar recuperación de contraseña → recibir el correo TLS vía Amazon SES → capturar token y completar restablecimiento → comprobar que el JWT anterior deja de autenticar (`GET /api/v1/auth/me` → `401`).
- **Resultado esperado:** `ProductionAccessEnabled: true`, correo recibido en destinatario externo, cambio de contraseña `200/204` y sesiones previas revocadas.
- **Qué cierra:** `specs/029-recuperacion-password/tasks.md` **`T029-23`**.
- **Texto exacto de evidencia:**
  `Completada (ProductionAccessEnabled=true verificado en SES us-east-2 el <FECHA>; flujo recover→reset→revocación comprobado en app con correo externo sin errores en CloudWatch)`

---

### §5.Z-5 — Rotar secreto JWT en SSM (cierra sesión a todos)

- **Prerrequisitos:** ventana acordada (invalidará todas las sesiones JWT activas hasta que hagan refresh o login).
- **Comandos:**
  ```powershell
  # Generar secreto aleatorio criptográfico de 64 bytes (88 chars Base64url / Base64 seguro en consola local sin imprimirlo en archivos)
  $bytes = New-Object byte[] 64
  [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  $secret = [Convert]::ToBase64String($bytes)
  aws ssm put-parameter `
    --region us-east-2 `
    --name "/sensus/prod/JwtSettings/Secret" `
    --type "SecureString" `
    --value $secret `
    --overwrite
  Remove-Variable secret, bytes
  # Forzar lectura en frío de SSM en Lambda sensus-api-prod (actualizar descripción o reciclar configuración no invasiva tras deploy)
  aws lambda update-function-configuration --region us-east-2 --function-name sensus-api-prod --description "Sensus API prod (JWT secret rotated <FECHA>)"
  ```
- **Resultado esperado:** `Version` incrementada en SSM (`aws ssm describe-parameters --parameter-filters "Key=Name,Values=/sensus/prod/JwtSettings/Secret"`), tokens firmados con el secreto anterior reciben `401 Unauthorized` y nuevos logins emiten tokens válidos con el nuevo secreto.
- **Qué cierra:** casilla abierta de rotación operativa en `specs/001-registro-usuario/checklist.md` (línea 31) y `specs/002-inicio-sesion/checklist.md` (línea 30).
- **Texto exacto de evidencia:**
  `- [x] Rotación operativa del secreto JWT en SSM tras retiro de appsettings.json (verificado <FECHA>: nueva versión <V> de /sensus/prod/JwtSettings/Secret en SecureString y reciclaje de sensus-api-prod con rechazo 401 de tokens previos)`

---

### §5.Z-6 — Cargar credenciales FCM en SSM y probar notificaciones push en 2 celulares

- **Prerrequisitos:** descarga desde Firebase Console de la llave privada Service Account JSON (proyecto de `sensus.com`) y apps compiladas con su `google-services.json`.
- **Comandos / Pasos:**
  1. Guardar el JSON en archivo temporal local fuera del repo (ej. `$env:TEMP\fcm-sa.json`) y subirlo como `SecureString`:
     ```powershell
     aws ssm put-parameter `
       --region us-east-2 `
       --name "/sensus/prod/push/fcm-service-account" `
       --type "SecureString" `
       --value (Get-Content -Raw "$env:TEMP\fcm-sa.json") `
       --overwrite
     Remove-Item "$env:TEMP\fcm-sa.json" -Force
     ```
  2. Instalar app en **2 celulares físicos** (Comprador y Comerciante), iniciar sesión para registrar `PUT /api/v1/users/device-token`.
  3. Crear apartado (`POST /api/v1/offers/{id}/claim`): verificar respuesta `200` con código QR y recepción de push en ambos celulares en 3 estados: **primer plano**, **segundo plano** y **app cerrada**.
  4. Desactivar push en ajustes (`PUT /api/v1/users/settings`) para un destinatario, repetir claim y comprobar **0 avisos** recibidos para esa cuenta.
- **Resultado esperado:** entrega push real verificada en primer plano, segundo plano y cerrada, apertura de pantalla correspondiente en Android y respeto de preferencias por usuario.
- **Qué cierra:**
  - `specs/027-registro-dispositivo/tasks.md` **`T027-07`**, **`T027-12`**, **`T027-13`** + casilla línea 22 de `checklist.md`
  - `specs/033-apartado-con-qr/checklist.md` líneas 72, 73, 74 (y porción FCM de línea 76 junto con `§5.Z-10`)
  - Nota física de `specs/044-ajustes-notificaciones/tasks.md` **`T044-13`**.
- **Texto exacto de evidencia:**
  `Completada (<FECHA>: /sensus/prod/push/fcm-service-account cargado como SecureString en SSM; prueba en vivo con 2 Android físicos verificando entrega en primer plano, segundo plano y app cerrada al ejecutar POST /api/v1/offers/{id}/claim, apertura de pantalla y 0 avisos con push deshabilitado; sin tokens/QR en logs)`

---

### §5.Z-7 — Invocar pausa nocturna (`stop`/`start`) a mano de día y comprobar alarma

- **Prerrequisitos:** `§5.Z-1` desplegado en AWS; ventana de prueba con pocos minutos sin usuarios reales de app.
- **Comandos:**
  ```powershell
  # 1. Ejecutar STOP manual y comprobar concurrencia 0 en sensus-api-prod
  aws lambda invoke --region us-east-2 --function-name sensus-night-switch-prod --cli-binary-format raw-in-base64-out --payload '{"action":"stop"}' "$env:TEMP\ns-stop.json"
  Get-Content "$env:TEMP\ns-stop.json"
  aws lambda get-function-concurrency --region us-east-2 --function-name sensus-api-prod

  # 2. Probar que una ruta API responde 503/throttled y la alarma sensus-night-switch-errors-prod permanece intacta (sin silenciar ni error)
  aws cloudwatch describe-alarms --region us-east-2 --alarm-names "sensus-night-switch-errors-prod" --query "MetricAlarms[0].{State:StateValue,ActionsEnabled:ActionsEnabled}"

  # 3. Restaurar inmediatamente con START manual y comprobar /health/ready 200
  aws lambda invoke --region us-east-2 --function-name sensus-night-switch-prod --cli-binary-format raw-in-base64-out --payload '{"action":"start"}' "$env:TEMP\ns-start.json"
  Get-Content "$env:TEMP\ns-start.json"
  node .github/scripts/post-deploy-smoke.mjs --base-url https://zqqdwovx81.execute-api.us-east-2.amazonaws.com
  Remove-Item "$env:TEMP\ns-stop.json","$env:TEMP\ns-start.json" -Force
  ```
- **Resultado esperado:** tras `stop`, `ReservedConcurrentExecutions` = `0` y NAT Gateway eliminado; alarma `sensus-night-switch-errors-prod` con `ActionsEnabled: true` e estado `OK`; tras `start`, concurrencia restaurada al final y `post-deploy-smoke.mjs` devuelve `0` (`CA-051-01`, `CA-051-02`, `CA-051-05`).
- **Qué cierra:** `specs/051-pausa-nocturna-infraestructura/tasks.md` **`T051-08`** y casilla línea 20 de `checklist.md`.
- **Texto exacto de evidencia:**
  `Completada (<FECHA>: invocación manual stop→start sobre sensus-night-switch-prod en vivo; CA-051-01 concurrencia 0 / NAT eliminado y CA-051-02 restauración + post-deploy-smoke.mjs exit 0; alarma sensus-night-switch-errors-prod verificada activa sin silenciar)`

---

### §5.Z-8 — Turno real de Sensi IA y verificación del contador en Amazon DynamoDB

- **Prerrequisitos:** `§5.Z-1` desplegado y ventana diurna (`07:00–22:30`), un JWT de usuario válido en `$env:SENSUS_TOKEN`.
- **Comandos:**
  ```powershell
  # 1. Consultar nombre de tabla DynamoDB desde el entorno de la Lambda
  $tbl = aws lambda get-function-configuration --region us-east-2 --function-name sensus-api-prod --query "Environment.Variables.AiBudgetStore__TableName" --output text

  # 2. Enviar 1 turno real a POST /api/v1/ai/chat (reemplazar body según contrato) y medir tiempo
  $headers = @{ Authorization = "Bearer $env:SENSUS_TOKEN"; "Content-Type" = "application/json" }
  $t = Measure-Command { $resp = Invoke-RestMethod -Method Post -Uri "https://zqqdwovx81.execute-api.us-east-2.amazonaws.com/api/v1/ai/chat" -Headers $headers -Body '{"message":"Hola Sensi, recomendaciones cerca"}' }
  Write-Output "Latencia ms: $($t.TotalMilliseconds)"

  # 3. Inspeccionar ítems/conteo en la tabla DynamoDB (sin PII)
  aws dynamodb scan --region us-east-2 --table-name $tbl --select COUNT
  ```
- **Resultado esperado:** HTTP `200`, latencia dentro de NFR y contador mensual/diario en DynamoDB incrementado con atributo TTL `expiresAt` presente (`Scan Count >= 1`).
- **Qué cierra:** porción en vivo de **`T049-10`** en `specs/049-contadores-presupuesto-dynamodb/tasks.md` y casilla línea 30 de `checklist.md`.
- **Texto exacto de evidencia:**
  `- [x] Turno de Sensi en el entorno desplegado con incremento real observado en DynamoDB y p95 desde Lambda (<FECHA>: incremento atómico verificado en tabla <TABLA>, ttl expiresAt activo, latencia <X> ms)`

---

### §5.Z-9 — Validar amigos en vivo después del despliegue

- **Prerrequisitos:** `§5.Z-1` desplegado, dos cuentas de prueba con JWTs `$env:TOKEN_A` y `$env:TOKEN_B`.
- **Comandos / Pasos (PowerShell contra `https://zqqdwovx81.execute-api.us-east-2.amazonaws.com`):**
  1. Con `TOKEN_A`: `POST /api/v1/friends/requests` hacia el `userId` de B → `201`.
  2. Con `TOKEN_B`: `GET /api/v1/friends/requests/incoming` → contiene solicitud; `POST /api/v1/friends/requests/{requestId}/accept` → `200/204`.
  3. Ambos consultan `GET /api/v1/friends` → listado mutuo paginado `200`; probar eliminación `DELETE /api/v1/friends/{friendUserId}` → `204`.
- **Resultado esperado:** ciclo completo solicitud → aceptación → consulta paginada → eliminación exitoso contra MongoDB real y CloudWatch sin errores `5xx`.
- **Qué cierra:** `specs/050-amigos-y-solicitudes/tasks.md` **`T050-07`** y casilla línea 23 de `checklist.md`.
- **Texto exacto de evidencia:**
  `Completada (<FECHA>: ciclo envío/aceptación/listado/eliminación de amistad ejecutado en vivo contra producción + MongoDB Atlas con HTTP 201/200/204 sin errores 5xx)`

---

### §5.Z-10 — Correr `dast-zap.yml` de día y `load-smoke.mjs` (5 rps, sin `/ai`)

- **Prerrequisitos:** `§5.Z-1` desplegado y horario diurno `07:00–22:30` (`America/Mexico_City`), más un JWT válido para rutas protegidas en `$env:LOAD_TEST_TOKEN`.
- **Comandos / Pasos:**
  1. En GitHub Actions → **Workflows → `dast-zap.yml` → Run workflow** (`target_url`: `https://zqqdwovx81.execute-api.us-east-2.amazonaws.com`) y esperar resultado verde con artefacto `zap-api-baseline-report`.
  2. En local (con `$env:LOAD_TEST_TOKEN` asignado por env, nunca impreso en repo):
     ```powershell
     node .github/scripts/load-smoke.mjs `
       --base-url https://zqqdwovx81.execute-api.us-east-2.amazonaws.com `
       --rps 5 `
       --duration-sec 120 `
       --max-p95-ms 700
     ```
- **Resultado esperado:**
  - `dast-zap.yml` concluye con `0` hallazgos bloqueantes en `.zap/rules.tsv` (`G-02` verificada en vivo).
  - `load-smoke.mjs` mantiene `rps <= 5`, nunca toca `/api/v1/ai/*`, tasa de error `<= umbral` y `p95 < 700 ms` (`NFR-033-01` / `NFR-042-01` / `G-13`).
- **Qué cierra:** casilla `NFR-042-01/02` en `specs/042-recomendaciones/checklist.md` (línea 28), porción de carga de `specs/033-apartado-con-qr/checklist.md` (línea 76, junto con `§5.Z-6`), y cierra en vivo `G-02` / `G-13` en `constitution.md` Apéndice F.
- **Texto exacto de evidencia:**
  `Verificado el <FECHA> en horario diurno: workflow dast-zap.yml corrida <ID> en verde (artefacto zap-api-baseline-report); load-smoke.mjs (5 rps, 120 s, sin /api/v1/ai/*) p50=<X> ms, p95=<Y> ms (< 700 ms), p99=<Z> ms, 0 % error.`

---

### §5.Z-11 — Ejecutar `cdk diff` con credenciales AWS activas para 049 y 055

- **Prerrequisitos:** `aws login` / credenciales válidas y `ALERTS_EMAIL` disponible en variable temporal de sesión.
- **Comandos:**
  ```powershell
  cd infra
  cdk diff --context environment=prod --context budgetEmail=$env:ALERTS_EMAIL
  cd ..
  ```
- **Resultado esperado:** muestra únicamente los diffs esperados (p. ej. `DefaultRouteSettings` de A5, roles OIDC o recursos pendientes de `§5.Z-1`), sin cambios destructivos no deseados sobre MongoDB/SSM/DynamoDB.
- **Qué cierra:** casilla `cdk diff` en `specs/049-contadores-presupuesto-dynamodb/checklist.md` (línea 28, junto con `§5.Z-8` para `T049-10`) y `specs/055-interacciones-ofertas/checklist.md` (línea 25).
- **Texto exacto de evidencia:**
  `- [x] cdk synth y cdk diff revisados (verificado el <FECHA> contra stack SensusBackendStack-prod en us-east-2 sin reemplazos destructivos inesperados)`

---

### §5.Z-12 — Dar de alta el TOTP de administrador en vivo (Spec 062)

- **Prerrequisitos:** `§5.Z-1` desplegado y parámetro SSM `/sensus/prod/Mfa/EncryptionKey` creado con clave AES-256 en Base64 (32 bytes = 44 chars Base64):
  ```powershell
  $k = New-Object byte[] 32
  [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($k)
  $b64 = [Convert]::ToBase64String($k)
  aws ssm put-parameter --region us-east-2 --name "/sensus/prod/Mfa/EncryptionKey" --type "SecureString" --value $b64 --overwrite
  Remove-Variable k, b64
  ```
- **Comandos / Pasos (sin guardar secreto ni recovery codes en archivos ni chat):**
  1. Iniciar sesión como cuenta `Administrador` (`POST /api/v1/auth/login`) → obtener `accessToken`.
  2. Llamar `POST /api/v1/auth/mfa/enroll` con ese `accessToken` → registrar la `otpauthUri` en tu app autenticadora (Google Authenticator / 1Password / Aegis) y guardar los 8 `recoveryCodes` en gestor de contraseñas cifrado.
  3. Verificar activación con `POST /api/v1/auth/mfa/verify` enviando el primer código de 6 dígitos.
  4. Comprobar *step-up*: `POST /api/v1/auth/login` ahora devuelve `{ requiresMfa: true, mfaChallengeToken: ... }`; ese challenge token da `403` en rutas `AdminOnly`, y tras `POST /api/v1/auth/mfa/verify` el nuevo JWT sí autoriza rutas `AdminOnly`.
- **Resultado esperado:** TOTP activo en producción para la cuenta administradora y política `AdminOnlyPolicy` exigiendo `amr=mfa` + `mfa_at <= Mfa:StepUpWindowMinutes`.
- **Qué cierra:** verificación operativa en vivo de **Spec 062** (ya 100 % verificada en código/pruebas) y verificación operativa restante de **`G-05`** en `constitution.md` Apéndice F.
- **Texto exacto de evidencia (para nota en `specs/062-segundo-factor-administrador/checklist.md` y `G-05`):**
  `Verificado en vivo el <FECHA>: /sensus/prod/Mfa/EncryptionKey provisionado en SSM SecureString; enrolamiento POST /api/v1/auth/mfa/enroll y desafío login→/mfa/verify→AdminOnly comprobados en producción con la cuenta Administrador.`

---

### §5.Z-13 — Actualizar aviso de privacidad en la landing y documento FT-23 móvil

- **Prerrequisitos:** acceso al repositorio de la landing web (`sensus-landing` o equivalente) y al repositorio móvil (`C:\Users\Rodrigo\Sensus\Sensus` / entregables FT-23).
- **Pasos:**
  1. En el Aviso de Privacidad publicado en la landing y en el documento **FT-23**, añadir cláusulas explícitas de tratamiento de:
     - **Historial de interacciones con ofertas** (vistas, clics, guardados y compartidos, Spec 055) con seudonimización/HMAC y expiración automática.
     - **Personalización del feed de recomendaciones** basada en dichas señales y distancia geolocalizada aproximada (Spec 042).
  2. Publicar la actualización del aviso en la landing y actualizar versión/fecha en FT-23.
- **Resultado esperado:** URL pública del aviso vigente y sección en FT-23 reflejan ambas finalidades antes de liberar la funcionalidad a usuarios finales.
- **Qué cierra:** `specs/042-recomendaciones/checklist.md` línea 12 y `specs/055-interacciones-ofertas/checklist.md` línea 11.
- **Texto exacto de evidencia:**
  `- [x] Aviso de privacidad actualizado con la personalización del feed / el registro de interacciones (actualizado en <URL_AVISO_PRIVACIDAD> versión <V> y FT-23 el <FECHA>)`

---

## 6. Decisiones o riesgos que requieren respuesta del usuario

1. **Docker Desktop detenido (`DOCKER_EC=1`):** tras el reinicio del equipo durante la sesión, el motor Linux de Docker quedó apagado y no se reinició a la fuerza por regla. ¿Enciendes Docker Desktop ahora para correr `dotnet test tests/Sensus.IntegrationTests` completo (incluyendo `QuarantineLifecycle_GetByIdQueryExpiredAndUpdate_WorksAgainstRealMongoDb` de la `019`) y `gitleaks` antes de autorizar el commit?
2. **Cambios concurrentes en móvil (`movil_Rodrigo`):** hay archivos sin commit de otra historia (`MyClaimsScreen.kt`, `MyClaimsUiState.kt`, `MyClaimsViewModel.kt`, `MyClaimsViewModelTest.kt`, además de `Favorites*`) que contienen 3 hallazgos de `detekt` y 1 de `ktlintTestSourceSetCheck`. ¿Confirmas que los dejamos sin tocar en esta rama mientras revisas tus cambios de A3?
3. **Brecha `G-12` vencida (`2026-11-23`) y brechas de móvil (`G-04`, `G-07`, `G-08`, `G-09`):** `G-12` (clave Figma en `mcp_config.json` del móvil) figura como **Abierta y vencida** en `constitution.md` Apéndice F. ¿Ya rotaste esa clave o prefieres actualizar su remediación y fecha objetivo junto con `G-07..09` antes del PR a `main`?
4. **Decisión de costo AWS (`G-10` WAF y `G-14` Valkey IAM):** `G-10` quedó mitigada sin costo en código con `DefaultRouteSettings` (50 req/s, burst 100), y `G-14` requiere habilitar IAM Auth en ElastiCache Serverless Valkey. ¿Se mantiene `G-10` con throttling de API Gateway sin contratar AWS WAF (por presupuesto) y en qué sprint agendas `G-14`?
5. **Autorización de Git y orden de ejecución diurna:** ningún repo tiene `commit` ni `push`. ¿Autorizas preparar los commits en `Back_Rodrigo` y `movil_Rodrigo` una vez que revises el diff y pases Docker + `gitleaks`?
