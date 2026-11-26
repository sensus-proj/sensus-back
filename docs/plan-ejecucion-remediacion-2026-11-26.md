# Plan de ejecución — Remediación DevSecOps y pendientes del backend Sensus

| Campo | Valor |
|---|---|
| Fecha | 2026-11-26 |
| Autor del plan | Claude (sesión con Rodrigo Sánchez, responsable del proyecto) |
| Destinatario | Modelo de IA que ejecutará el trabajo |
| Repositorio backend | `C:\Users\Rodrigo\source\repos\sensus-back` (rama `Back_Rodrigo`) |
| Repositorio móvil | `C:\Users\Rodrigo\Sensus\Sensus` (usar rama `movil_Rodrigo`) |
| Normativa | `constitution.md` v3.0.0, `AGENTS.md`, `memory.md` |
| Estimación total | ~20 h de trabajo efectivo, en 4 bloques |

---

## 0. Cómo usar este documento

1. Lee completo este archivo antes de tocar nada. Después lee `AGENTS.md`, `constitution.md` (en especial Principio VIII y Apéndices E y F) y la sección A y F de `memory.md`.
2. Ejecuta los puntos **en el orden de la sección 5**. Cada punto es independiente y tiene: objetivo, requisito a crear, archivos, pasos, pruebas, criterio de terminado y verificación.
3. No avances al siguiente punto hasta que el actual cumpla su **criterio de terminado**. Si no puedes cumplirlo, detente, deja el punto documentado como `Parcial` en su `tasks.md` y `checklist.md` con la razón exacta, y sigue con el siguiente.
4. Al terminar cada **bloque**, ejecuta la verificación global de la sección 6 y reporta al usuario con el formato de la sección 7.
5. Si encuentras algo que contradice este plan o la constitución, **detente y pregunta** al usuario. No inventes alcance.

---

## 1. Reglas no negociables

### 1.1 Git

- **Backend:** PROHIBIDO `git commit`, `git push`, `git merge`, `git rebase`, crear o borrar ramas, `git stash`, `git reset`. Todos los cambios quedan **sin commit** en el working tree de `Back_Rodrigo`. El usuario lo pidió explícitamente.
- **Móvil:** el usuario autorizó solo esto, en este orden, una vez:
  1. `git -C C:\Users\Rodrigo\Sensus\Sensus fetch origin`
  2. `git -C C:\Users\Rodrigo\Sensus\Sensus switch movil_Rodrigo` (existe en `origin`; si no existe local, `git switch -c movil_Rodrigo --track origin/movil_Rodrigo`)
  3. `git -C C:\Users\Rodrigo\Sensus\Sensus pull origin develop` ("baja cambios desde develop"). Si hay conflictos, **detente y pregunta**; no los resuelvas por tu cuenta.
  4. Después, cambios sin commit y sin push.
  - Antes del paso 2 revisa `git status`: si la rama actual (`movil_Rocio`) tiene cambios sin commit, **detente y pregunta**. Hoy solo tiene la carpeta sin seguimiento `Claude outputs/`, que no estorba al cambiar de rama.
- Nunca uses `--force`, `--no-verify` ni reescribas historia.

### 1.2 AWS y GitHub

- Nunca ejecutes `cdk deploy`, `cdk destroy`, ni crees, modifiques o borres recursos de AWS por CLI o consola. **Todo recurso nuevo va en `infra/` con CDK** y se despliega solo por CI cuando el usuario haga los PR.
- Lectura de AWS (`describe`, `get`, `list`, `logs filter-log-events`) está permitida. Las credenciales locales salen de `aws login`, que **solo hace el usuario**; si expiran (`Your session has expired`), pídele que corra `aws login`.
- No existe `gh` CLI. No intentes configurar GitHub (environments, secretos, protección de ramas): eso lo hace el usuario con instrucciones que tú le escribes.
- Nunca escribas secretos, llaves, correos personales, IDs de cuenta ni tokens en el repositorio, en logs, en resúmenes de CI ni en tus mensajes.

### 1.3 Flujo SDD obligatorio por punto (AGENTS.md §3)

Para **cada** punto que cambie código o configuración:

1. `spec.md` de la spec dueña: agrega el requisito (`FR-<spec>-<nn>` o `NFR-<spec>-<nn>`) **y** su criterio de aceptación `CA-<spec>-<nn>`. Sube la versión de la spec (MINOR) y agrega fila al registro de evolución si la spec lo tiene.
2. `plan.md`: diseño, motivo, costo (0 USD salvo que se indique) y, si toca identidad, datos, códigos canjeables o acciones de administrador, actualiza o agrega la sección **STRIDE**.
3. `tasks.md`: agrega tareas `T<spec>-<nn>` con capa, requisito y dependencia; estado `Pendiente` y luego `Hecho`/`Completada` con evidencia breve.
4. Implementa el código.
5. Agrega pruebas (ver §1.5). Corre las pruebas afectadas hasta verde.
6. `checklist.md`: agrega fila en el registro de evidencia con fecha `2026-11-26` (o la real), responsable, resultado y evidencia concreta (nombres de pruebas, conteos, comandos).
7. `specs/requirements.md`: agrega la fila del requisito con formato `| \`FR-NNN-nn\` | NNN | FR | <resumen> | Must | Implementado |`, inmediatamente después del último requisito de esa spec.
8. Si el punto cierra una brecha del Apéndice F de `constitution.md` (G-xx), cambia su estado a `Cerrada en código el <fecha> (Spec NNN, FR-NNN-nn; pendiente verificar en producción)` o `Cerrada el <fecha>` si ya lo verificaste en producción.
9. Corre `node .github/scripts/check-specs.mjs specs` → debe decir `Estructura SDD correcta.`

**Trampas conocidas de las specs:**

- `check-specs.mjs` **rechaza** IDs de requisito de otra spec dentro de `spec.md` (por ejemplo, escribir `FR-047-17` dentro de la spec 046 falla). En `spec.md` refiérete a otras specs como "Spec 047" sin el ID. En `plan.md`, `tasks.md` y `checklist.md` sí puedes citar IDs ajenos.
- Muchos archivos usan saltos de línea **CRLF**. Al editar con scripts, conserva el salto de línea original (detecta `\r\n`). Con `sed` no ancles patrones con `$` en archivos CRLF.
- Los números siguientes libres (verificados el 2026-11-26) están en la tabla 1.4. Vuelve a verificarlos antes de usarlos.

### 1.4 Siguientes IDs libres por spec (verificar antes de usar)

| Spec | Último FR | Último NFR | Última tarea |
|---|---|---|---|
| 002 inicio de sesión | FR-002-07 | NFR-002-02 | T002-09 |
| 004 cierre de sesión | FR-004-04 | NFR-004-02 | T004-10 |
| 014 logs estructurados | FR-014-04 | NFR-014-02 | T014-07 |
| 017 moderación multimodal | FR-017-08 | NFR-017-02 | T017-14 |
| 021 auditoría de moderación | FR-021-04 | NFR-021-02 | T021-10 |
| 024 publicaciones del feed | FR-024-07 | NFR-024-02 | T024-09 |
| 027 registro de dispositivo | FR-027-07 | NFR-027-03 | T027-16 |
| 029 recuperación de contraseña | FR-029-13 | NFR-029-03 | T029-23 |
| 033 apartado con QR | FR-033-12 | NFR-033-03 | T033-17 |
| 034 creación de oferta | FR-034-09 | NFR-034-02 | T034-17 |
| 039 me gusta | FR-039-06 | NFR-039-02 | T039-11 |
| 045 infraestructura | FR-045-18 | NFR-045-05 | T045-19 |
| 046 Sensi IA | FR-046-19 | NFR-046-08 | T046-24 |
| 047 CI/CD | FR-047-18 | NFR-047-06 | T047-20 |
| 049 contadores DynamoDB | FR-049-07 | NFR-049-04 | T049-11 |
| 050 amigos | FR-050-07 | NFR-050-03 | T050-07 |
| Nueva | **062** (siguiente número libre; la última es 061) | | |

Nota: `FR-034-05` (límite de 20 activas) y `FR-034-07` (202) **ya existen** en la spec 034; no los dupliques, impleméntalos.

### 1.5 Pruebas obligatorias (AGENTS.md §11)

| Cambio | Prueba |
|---|---|
| Regla de dominio | Unitaria en `tests/Sensus.Application.UnitTests` |
| Endpoint nuevo o modificado | Integración en `tests/Sensus.IntegrationTests`: éxito, entrada inválida, sin autenticación (401), sin permisos (403) |
| Persistencia MongoDB | Integración con Testcontainers contra MongoDB real |
| Integración externa (OpenAI, SES, FCM, Cloudinary) | Doble que simule fallo, timeout y respuesta malformada |
| Corrección de defecto | Prueba de regresión que falle antes del arreglo |
| Scripts de CI (`.github/scripts/*.mjs`) | `node --test` con servidor simulado; agrégalo al paso de `ci.yml` que ya corre `node --test .github/scripts/run-ai-evals.test.mjs .github/scripts/post-deploy-smoke.test.mjs` |

Prohibido bajar umbrales, marcar pruebas como omitidas o desactivarlas.

### 1.6 Comandos (desde la raíz del backend)

```powershell
dotnet build sensus-back.sln -warnaserror
dotnet test tests/Sensus.Application.UnitTests
dotnet test tests/Sensus.ArchitectureTests
dotnet test tests/Sensus.IntegrationTests --filter "FullyQualifiedName~<Clase>"
dotnet test tests/Sensus.E2ETests
dotnet format sensus-back.sln --verify-no-changes
node .github/scripts/check-specs.mjs specs
node --test .github/scripts/run-ai-evals.test.mjs .github/scripts/post-deploy-smoke.test.mjs
dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror
```

- `dotnet build` de la solución tarda ~2 min; las pruebas de integración completas, más. Córrelas en segundo plano si tu entorno lo permite.
- No hay CDK CLI instalado localmente; `cdk synth` lo valida el CI. Si necesitas sintetizar, pregunta al usuario antes de instalar nada.
- Móvil: `C:\Users\Rodrigo\Sensus\Sensus\gradlew.bat assembleDebug`, `testDebugUnitTest`, `connectedDebugAndroidTest`. JDK 17 y Android SDK en `C:\Users\Rodrigo\AppData\Local\Android\Sdk`. `adb` en `...\Sdk\platform-tools\adb.exe`.

---

## 2. Datos del entorno

| Dato | Valor |
|---|---|
| Nube | AWS, región `us-east-2` |
| API producción | `https://zqqdwovx81.execute-api.us-east-2.amazonaws.com` |
| Stack CDK | `SensusBackendStack-prod` (salida `ApiGatewayUrl`) |
| Lambda API | `sensus-api-prod` (512 MB, 29 s, `PROVIDED_AL2023`, `ARM_64`, en VPC) |
| Rol de la Lambda | `sensus-api-lambda-role-prod` |
| Lambda de pausa | `sensus-night-switch-prod` |
| Pausa nocturna | 22:30–07:00 `America/Mexico_City`: la Lambda API queda con concurrencia reservada `0` y responde `503`. **No hagas pruebas en vivo en ese horario.** |
| Secretos | SSM bajo `/sensus/prod/` (`JwtSettings/Secret`, `MongoDbSettings/ConnectionString`, `Cloudinary/*`, `OpenAiSettings/ApiKey`, `ClaimCodeSettings/Secret`, `Interactions/HmacKeysJson`, `push/fcm-service-account`, `SesEmailSettings/From`, `SesEmailSettings/Region`). Cada parámetro `/sensus/prod/Seccion/Clave` se lee como `Seccion:Clave`. |
| Usuario de CI | `github-cdk-deployer` (llave de larga duración, `AdministratorAccess`) — se reemplaza en el punto 10 |
| Bucket assets CDK | `cdk-hnb659fds-assets-<cuenta>-us-east-2` (no escribas el número de cuenta en el repo) |
| SES | Identidad verificada: el Gmail del usuario. Cuenta en sandbox, revisión `PENDING`. |
| Workflows | `.github/workflows/ci.yml`, `deploy-serverless.yml` (CD a Lambda), `ai-evals.yml`, `deploy.yml` (EC2 heredado, manual) |
| SHAs aprobados de acciones | `actions/checkout@11d5960a326750d5838078e36cf38b85af677262` (v4), `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4), `actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020` (v4), `actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02` (v4), `gitleaks/gitleaks-action@ff98106e4c7b2bc287b24eaf42907196329070c7` (v2). Cualquier acción nueva se fija por SHA completo obtenido con `git ls-remote https://github.com/<org>/<repo> refs/tags/<tag>`. |
| Celulares conectados por USB | `R5CW90LY07N` (Galaxy S23 Ultra), `uwmnofhub6ucq49d` (Xiaomi 2510DRA23L; se desconecta a ratos). Paquete Android: `sensus.com`. |
| Docker Desktop | Instalado en `C:\Program Files\Docker\Docker\Docker Desktop.exe`; a veces no levanta el pipe `dockerDesktopLinuxEngine`. Si no responde `docker info`, pide al usuario que lo abra y espere. |

---

## 3. Decisiones ya tomadas por el usuario (no las vuelvas a preguntar)

1. **Swagger** solo en Development. *(Ya implementado, punto 6.)*
2. **`imageUrl` en JSON** de `POST /api/v1/feed/posts`: se **rechaza** cualquier URL externa. Solo se aceptan imágenes subidas por `multipart/form-data` (moderadas en la misma solicitud) o, si el cliente ya subió la imagen por `/api/v1/media/upload`, una referencia a un `MediaAsset` **aprobado del mismo usuario**.
3. **2FA del Administrador**: TOTP (RFC 6238, app autenticadora), con *step-up* en acciones privilegiadas.
4. **Oferta sin veredicto inmediato**: queda privada (no publicada) y la API responde **202**.
5. **Refresh token**: rotatorio, vigencia **30 días**, con **detección de reúso** (si se usa un refresh ya rotado, se revoca toda la familia y se rota el `SecurityStamp`). Token de acceso de **15 min**.
6. **Pruebas en vivo** (ZAP, carga, push real): contra **producción**, solo de día; ZAP **solo baseline pasivo**; carga **baja y acotada** y **sin endpoints de Sensi IA** (no gastar OpenAI); push solo a los 2 celulares del usuario.
7. **Sin commits ni push** en ningún repositorio.

---

## 4. Estado actual (al entregar este plan)

Cambios **sin commit** en `Back_Rodrigo` (base `30240b9`, que aún no está en `develop`):

| Punto | Estado | Archivos |
|---|---|---|
| 4. Acciones de despliegue por SHA | **Hecho y verificado** (ningún `uses:` por etiqueta) | `.github/workflows/deploy-serverless.yml`, `.github/workflows/deploy.yml`, spec 045 (`FR-045-18`, `T045-19`, checklist), `specs/requirements.md` |
| 6. Swagger privado (G-15) + HSTS en Lambda | **Hecho y verificado** (`CorsAndHttpsSecurityTests` + `OpenApiContractTests` 12/12) | `src/Sensus.Api/Extensions/HostingSecurityPolicy.cs` (nuevo), `src/Sensus.Api/Program.cs`, `tests/Sensus.IntegrationTests/Security/CorsAndHttpsSecurityTests.cs`, spec 011 (`FR-011-05`, `T011-08`, checklist), `specs/requirements.md`, `constitution.md` (G-15) |
| Este plan | Nuevo | `docs/plan-ejecucion-remediacion-2026-11-26.md` |

Antes de empezar, corre `git status` y `git diff --stat` para confirmar que ves exactamente esos cambios. No los reviertas.

---

## 5. Puntos a ejecutar (en este orden)

> Los puntos 1, 2, 3 y 8 de la tabla original dependen del usuario; sus instrucciones están en la sección 5.Z.

---

### BLOQUE A — Código corto (~3 h)

#### A1. Punto 7 — H4: moderación *fail-open* sin servicio registrado

- **Problema:** `src/Sensus.Application/Common/Moderation/ContentModerationHelper.cs`, método `EvaluateAsync`, líneas ~65-68: si `moderationService is null` devuelve `Approved(ApprovedDecision, "Sin servicio de moderación configurado.")`. Viola "NUNCA moderación fail-open" (AGENTS.md §12).
- **Requisito:** `FR-017-09` en `specs/017-moderacion-multimodal/spec.md`: "Si no hay servicio de moderación de texto disponible, la evaluación devuelve cuarentena/rechazo (fail-closed) y el contenido no se publica". CA: dado servicio nulo, cuando se evalúa texto no vacío, entonces `IsApproved == false` y decisión `Quarantine`.
- **Pasos:**
  1. Lee completo `ContentModerationHelper.cs` y busca todos sus llamadores: `rg "ContentModerationHelper\.(EvaluateAsync|EnsureApprovedAsync)" src`.
  2. Cambia la rama `moderationService is null` para devolver una evaluación **no aprobada** con `Decision = QuarantineDecision` (constante ya existe), `Verdict` mapeable a `ModerationVerdict.Quarantined`, mensaje "La moderación no está disponible; el contenido no fue publicado." y un `Error.Failure("<errorCode>.ModerationUnavailable", ...)`. Revisa cómo `Rejected(...)` construye `RejectionError` y respeta ese patrón.
  3. Revisa `EnsureApprovedAsync` (mismo archivo) para que también falle cerrado con servicio nulo.
  4. **No cambies** el comportamiento de texto vacío (sigue aprobado) ni el `ProfanityFilter`.
  5. Revisa cada llamador: si alguno dependía de aprobar sin servicio (por ejemplo, pruebas que construyen handlers con `textModerationService: null`), ajusta esas **pruebas** para inyectar un doble aprobador explícito; no reviertas el fail-closed.
- **Pruebas:** en `tests/Sensus.Application.UnitTests` (busca pruebas existentes de `ContentModerationHelper`; si no hay, crea `Common/Moderation/ContentModerationHelperTests.cs`):
  - Servicio nulo + texto no vacío → `IsApproved == false`, decisión `Quarantine`, `RejectionError` no nulo. **Debe fallar antes del arreglo.**
  - Servicio nulo + texto vacío → aprobado (sin cambio).
  - Servicio que lanza excepción y servicio que devuelve `null` → no aprobado.
  - Corre toda la suite unitaria: `dotnet test tests/Sensus.Application.UnitTests` → 0 fallos.
- **Terminado cuando:** pruebas en verde, build `-warnaserror` limpio, spec 017 documentada (FR, CA, `T017-15`, checklist), `requirements.md` actualizado.

#### A2. Punto 15 — H1: redactar en logs las solicitudes con contenido

- **Problema:** `LoggingBehavior` y `PerformanceBehavior` (`src/Sensus.Application/Common/Behaviors/`) registran `{@Request}` completo salvo que el request implemente `ISensitiveRequest` (`src/Sensus.Application/Common/Interfaces/ISensitiveRequest.cs`). Comandos con texto libre, imágenes o prompts **no** lo implementan.
- **Requisito:** `FR-014-05` en `specs/014-logs-estructurados-correlacion/spec.md`: "Toda solicitud MediatR que transporte contenido de usuario (texto libre, imágenes, prompts, datos personales) implementa `ISensitiveRequest` y se registra sin su carga útil". CA con lista de comandos.
- **Pasos:**
  1. Agrega `ISensitiveRequest` a estos records (verificados el 2026-11-26):
     - `src/Sensus.Application/Ai/Commands/SendMessage/SendAiMessageCommand.cs`
     - `src/Sensus.Application/Comercios/Commands/CreateStoreReview/CreateStoreReviewCommand.cs`
     - `src/Sensus.Application/Comercios/Commands/UpdateMerchantProfile/UpdateMerchantProfileCommand.cs`
     - `src/Sensus.Application/Feed/Commands/CreateFeedComment/CreateFeedCommentCommand.cs`
     - `src/Sensus.Application/Feed/Commands/CreateFeedPost/CreateFeedPostCommand.cs`
     - `src/Sensus.Application/Media/Commands/UploadMedia/UploadMediaCommand.cs`
     - `src/Sensus.Application/Offers/Commands/CreateOffer/CreateOfferCommand.cs`
     - `src/Sensus.Application/Offers/Commands/UpdateOffer/UpdateOfferCommand.cs`
     - `src/Sensus.Application/Reports/Commands/CreateContentReport/CreateContentReportCommand.cs`
     - `src/Sensus.Application/Users/Commands/UpdateProfile/UpdateProfileCommand.cs`
  2. Busca además respuestas a reseñas y cualquier otro comando con texto libre o bytes: `rg "record \w+(Command|Query)\(" src/Sensus.Application` y revisa los que tengan `string Content`, `Text`, `Comment`, `Message`, `Prompt`, `byte[]`, `Description`, `Title`, correo, teléfono o dirección. Agrégalos también y lístalos en la spec.
  3. En `PerformanceBehavior`, confirma que la rama lenta tampoco registra el request cuando es sensible (hoy ya distingue; verifícalo).
  4. Agrega una **prueba de arquitectura** en `tests/Sensus.ArchitectureTests/ArchitectureTests.cs` que falle si un tipo que implementa `IRequest<>` tiene propiedades `byte[]` o nombres de la lista anterior y **no** implementa `ISensitiveRequest`. Si la regla genera falsos positivos, usa una lista blanca explícita y comentada.
- **Pruebas:** unitarias de `LoggingBehavior` con un `ILogger` de prueba que capture mensajes: con `CreateFeedPostCommand("contenido secreto", ...)` el texto **no** aparece en ningún mensaje ni propiedad estructurada. Prueba de arquitectura nueva en verde.
- **Terminado cuando:** pruebas verdes, spec 014 documentada (`FR-014-05`, CA, `T014-08`), `requirements.md`.

#### A3. Punto 12 — 024: `imageUrl` en JSON sin moderar (H3)

- **Problema:** `src/Sensus.Api/Endpoints/FeedEndpoints.cs` (rama JSON, ~línea 200) toma `body.ImageUrl` y `CreateFeedPostCommand` (handler dentro de `src/Sensus.Application/Feed/Commands/CreateFeedPost/CreateFeedPostCommand.cs`, ~línea 114) la publica sin moderación si no hay `ImageBytes`. El validador solo exige URL absoluta.
- **Decisión:** rechazar URLs externas (§3.2).
- **Requisito:** `FR-024-08` en `specs/024-publicaciones-feed/spec.md`: "En JSON no se acepta `imageUrl` arbitraria: solo `mediaAssetId` de un `MediaAsset` aprobado y propiedad del autor; cualquier otra URL responde 400". CA-024-08 (URL externa → 400, nada persistido) y CA-024-09 (asset aprobado propio → 201 con su `SecureUrl`; asset de otro usuario o no aprobado → 400). Actualiza `specs/024-publicaciones-feed/contracts/openapi.yaml`.
- **Antes de implementar, revisa el móvil:** `C:\Users\Rodrigo\Sensus\Sensus\app\src\main\java\com\example\sensus\data\remote\dto\FeedDtos.kt` (campo `imageUrl`) y cómo se crea una publicación (`rg -n "feed/posts" app/src/main/java`). Si la app hoy manda `imageUrl` en JSON, el cambio del móvil va en este mismo punto (rama `movil_Rodrigo`): subir la imagen por `multipart` al crear la publicación, o subirla antes por `/api/v1/media/upload` y mandar `mediaAssetId`.
- **Pasos backend:**
  1. Agrega `Guid? MediaAssetId` al body JSON y al comando; elimina la aceptación de `ImageUrl` arbitraria (si el cuerpo trae `imageUrl`, el validador responde 400 con mensaje claro). Mantén `multipart` igual.
  2. En el handler, si viene `MediaAssetId`: carga el asset con el repositorio de media existente (`rg "interface IMediaAssetRepository" src`), exige `OwnerUserId == userId` y estado aprobado; usa su URL segura y su `PublicId`. Si no cumple → `Error.Validation("FeedPost.MediaNotApproved", ...)`.
  3. Mensajes y códigos de error en español, consistentes con los existentes `FeedPost.*`.
- **Pruebas:** unitarias del handler (URL externa rechazada, asset aprobado propio aceptado, asset ajeno rechazado, asset no aprobado rechazado) e integración en `tests/Sensus.IntegrationTests/Feed/FeedPostEndpointsTests.cs` (400 con `imageUrl`, 201 con asset aprobado, 401 sin token). Regresión: una prueba que mande `imageUrl` externa debe fallar antes del arreglo.
- **Móvil (si aplica):** ajusta DTO y repositorio, compila `assembleDebug`, corre `testDebugUnitTest`.
- **Terminado cuando:** pruebas verdes, OpenAPI y spec 024 actualizadas (`T024-10`), `requirements.md`, `memory.md` deja de listar H3 como abierto.

#### A4. Punto 14 — 034: límite de 20 ofertas activas por comercio (`FR-034-05`, ya existe)

- **Requisito existente:** `FR-034-05` "Limitar a 20 ofertas activas por comercio" (Should). Conviértelo en implementado; agrega CA-034-xx si no hay uno medible (21.ª publicación → 409 `Offer.ActiveLimitReached`; borradores no cuentan).
- **Pasos:**
  1. Define el límite en **configuración** (prohibido fijar umbrales en código): por ejemplo `Offers:MaxActivePerMerchant` = 20 en `src/Sensus.Api/appsettings.json`, con clase de settings en Application y validación `[Range(1, 1000)]`.
  2. Agrega a `src/Sensus.Domain/Repositories/IOfferRepository.cs` un método `Task<long> CountActiveByMerchantAsync(Guid merchantId, DateTime nowUtc, CancellationToken)` ("activa" = `Status == Published` y `EndsAt > nowUtc`; confirma la definición en la spec 034/037).
  3. Implementación Mongo en el repositorio de ofertas existente usando el índice compuesto `{MerchantId, Status, ...}` (ya existe uno con `MerchantId, Status, CreatedAtUtc` en `MongoDbContext.cs`; verifica que sirva o declara uno nuevo y justifícalo en `plan.md`).
  4. En `src/Sensus.Application/Offers/Commands/CreateOffer/CreateOfferCommandHandler.cs`, antes de persistir una oferta **no borrador**, cuenta y si `>= límite` devuelve `Error.Conflict("Offer.ActiveLimitReached", ...)` → 409. Aplica la misma regla al pasar una oferta de `Draft`/`Paused` a `Published` (busca el comando de cambio de estado: `rg "UpdateOfferStatus" src`) y al `republish`.
  5. Considera la carrera de dos solicitudes simultáneas: documenta en `plan.md` que el conteo no es atómico y que el riesgo aceptado es exceder en 1; si quieres cerrarlo, usa un contador por comercio con `$inc` condicional.
- **Pruebas:** unitarias del handler (19 → crea, 20 → 409, borrador con 20 activas → crea); integración con Testcontainers del conteo; integración HTTP en `tests/Sensus.IntegrationTests/Offers/CreateOfferEndpointsTests.cs` (409 con ProblemDetails).
- **Terminado cuando:** pruebas verdes, OpenAPI de 034 con 409, `T034-18`, checklist, `requirements.md` (cambia `FR-034-05` a Implementado si ya estaba registrado).

---

### BLOQUE B — CI e infraestructura (~4 h)

#### B1. Punto 5 — Aprobación manual para desplegar a producción

- **Requisito:** `FR-045-19`: "El job de despliegue a producción usa el *environment* de GitHub `production`, que exige aprobación de un revisor antes de ejecutarse".
- **Pasos (código):** en `.github/workflows/deploy-serverless.yml`, en el job `deploy-serverless`, agrega `environment: production` (con `url: https://zqqdwovx81.execute-api.us-east-2.amazonaws.com` opcional). No cambies nada más.
- **Pasos (usuario):** escribe en la checklist y en tu reporte estas instrucciones exactas para el usuario: GitHub → repo `sensus-proj/sensus-back` → Settings → Environments → New environment `production` → Required reviewers: él mismo (y quien decida) → Deployment branches: *Selected branches* → `main`. Mientras no exista el environment, GitHub lo crea vacío **sin** revisores, así que el paso del usuario es obligatorio para que la protección funcione.
- **Terminado cuando:** YAML editado, spec 045 documentada (`T045-20`), instrucciones al usuario escritas.

#### B2. Punto 10 — OIDC para AWS en lugar de llave fija

- **Requisito:** `FR-045-20`: "CI/CD se autentica en AWS con OIDC de GitHub y un rol con permisos mínimos; no se usan llaves de acceso de larga duración".
- **Pasos (CDK en `infra/`):**
  1. Crea un archivo nuevo `infra/src/Sensus.Infra/SensusInfraStack.GitHubOidc.cs` (clase parcial) con:
     - `OpenIdConnectProvider` para `https://token.actions.githubusercontent.com`, audiencia `sts.amazonaws.com`. Antes de crearlo, verifica con `aws iam list-open-id-connect-providers` que no exista ya; si existe, impórtalo con `OpenIdConnectProvider.FromOpenIdConnectProviderArn`.
     - Rol `sensus-github-deploy-<env>` asumible con `sts:AssumeRoleWithWebIdentity` solo si `token.actions.githubusercontent.com:aud == sts.amazonaws.com` y `token.actions.githubusercontent.com:sub` es `repo:sensus-proj/sensus-back:environment:production` (requiere B1) o `repo:sensus-proj/sensus-back:ref:refs/heads/main`.
     - Permisos mínimos: `sts:AssumeRole` sobre los roles de bootstrap de CDK (`arn:aws:iam::<cuenta>:role/cdk-hnb659fds-*-<cuenta>-us-east-2`), lectura/escritura del prefijo `rollback/` del bucket de assets, `lambda:GetFunction`, `lambda:GetFunctionConcurrency`, `lambda:UpdateFunctionCode` sobre `sensus-api-<env>`, `cloudformation:DescribeStacks` sobre el stack, y `sts:GetCallerIdentity`. Justifica cada permiso en `plan.md`.
     - `CfnOutput` `GitHubDeployRoleArn` (no lo imprimas en el resumen del job).
  2. En `deploy-serverless.yml`: agrega `permissions: id-token: write` y `contents: read` (a nivel job o workflow) y un paso `aws-actions/configure-aws-credentials` **fijado por SHA** (obtén el SHA de la última versión estable con `git ls-remote https://github.com/aws-actions/configure-aws-credentials refs/tags/v4*` y elige el tag más alto). Usa `role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}` y `aws-region`. Quita `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` del `env` del job.
  3. Mismo cambio en el job `iac` de `ci.yml` (usa las llaves para `cdk diff`): pásalo a OIDC con un rol de **solo lectura** (por ejemplo `ReadOnlyAccess` + asumir el rol `lookup` de CDK) o deja `cdk diff` condicionado a la existencia del secreto nuevo.
- **Transición segura (escríbela para el usuario, en este orden):** (a) PR con el rol nuevo **pero** el workflow aún con llaves → despliega el rol; (b) el usuario crea el secreto `AWS_DEPLOY_ROLE_ARN` con el valor de la salida `GitHubDeployRoleArn`; (c) segundo PR que cambia el workflow a OIDC; (d) cuando un despliegue con OIDC salga bien, el usuario desactiva y luego borra la llave de `github-cdk-deployer` y los secretos `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`. **Implementa el cambio de workflow detrás de una condición** para que (a) y (c) sean el mismo código si es posible: por ejemplo, usar OIDC si existe `secrets.AWS_DEPLOY_ROLE_ARN` y, si no, las llaves. Documenta que la rama con llaves se elimina después.
- **Pruebas:** `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror`. Si se agrega un proyecto de pruebas de infra (no existe hoy), usa `Amazon.CDK.Assertions` para verificar la condición `sub` del rol; pregunta antes de crear un proyecto nuevo.
- **Terminado cuando:** CDK compila, workflow editado, spec 045 (`FR-045-20`, STRIDE del rol en `plan.md`, `T045-21`), instrucciones de transición escritas.

#### B3. Punto 11 — Monitoreo de rebotes y quejas de SES

- **Requisito:** `FR-029-14`: "Los envíos de SES usan un *configuration set* que publica rebotes, quejas y rechazos a CloudWatch y a una alarma; los rebotes y quejas no se registran con el correo del destinatario en claro".
- **Pasos:**
  1. En CDK (archivo nuevo `SensusInfraStack.Email.cs`): `CfnConfigurationSet` `sensus-transactional-<env>` y `CfnConfigurationSetEventDestination` con `CloudWatchDestination` (dimensión `ses:configuration-set` por defecto) para `BOUNCE`, `COMPLAINT`, `REJECT`, `DELIVERY`. Alarma CloudWatch sobre la métrica de rebotes/quejas conectada al tema SNS de alarmas existente (busca `alarmTopic` en `SensusInfraStack.cs`). Umbral en contexto de CDK (no fijo en código).
  2. Concede a la Lambda `ses:SendEmail` también sobre el ARN del configuration set (`arn:aws:ses:<región>:<cuenta>:configuration-set/sensus-transactional-<env>`), en el mismo statement `AllowSesSendTransactionalEmail`.
  3. La app ya soporta `SesEmailSettings:ConfigurationSetName` (vacío en `appsettings.json`). Indica al usuario que cree `/sensus/prod/SesEmailSettings/ConfigurationSetName` = `sensus-transactional-prod` **después** de desplegar el CDK (si se crea antes, el envío falla porque el configuration set no existe).
  4. Verifica en `src/Sensus.Infrastructure/Services/SesEmailSender.cs` que `ConfigurationSetName` se envía cuando no está vacío; agrega prueba si falta.
  5. Costo: SES → CloudWatch en Free Tier; alarma estándar dentro de las 10 gratuitas (cuenta las existentes: hoy 7; con esta 8).
- **Terminado cuando:** CDK compila, `T029-24`, checklist, instrucción al usuario escrita.

#### B4. Punto 13 — `cdk-nag` y SBOM en CI (G-03, G-04)

- **Requisitos:** `FR-047-19` (escaneo de IaC bloqueante con `cdk-nag` AwsSolutions), `FR-047-20` (SBOM CycloneDX del backend como artefacto en CI y en el despliegue).
- **Pasos cdk-nag:**
  1. Paquete NuGet `Cdklabs.CdkNag` en `infra/src/Sensus.Infra/Sensus.Infra.csproj`. Justifica en `plan.md`: versión exacta (la última estable compatible con `Amazon.CDK.Lib` del proyecto; revisa `nuget.org`), licencia Apache-2.0, costo 0. Esto es una dependencia nueva: AGENTS.md pide justificarla, no aprobación explícita, pero menciónala en tu reporte.
  2. En `infra/src/Sensus.Infra/Program.cs`: `Aspects.Of(app).Add(new AwsSolutionsChecks(new NagPackProps { Verbose = true }));`.
  3. Corre la síntesis donde puedas (CI lo hará) y para cada hallazgo: corrígelo, o suprímelo con `NagSuppressions.AddResourceSuppressions(...)` **con razón escrita** (por ejemplo, `AwsSolutions-IAM5` para comodines justificados). Lista cada supresión en `plan.md`.
  4. Como no hay CDK CLI local, si no puedes sintetizar, deja la integración lista y documenta que el primer `cdk synth` de CI revelará los hallazgos; **no** marques G-03 como cerrada hasta ver el CI verde.
- **Pasos SBOM:**
  1. En `ci.yml`, job `quality`: instala `CycloneDX` como herramienta .NET con versión fija (`dotnet tool install --global CycloneDX --version <x.y.z>`, revisa la última estable) y genera `bom.json` de `sensus-back.sln`. Súbelo con `upload-artifact` (SHA aprobado). Haz lo mismo en `deploy-serverless.yml` antes de `cdk deploy` (para que el SBOM corresponda a lo desplegado).
  2. Procedencia (SLSA): `actions/attest-build-provenance` fijado por SHA sobre el `bootstrap` publicado, con `permissions: id-token: write, attestations: write`. Si complica el flujo, déjalo documentado como siguiente paso y no cierres G-04 del todo.
- **Terminado cuando:** CDK compila con cdk-nag, workflows editados, spec 047 (`FR-047-19`, `FR-047-20`, `T047-21`, `T047-22`), G-03/G-04 actualizadas con su estado real.

#### B5. Punto 16 — ZAP baseline (DAST, G-02)

- **Requisito:** `FR-047-21`: "Escaneo DAST pasivo (ZAP baseline) contra la API desplegada, manual y programado de día; los hallazgos de severidad alta bloquean el siguiente despliegue".
- **Pasos:**
  1. Workflow nuevo `.github/workflows/dast-zap.yml`: disparadores `workflow_dispatch` y `schedule` en horario diurno de México (por ejemplo `cron: '0 17 * * 1'` = 11:00 CDMX lunes). Job con `permissions: contents: read`.
  2. Usa `zaproxy/action-api-scan` **fijado por SHA** en modo baseline/pasivo, o el contenedor `ghcr.io/zaproxy/zaproxy:stable` con `zap-api-scan.py -t <openapi> -f openapi` en modo **pasivo** (`-S` o configuración baseline). Como Swagger ya no es público (punto 6), genera el OpenAPI en CI: construye la API y obtén `/openapi/v1.json` levantándola en modo Development dentro del job, o versiona el documento generado. Elige una y justifícala.
  3. Reglas en `.zap/rules.tsv` versionado para ajustar falsos positivos con justificación.
  4. Sube el reporte HTML/JSON como artefacto. No pongas la URL de producción como secreto ni la imprimas con datos sensibles.
  5. Corre el workflow **una vez manualmente** solo si el usuario hace push (no lo hagas tú). Alternativa local autorizada por el usuario: correr ZAP baseline con Docker desde la PC **de día** contra producción, solo pasivo, y adjuntar el resultado al checklist.
- **Terminado cuando:** workflow y reglas creados, spec 047 (`T047-23`), y — si se corrió — evidencia del primer reporte. G-02 queda "cerrada en código" hasta ver una corrida real sin altos.

---

### BLOQUE C — Flujos de seguridad (~9 h)

#### C1. Punto 17 — 024/017: cuarentena real y auditoría de publicaciones

- **Problema:** `CreateFeedPostCommand` (handler) devuelve error ante rechazo o fallo de moderación sin persistir nada en estado `Quarantined` (el enum `FeedPostStatus` ya tiene `PendingModeration`, `Published`, `Quarantined`, `Deleted`) ni registrar auditoría. `ModerationTargetType` (`src/Sensus.Domain/Enums/ModerationAuditEnums.cs`) no tiene `FeedPost`.
- **Requisitos:** `FR-024-09` (ante fallo, timeout o respuesta inválida del moderador la publicación se persiste en `Quarantined`, no aparece en el feed y no se sube imagen a Cloudinary; ante rechazo explícito → 400 y no se persiste), `FR-021-05` (toda decisión de moderación de publicaciones del feed se registra en la auditoría de solo adición, con veredicto, categorías, puntajes, umbral y acción, sin el contenido).
- **Pasos:**
  1. Agrega `FeedPost = 6` al final de `ModerationTargetType` (**no** reordenes valores existentes: están persistidos).
  2. En el handler: distingue **rechazo** (contenido inapropiado → 400, sin persistir, auditoría `Rejected`) de **indisponibilidad** (servicio nulo, excepción, timeout, respuesta malformada → persistir con `Status = Quarantined`, sin imagen, devolver 202 con `status: Quarantined` o 503; decide con la spec 024 vigente — CA-024-06 dice "queda en cuarentena" — y documenta el código HTTP en el OpenAPI).
  3. Usa `IModerationAuditRecorder` igual que `CreateOfferCommandHandler` (registro antes de persistir; fail-closed si la auditoría de una aprobación falla).
  4. Confirma que el feed cercano (`GetNearbyFeedQuery`) y el detalle excluyen `Quarantined` (hay prueba `Like_Returns404_WhenPostIsQuarantinedOrMissing`, revisa también listados).
  5. Define cómo sale una publicación de cuarentena: reutiliza el flujo de liberación de media (`ReleaseQuarantinedMedia`) si encaja; si no, deja documentado que se purga por retención (`Moderation:QuarantineRetentionDays`) y agrega la publicación a esa purga.
- **Pruebas:** unitarias (rechazo, indisponible, timeout, malformada, aprobada; auditoría registrada en cada caso), integración HTTP y Testcontainers (persistido en `Quarantined`, excluido del feed). Actualiza la spec 017 (sus tareas T017-07, T017-12 pueden cerrarse; revisa su texto).
- **Terminado cuando:** pruebas verdes, specs 024, 017 y 021 documentadas, OpenAPI 024 actualizado.

#### C2. Punto 19 — 034: 202 cuando la moderación queda pendiente (`FR-034-07`, ya existe)

- **Decisión:** la oferta sin veredicto inmediato queda privada y la API responde 202 (§3.4).
- **Pasos:**
  1. En `CreateOfferCommandHandler`, cuando la evaluación sea **indisponible** (no rechazo): persiste la oferta como `Draft` (privada) con una marca `ModerationPending = true` (o un estado nuevo `PendingModeration`; si agregas valor a `OfferStatus`, hazlo **al final** del enum) y devuelve un resultado que el endpoint traduzca a **202** con `Location` del detalle privado.
  2. Proceso de re-moderación: un `BackgroundService` en `src/Sensus.Api/Services/` (hay ejemplos: `QuarantineRetentionBackgroundService`, `DeletedAccountPurgeBackgroundService`) que reintente la moderación de ofertas pendientes cada N minutos (configurable) y publique si aprueba, rechace si no; con límite de intentos y auditoría. Respeta la pausa nocturna (la Lambda no corre de noche; el servicio en Lambda solo corre mientras hay invocaciones — documenta esta limitación y, si hace falta, usa EventBridge Scheduler en CDK como en la pausa nocturna).
  3. Notificaciones: el aviso de ofertas cercanas (`FR-044-08`) solo se dispara cuando la oferta pasa a publicada.
- **Pruebas:** unitarias del handler (indisponible → 202 y no pública; aprobada → 201), del servicio de re-moderación, integración HTTP (202, detalle visible solo para el dueño, no aparece en búsquedas).
- **Terminado cuando:** pruebas verdes, OpenAPI 034 con 202, `FR-034-07` Implementado, tareas y checklist.

#### C3. Punto 20 — H2: moderar antes de Cloudinary y almacenamiento privado

- **Problema:** `src/Sensus.Application/Media/Commands/UploadMedia/UploadMediaCommandHandler.cs` llama `_mediaStorage.UploadImageAsync` (~línea 74) **antes** de `_moderationService.ModerateImageBytesAsync` (~línea 86). `src/Sensus.Infrastructure/Services/CloudinaryMediaStorage.cs` no fuerza `type=private`/`access_mode=authenticated`.
- **Requisito:** `FR-017-10` (moderar bytes antes de cualquier subida; rechazada o indisponible → no se sube nada y se audita) y `FR-022-xx` o `FR-023-xx` (verifica qué spec es dueña de Cloudinary: `rg -l "Cloudinary" specs/*/spec.md`) para almacenamiento `authenticated` y entrega solo por URLs firmadas (`MediaDeliveryEndpoints.cs` ya firma con HMAC).
- **Pasos:**
  1. Reordena el handler: valida → modera bytes → si aprobado, sube → persiste asset → audita. Si indisponible: no sube; guarda evidencia mínima (hash) en cuarentena si la spec lo pide, sin bytes en Cloudinary.
  2. En `CloudinaryMediaStorage` (y `CloudinaryImageStorage` si sube imágenes de usuario): `type = "authenticated"` (o `private`) en los parámetros de subida; las URLs públicas dejan de servir. Revisa **todos** los consumidores de `SecureUrl` (feed, ofertas, perfil, reseñas) para que entreguen URL firmada o pasen por `MediaDeliveryEndpoints`.
  3. **Migración:** las imágenes ya subidas son públicas. No las cambies por CLI ni por script contra producción sin preguntar; documenta un plan de migración y pregunta al usuario.
  4. Móvil (`movil_Rodrigo`): `CloudinarySignedUploader.kt` y `MediaApiService.kt`/`MediaDto.kt` — si la app sube directo a Cloudinary con firma (`/api/v1/users/photo-signature`), esa subida **evita la moderación previa**: o se mueve a `/api/v1/media/upload` (moderado) o la firma se emite con `type=authenticated` y la foto se modera antes de usarse. Elige la opción que no deje contenido sin moderar público y justifícala.
- **Pruebas:** unitarias (orden: el doble de storage **no** se llama si la moderación rechaza o falla), integración HTTP de `POST /api/v1/media/upload` (hoy **no tiene** integración: agrega éxito, 400, 401, 403, 429), móvil `testDebugUnitTest`.
- **Terminado cuando:** pruebas verdes, specs documentadas, plan de migración escrito y preguntado al usuario, `memory.md` deja H2 como cerrado en código.

#### C4. Punto 18 — Tokens de 15 min con refresh rotatorio (G-06)

- **Decisión:** acceso 15 min; refresh 30 días, rotatorio, con detección de reúso (§3.5).
- **Requisitos:** `FR-002-08` (login devuelve `accessToken` de 15 min y `refreshToken` opaco), `FR-002-09` (`POST /api/v1/auth/refresh` rota el refresh), `FR-002-10` (reúso de un refresh rotado revoca la familia y rota `SecurityStamp`), `FR-004-05` (logout revoca la familia del refresh del dispositivo). `NFR-002-03`: refresh almacenado solo como hash SHA-256, nunca en claro; TTL en MongoDB.
- **Pasos backend:**
  1. Entidad de dominio `RefreshToken` (id, userId, familyId, tokenHash, createdAt, expiresAt, rotatedAt, revokedAt, replacedById, deviceId opcional) en `Sensus.Domain`; repositorio en Domain; implementación Mongo con índices: único en `tokenHash`, por `familyId`, TTL en `expiresAt`. Decláralos en `MongoDbContext.cs` y justifícalos.
  2. `JwtSettings.ExpiryMinutes` pasa a 15 en `appsettings.json` y su validador (`JwtSettingsValidator`) limita a `1..15`. Nuevo `JwtSettings:RefreshTokenDays` = 30 (validado).
  3. Login (`src/Sensus.Application/Users/Commands/Login/LoginCommandHandler.cs`) y registro: emiten refresh (32 bytes aleatorios, base64url) y guardan solo el hash. `AuthResponseDto` (`src/Sensus.Application/Users/DTOs/AuthResponseDto.cs`) agrega `RefreshToken` y `RefreshExpiresAtUtc` (**cambio aditivo**: no quites campos).
  4. `POST /api/v1/auth/refresh` en `AuthEndpoints.cs` (agrega la ruta a la spec 002 y a su OpenAPI **antes** de implementarla): valida, rota, detecta reúso; rate limit `auth-strict`; comando con `ISensitiveRequest`.
  5. Logout (`LogoutCommandHandler.cs`) revoca la familia.
  6. Compatibilidad: versiones viejas de la app no conocen el refresh; con 15 min de acceso se les cerrará la sesión cada 15 min. Documenta este impacto y coordínalo con el móvil en el mismo punto.
- **Pasos móvil (`movil_Rodrigo`):** guardar `refreshToken` cifrado (EncryptedSharedPreferences/Keystore; revisa `UserSessionEntity.java` y `SensusDatabaseMigrations.java` — **no** lo guardes en claro en Room); un `Authenticator` de OkHttp en `NetworkClient.kt` que, ante 401, llame `/auth/refresh` una sola vez (con *mutex* para no rotar en paralelo) y reintente; si el refresh falla, cerrar sesión. Pruebas unitarias del autenticador con `MockWebServer`.
- **Pruebas backend:** unitarias (emisión, rotación, reúso → revocación de familia, expiración), integración HTTP (`/auth/refresh` 200/401/429, logout revoca), Testcontainers del repositorio (índice único y TTL). Actualiza `JwtSettingsValidator` tests.
- **Terminado cuando:** backend y móvil compilan y pasan pruebas, specs 002 y 004 documentadas (STRIDE de sesión en `plan.md`), G-06 "cerrada en código".

#### C5. Punto 22 — Segundo factor TOTP para Administrador (G-05)

- **Spec nueva 062** `specs/062-segundo-factor-administrador/` con los **cuatro** artefactos (copia las plantillas de `specs/_templates/`), entrada en `specs/README.md` y requisitos en `specs/requirements.md`. Alineada a `constitution.md` v3.0.0.
- **Requisitos sugeridos:** `FR-062-01` enrolamiento TOTP (RFC 6238, SHA-1, 6 dígitos, 30 s) con secreto cifrado en reposo; `FR-062-02` el login de un Administrador con TOTP activo exige el código (respuesta intermedia sin token de acceso completo); `FR-062-03` *step-up*: endpoints `AdminOnly` exigen un claim `amr=mfa` emitido en los últimos N minutos (configurable); `FR-062-04` códigos de recuperación de un solo uso (hash); `FR-062-05` auditoría de solo adición de enrolamiento, verificación y fallos; `NFR-062-01` límite de intentos y bloqueo temporal; `NFR-062-02` ventana de tolerancia ±1 paso.
- **Pasos:** endpoints nuevos (por ejemplo `POST /api/v1/auth/mfa/enroll`, `POST /api/v1/auth/mfa/verify`) — agrégalos primero a la spec y OpenAPI. Implementación TOTP: usa una librería mantenida (por ejemplo `Otp.NET`, licencia MIT) justificando versión/licencia/costo, o implementa RFC 6238 con `HMACSHA1` y vectores de prueba oficiales del RFC. El secreto se cifra con una clave en SSM (`/sensus/<env>/Mfa/EncryptionKey`, la crea el usuario; escribe la instrucción). Las 7 rutas `AdminOnly` actuales están en `MediaEndpoints.cs`, `ModerationAuditEndpoints.cs`, `OfferEndpoints.cs` y `SanctionEndpoints.cs`.
- **Pruebas:** vectores RFC 6238, enrolamiento, login con y sin código, step-up vencido → 403, códigos de recuperación, límite de intentos, auditoría.
- **Terminado cuando:** spec 062 completa y válida, pruebas verdes, G-05 "cerrada en código" (la auditoría generalizada de G-05 puede quedar parcial: dilo).

---

### BLOQUE D — Pruebas, STRIDE y validaciones en vivo (~4 h)

#### D1. Punto 9 — Pruebas de 039 y Testcontainers pendientes

- **Pasos:**
  1. Asegura Docker: `docker info`. Si falla, pide al usuario abrir Docker Desktop.
  2. Limpia restos que rompen pruebas: la corrida previa dejó una tabla en DynamoDB Local (`ResourceInUseException: Cannot create preexisting table`). Revisa `tests/Sensus.IntegrationTests/**/DynamoDbLocalBudgetCounterStoreTests.cs`: si la prueba usa un contenedor fijo o un nombre de tabla fijo, corrígela para usar nombre único por corrida (es un defecto de la prueba, con regresión).
  3. Corre las clases de Testcontainers (búscalas: `rg -l "Testcontainers|MongoDbContainer|RedisContainer|DynamoDb" tests/Sensus.IntegrationTests`), entre ellas `MongoFeedSocialIntegrationTests` (spec 039: unicidad, idempotencia y concurrencia `CA-039-04`).
  4. Registra conteos exactos y cierra `T039-09`, la casilla de concurrencia de la checklist 039 y la casilla V del Constitution Check si quedan verdes.
- **Terminado cuando:** 0 fallos en las ~50 pruebas de contenedores o cada fallo documentado con causa y arreglo.

#### D2. Punto 21 — STRIDE en specs críticas (G-11)

- **Specs:** 001, 002, 003, 004, 029, 030, 033, 034 (`ls specs` para los slugs exactos).
- **Pasos:** en cada `plan.md`, sección "Modelo de amenazas (STRIDE — Principio VIII)" con tabla `Amenaza | Escenario | Control existente (con archivo/prueba) | Riesgo residual`, una fila por letra S, T, R, I, D, E como mínimo. Usa controles **reales** del código (busca y cita archivos/pruebas). La spec 046 ya tiene una sección STRIDE: úsala como formato. La 030 está `Escindida` en 060/061: agrega STRIDE en 060 y 061 y una nota en 030.
- **Terminado cuando:** las 8 (+060/061) tienen STRIDE, `check-specs` verde, G-11 "Cerrada".

#### D3. Punto 23 — Push real con FCM y p95 (027/033), en producción de día

- **Pasos:**
  1. Verifica que existe `/sensus/prod/push/fcm-service-account` (`aws ssm describe-parameters`; ya existe) y que la app instalada en los celulares es de `movil_Rodrigo` compilada contra producción.
  2. El usuario inicia sesión en ambos celulares (no uses sus contraseñas). Pídele que lo haga.
  3. Dispara un evento real que genere push (apartado de una oferta de prueba del propio usuario, spec 033) **solo con datos del usuario** y verifica la llegada con `adb -s <id> shell dumpsys notification --noredact | findstr sensus` o pidiendo al usuario confirmación visual. No crees cuentas ni ofertas de terceros.
  4. p95 de apartado (`NFR-033-01` < 700 ms): solo dentro del punto D4.
  5. Si el push requiere que la app esté en primer plano/segundo plano/cerrada, prueba los tres estados con `adb shell am force-stop sensus.com` y documenta.
- **Terminado cuando:** evidencia por celular y estado de app en checklists 027 y 033; cierra `T027-12`, `T027-13`, `T033-13`, `T033-14` si aplica.

#### D4. Punto 24 — Validaciones en vivo y carga baja (046, 049, 050, G-13)

- **Restricciones:** producción, de día, carga baja y acotada, **sin** endpoints `/api/v1/ai/*` (no gastar OpenAI; los p95 de Sensi quedan documentados como no medidos por decisión del usuario).
- **Pasos:**
  1. Herramienta: `k6` (binario único, licencia AGPL para uso local, sin costo). Pregunta al usuario antes de instalarlo. Alternativa sin instalar: script Node con `fetch` y medición de percentiles en `.github/scripts/load-smoke.mjs` con pruebas.
  2. Escenarios de solo lectura con token de invitado (no requiere credenciales): `GET /health/ready`, `GET /api/v1/offers/nearby?lat=19.4326&lng=-99.1332&radiusKm=2`, `GET /api/v1/feed/...` según permisos. Tasa máxima 5 rps durante 2 min, con respeto al rate limiting (no busques provocar 429).
  3. Escenarios autenticados solo con la cuenta del usuario y si él la proporciona en su máquina (no pidas su contraseña en el chat; que la ponga en una variable de entorno local).
  4. Reporta p50/p95/p99 por ruta y compáralos con los NFR de cada spec. Guarda el script en el repo y el resultado en los checklists.
  5. 049: verifica un turno de Sensi **solo si el usuario lo autoriza explícitamente** (gasta OpenAI). 050: valida amigos contra MongoDB real con las cuentas del usuario.
- **Terminado cuando:** resultados documentados; G-13 actualizada con su estado real.

---

### 5.Z Puntos que solo puede hacer el usuario (escríbele estas instrucciones en tu reporte final)

1. **PR `Back_Rodrigo` → `develop` → `main`**, de día. Luego revisar en GitHub Actions que "Prueba de humo posterior al despliegue" salga APROBADA. (Con B1, alguien debe aprobar el despliegue.)
2. **Probar la recuperación de contraseña** con su Gmail desde la app después de las 07:00.
3. **SES fuera del sandbox:** esperar respuesta de AWS; consultar con `aws sesv2 get-account --region us-east-2 --query "{Prod:ProductionAccessEnabled,Review:Details.ReviewDetails.Status}"`.
4. **Llave de Figma (G-12, vencida 2026-11-23):** en el repo móvil existen `mcp_config.json` y `.agent/mcp_config.json`. El ejecutor debe: confirmar que ambos están en `.gitignore` (`git check-ignore -v <archivo>`), **no** leer ni copiar la llave a ningún lado, y documentar el cambio a variable de entorno del usuario (`FIGMA_API_KEY`) en `mcp_config.example.json`. El usuario debe **revocar la llave en Figma** (Settings → Personal access tokens) y crear una nueva en su variable de entorno. Después, actualizar G-12 a "Cerrada".
5. **GitHub:** crear el environment `production` con revisores (B1) y el secreto `AWS_DEPLOY_ROLE_ARN` (B2); luego borrar las llaves viejas.
6. **SSM:** crear `/sensus/prod/SesEmailSettings/ConfigurationSetName` (B3, después del despliegue) y `/sensus/prod/Mfa/EncryptionKey` como `SecureString` (C5).

---

## 6. Verificación global al cerrar cada bloque

Ejecuta todo y registra los números exactos:

```powershell
dotnet build sensus-back.sln -warnaserror
dotnet format sensus-back.sln --verify-no-changes
dotnet test tests/Sensus.Application.UnitTests
dotnet test tests/Sensus.ArchitectureTests
dotnet test tests/Sensus.E2ETests
dotnet test tests/Sensus.IntegrationTests
node .github/scripts/check-specs.mjs specs
node --test .github/scripts/run-ai-evals.test.mjs .github/scripts/post-deploy-smoke.test.mjs
node .github/scripts/run-ai-evals.mjs --mode deterministic --output-dir $env:TEMP\evals-det
dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror
git status --short
```

- Cobertura ≥ 80 % en Domain y Application (`dotnet test --collect:"XPlat Code Coverage"` y `node .github/scripts/check-coverage.mjs <dir> 80 Sensus.Domain Sensus.Application`).
- Móvil (si se tocó): `gradlew.bat assembleDebug testDebugUnitTest` en `C:\Users\Rodrigo\Sensus\Sensus`.
- Actualiza `memory.md`: versión (siguiente PATCH), fila en "H. Historial de actualizaciones", "Última tarea completada", "Próximos pasos" y hallazgos H1–H4 / brechas G-xx con su estado real.

---

## 7. Formato del reporte al usuario (en español)

Al cerrar cada bloque, responde con:

1. Una línea: qué quedó listo y qué no.
2. Tabla por punto: `Punto | Estado (Hecho / Parcial / Bloqueado) | Evidencia (pruebas y conteos) | Qué falta`.
3. Resultados de la verificación global con números.
4. Lista de acciones que le tocan al usuario, con comandos o pasos exactos.
5. Riesgos o decisiones que necesitan su respuesta, cada una como pregunta concreta.

Nunca digas que algo está "verificado en producción" si no lo comprobaste en vivo. Nunca afirmes conteos de pruebas que no corriste.
