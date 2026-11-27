# Plan de ejecución — Cierre de todas las specs del backend Sensus

| Campo | Valor |
|---|---|
| Fecha | 2026-11-26 |
| Autor del plan | Claude (sesión con Rodrigo Sánchez, responsable del proyecto) |
| Destinatario | Modelo de IA que ejecutará el trabajo sin supervisión |
| Repositorio backend | `C:\Users\Rodrigo\source\repos\sensus-back`, rama `Back_Rodrigo`, base `4a00d24` |
| Repositorio móvil | `C:\Users\Rodrigo\Sensus\Sensus`, rama `movil_Rodrigo`, base `c215700` (solo para el punto A3) |
| Normativa | `constitution.md` v3.0.0, `AGENTS.md`, `memory.md` |
| Plan anterior | `docs/plan-ejecucion-remediacion-2026-11-26.md` (ya ejecutado; sus bloques A–D están en `a8b66dd`) |
| Estimación | ~14–18 h efectivas en 4 bloques |

---

## 0. Objetivo y definición de "terminado"

**Objetivo:** que **cada** spec activa del backend (`specs/001` a `specs/062`, salvo `028` en backlog) quede con:

1. Todas sus tareas en `tasks.md` con estado final (`Completada`, `Hecho` o `Escindida → NNN`) **y evidencia concreta** (archivo, prueba o comando). Ninguna tarea `Pendiente` ni `Parcial` sin razón externa documentada.
2. Su `checklist.md` sin casillas `- [ ]`, salvo las que dependen de **una acción del usuario** listada en la sección 5.Z. Esas casillas se dejan abiertas, con el texto `(acción del usuario: <qué>; ver plan cierre §5.Z-<n>)` al final.
3. Constitution Check de `plan.md` y `checklist.md` en PASS en los **ocho** principios, o con la brecha del Apéndice F citada.
4. Código que cumple el requisito, con las pruebas que exige `AGENTS.md` §11, todas en verde.

**Prohibido marcar algo como completo sin evidencia.** Si una tarea dice algo que el código no hace, es un **hueco real**: impleméntalo (si es de alcance de esta spec) o regístralo en la sección "Hallazgos" del reporte final. No lo cierres en papel.

---

## 1. Cómo usar este documento

1. Lee completo este archivo. Después lee `AGENTS.md`, `constitution.md` (Principio VIII, Apéndices E y F) y `memory.md`.
2. Ejecuta los bloques **en orden: A → B → C → D**. Dentro de cada bloque, los puntos en el orden escrito.
3. No avances al siguiente punto hasta cumplir su **criterio de terminado**. Si no puedes, deja el punto como `Parcial` en `tasks.md` y `checklist.md` con la razón exacta, y sigue con el siguiente.
4. Al cerrar cada bloque, corre la verificación global de la sección 6 y escribe el avance en `docs/avance-cierre-specs-2026-11-26.md` (sección 7).
5. Si algo contradice este plan o la constitución, **detente y pregunta**. No inventes alcance.

---

## 2. Reglas no negociables

### 2.1 Git

- **Backend y móvil:** PROHIBIDO `git commit`, `push`, `merge`, `rebase`, `stash`, `reset`, `checkout -- <archivo>`, `clean`, crear o borrar ramas. Todo queda **sin commit**. El usuario revisa y sube los cambios con Claude.
- Antes de empezar: `git -C <repo> status --short` en ambos repos. El backend debe estar limpio en `Back_Rodrigo` (`4a00d24`). El móvil, en `movil_Rodrigo` (`c215700`), con solo la carpeta sin seguimiento `Claude outputs/`. Si ves otra cosa, **detente y pregunta**.
- Nunca uses `--force` ni `--no-verify`.

### 2.2 AWS, GitHub y producción

- Nunca `cdk deploy`/`destroy` ni cambios de recursos por CLI o consola. Todo recurso nuevo va en `infra/` con CDK.
- AWS solo de lectura (`describe`, `get`, `list`, `logs filter-log-events`). Si la sesión expiró, anótalo y sigue sin AWS; **no** pidas `aws login` en mitad de la noche.
- **Cero peticiones a la API de producción.** Las validaciones en vivo son de la sección 5.Z y las hace el usuario de día.
- GitHub: no existe `gh`. Puedes **leer** el estado del CI por la API pública sin token, por ejemplo:
  `curl -s "https://api.github.com/repos/sensus-proj/sensus-back/actions/runs?branch=Back_Rodrigo&per_page=5"`.
  No configures nada en GitHub.
- Nunca escribas secretos, llaves, correos personales, IDs de cuenta ni tokens en el repo, en logs ni en el reporte.

### 2.3 Flujo SDD por cada cambio de código (AGENTS.md §3)

1. `spec.md`: requisito nuevo `FR-<spec>-<nn>` o `NFR-<spec>-<nn>` con criterio `CA-<spec>-<nn>`. Sube la versión MINOR.
2. `plan.md`: diseño, costo (0 USD) y STRIDE si toca identidad, datos o acciones de administrador.
3. `tasks.md`: tarea `T<spec>-<nn>`, de `Pendiente` a `Completada (<evidencia>)`.
4. Código, con pruebas que **fallan antes** del cambio cuando se trate de un defecto.
5. `checklist.md`: fila de evidencia con fecha, responsable `Ejecutor cierre specs`, resultado y evidencia (pruebas y conteos).
6. `specs/requirements.md`: fila nueva justo después del último requisito de esa spec.
7. Si cierra una brecha del Apéndice F, actualiza su estado.
8. `node .github/scripts/check-specs.mjs specs` → `Estructura SDD correcta.`

**Trampas conocidas:**

- `check-specs.mjs` rechaza IDs de **otra** spec dentro de `spec.md`. En `spec.md` escribe "Spec 047", nunca `FR-047-xx`. En `plan.md`, `tasks.md` y `checklist.md` sí se permiten.
- Muchos archivos son **CRLF**. Conserva el salto de línea original al editar con scripts. Con `sed`, no ancles patrones con `$`.
- Las casillas de checklist se marcan cambiando `- [ ]` por `- [x]`. No borres casillas.

### 2.4 Siguientes IDs libres (verificados el 2026-11-26; vuelve a verificar antes de usar)

| Spec | Último FR | Último NFR | Último CA | Última tarea | Versión spec |
|---|---|---|---|---|---|
| 001 registro | FR-001-07 | NFR-001-02 | CA-001-04 | T001-09 | 1.1.0 |
| 002 inicio de sesión | FR-002-07 | NFR-002-02 | CA-002-06 | T002-10 | 1.1.0 |
| 003 perfil | FR-003-09 | NFR-003-02 | CA-003-08 | T003-11 | 1.1.0 |
| 004 cierre de sesión | FR-004-05 | NFR-004-02 | CA-004-03 | T004-11 | 1.1.0 |
| 017 moderación | FR-017-09 | NFR-017-02 | CA-017-07 | T017-15 | 1.3.0 |
| 022 carga multimedia | FR-022-07 | NFR-022-03 | CA-022-05 | T022-12 | 1.1.0 |
| 024 publicaciones | ver `spec.md` (A2 de ayer la modificó) | | | | |
| 045 infraestructura | FR-045-20 | NFR-045-05 | CA-045-10 | T045-21 | 1.1.0 |
| 051 pausa nocturna | FR-051-08 | NFR-051-04 | CA-051-05 | T051-10 | 1.0.0 |

Comando para recalcular una spec (Git Bash):
`grep -ohE "FR-002-[0-9]+" specs/002-*/spec.md | sort -t- -k3 -n | tail -1`

### 2.5 Pruebas obligatorias (AGENTS.md §11)

| Cambio | Prueba |
|---|---|
| Regla de dominio | Unitaria en `tests/Sensus.Application.UnitTests` |
| Endpoint nuevo o modificado | Integración en `tests/Sensus.IntegrationTests`: éxito, entrada inválida (400), sin autenticación (401), sin permisos (403) |
| Persistencia | Testcontainers contra MongoDB real (Docker Desktop debe estar arriba; si `docker info` falla, anótalo y sigue con lo demás) |
| Integración externa (OpenAI, SES, FCM, Cloudinary) | Doble que simule fallo, timeout y respuesta malformada |
| Corrección de defecto | Regresión que falle antes del arreglo |
| Script Python o JS de CI | Prueba con la librería estándar (`unittest`, `node --test`) agregada a `ci.yml` |

Prohibido bajar umbrales, omitir pruebas (`Skip`), comentar aserciones o relajar escáneres (gitleaks, `check_secrets`, Semgrep, Snyk, cdk-nag). Un falso positivo se resuelve cambiando el dato de prueba, nunca la regla.

### 2.6 Comandos (desde la raíz del backend)

```powershell
dotnet build sensus-back.sln -warnaserror
dotnet test tests/Sensus.Application.UnitTests
dotnet test tests/Sensus.ArchitectureTests
dotnet test tests/Sensus.IntegrationTests --filter "FullyQualifiedName~<Clase>"
dotnet test tests/Sensus.E2ETests
dotnet format sensus-back.sln --verify-no-changes
node .github/scripts/check-specs.mjs specs
node --test .github/scripts/load-smoke.test.mjs .github/scripts/run-ai-evals.test.mjs .github/scripts/post-deploy-smoke.test.mjs
dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror
```

Móvil (solo A3): `C:\Users\Rodrigo\Sensus\Sensus\gradlew.bat detekt ktlintCheck testDebugUnitTest koverXmlReportDebug koverVerifyDebug --no-daemon`, y `python scripts/check_secrets.py`, `check_sdd.py`, `check_layers.py` y `check_endpoints.py`.

---

## 3. Decisiones ya tomadas por el usuario (no las vuelvas a preguntar)

1. **Retirar los caminos de imagen heredados.** `POST /api/v1/users/photo-signature` responde **410 Gone**. El alias `mediaId` del feed se retira. `fotoPerfilUrl` solo se acepta si es la `SecureUrl` de un `MediaAsset` **aprobado del mismo usuario**.
2. **G-14 (autenticación IAM de Valkey) queda fuera** de este plan. Se mantiene `Abierta` en el Apéndice F con su fecha límite. No la implementes.
3. **Spec 028 (webhooks) es backlog** según `specs/README.md`. No se implementa. Sus tareas quedan `Pendiente (backlog)` y sus casillas abiertas, y el reporte lo dice.
4. **Sin commits ni push** en ningún repositorio.
5. **Cero tráfico a producción** por parte del ejecutor.
6. **Brechas solo móviles (G-07, G-08, G-09, G-12)** y la del aviso de privacidad (FT-23, landing) están fuera de este repo. Se citan en el reporte, no se tocan.

---

## 4. Estado al entregar este plan (verificado en código el 2026-11-26)

- Build `-warnaserror` sin advertencias. Unitarias 1215/1215, arquitectura 6/6, `check-specs` 62/62. Las de integración (726) e E2E pasaron en la ejecución nocturna anterior.
- CI de `Back_Rodrigo`: el paso SBOM se corrigió en `4a00d24`. Confirma con la API pública que la última corrida esté verde antes de cerrar el punto B3.
- Inventario de tareas no cerradas por spec, en tres tipos:
  - **Documentación atrasada.** El código existe y solo falta cerrar el papel con evidencia: 001, 002, 003, 005, 006, 008 (checklist), 013, 015, 016, 019, 022, 027 (DoR), 031, 034 (DoR) y 017 (`T017-04`, `T017-08`).
  - **Huecos reales de código:** los puntos del Bloque A.
  - **Dependen del usuario o del despliegue:** 027 (FCM real), 029 (sandbox SES y `ConfigurationSetName`), 033 (push real), 042/055 (aviso de privacidad y p95), 045 (environment, OIDC y despliegue), 047 (primera corrida de cdk-nag, SBOM y ZAP), 048/051 (verificación posterior al despliegue), 049 (turno real de Sensi), 050 (validación en vivo). Ver la sección 5.Z.

---

## 5. Puntos a ejecutar

### BLOQUE A — Huecos reales de código y seguridad (~6 h)

#### A1. `/auth/me` devuelve un objeto anónimo (Spec 002, `T002-09`)

- **Hoy:** `src/Sensus.Api/Endpoints/AuthEndpoints.cs`, alrededor de la línea 168, hace `Results.Ok(new { UserId, NombreCompleto, ... })` armado desde los claims. Viola `AGENTS.md` §7.3: no hay DTO ni mapeo probado.
- **Hacer:**
  1. Crea `record CurrentUserDto(...)` en `src/Sensus.Application/Users/` con los mismos campos y nombres JSON que hoy (revisa la serialización actual para no romper la app: el móvil lee la respuesta).
  2. Crea la consulta `GetCurrentUserQuery` y su handler, que arma el DTO a partir de `ICurrentUserService` y los claims. Esto es lectura: no mezcles escritura (CQRS).
  3. El endpoint envía la consulta y devuelve `result.ToHttpResult()`. Agrega `.Produces<CurrentUserDto>(200)` y `.ProducesProblem(401)`.
  4. Actualiza `specs/002-inicio-sesion/contracts/openapi.yaml` con el esquema `CurrentUserDto`.
- **Pruebas:** unitaria del mapeo (todos los campos, roles sin duplicados, claim ausente → `null`), integración 200 con JWT válido y 401 sin token, y `OpenApiContractTests` en verde.
- **Terminado cuando:** `T002-09` está `Completada` con evidencia, las pruebas pasan y el JSON de respuesta es idéntico al anterior (compáralo en la prueba de integración con los nombres de propiedad exactos).

#### A2. Retirar los caminos de imagen sin moderar (Specs 003, 022 y 024; decisión §3.1)

- **Hoy:**
  - `UserEndpoints.cs`, línea ~73: `POST /api/v1/users/photo-signature` firma subidas directas a Cloudinary sin moderación (`T003-11` pedía límite de tasa).
  - `UpdateProfileCommandValidator.cs` acepta cualquier host que **termine en** `cloudinary.com` (por ejemplo `evilcloudinary.com` pasa) y de cualquier cuenta (`T003-09`).
  - La foto no se exige moderada (`T003-10`).
  - `FeedEndpoints.cs`, línea ~202: `mediaAssetId = body?.MediaAssetId ?? body?.MediaId` (alias heredado).
- **Hacer:**
  1. **Spec 003:** nuevo requisito "`fotoPerfilUrl` solo se acepta si coincide exactamente con la `SecureUrl` de un `MediaAsset` en estado `Approved` cuyo dueño es el usuario autenticado; `null` o vacío borra la foto". Validación en el **handler**, porque consulta el repositorio: agrega a `IMediaAssetRepository` el método `Task<bool> IsApprovedOwnedUrlAsync(Guid ownerId, string secureUrl, CancellationToken ct)`. Impleméntalo en Mongo con un índice declarado en `MongoDbContext` (`ownerId` + `secureUrl`), con su nombre y justificación en `plan.md`. Error `400 Profile.PhotoNotModerated` con mensaje genérico. Endurece también el validador de borde: `https`, host **exactamente** `res.cloudinary.com` y la ruta empieza con `/<CloudName>/`, leyendo el nombre de la cuenta de la configuración de Cloudinary ya existente.
  2. **Retiro de `photo-signature`:** el endpoint responde **410 Gone** con ProblemDetails (`code: Media.SignatureRetired`, mensaje "Usa POST /api/v1/media/upload"). Conserva la ruta, porque si desaparece un cliente viejo recibe un 404 confuso. Sigue exigiendo `RegisteredUserPolicy`. Borra `GeneratePhotoSignatureCommand`, su handler y su prueba **solo** si nada más los usa (búscalo con grep). Marca `.ExcludeFromDescription()` o documéntalo como `deprecated: true` en OpenAPI; elige lo que haga pasar `OpenApiContractTests`. `T003-11` (límite de tasa) se cierra como `Completada (superada por el retiro con 410, FR-003-xx)`.
  3. **Spec 024:** retira el alias `MediaId`. Si el cuerpo trae `mediaId` y no trae `mediaAssetId`, responde **400** `Feed.MediaIdRetired` ("Usa mediaAssetId"). Nunca descartes la imagen en silencio.
  4. Actualiza los OpenAPI de 003, 022 y 024.
- **Pruebas:**
  - Unitarias del handler de perfil: URL aprobada propia → OK; URL de otro usuario → 400; activo en cuarentena → 400; host `evilcloudinary.com` → 400 en el validador; otra cuenta de Cloudinary → 400; `null` → borra la foto.
  - Integración: `photo-signature` → 410 con JWT y 401 sin JWT; perfil con URL no moderada → 400; feed con `mediaId` → 400.
  - Testcontainers: `IsApprovedOwnedUrlAsync` contra Mongo real, verificando también que el índice existe.
- **Terminado cuando:** `T003-09`, `T003-10` y `T003-11` están cerradas con evidencia, junto con las tareas nuevas de 022 y 024, y todas las pruebas anteriores de perfil, feed y media siguen en verde.

#### A3. Limpieza móvil del retiro (rama `movil_Rodrigo`, sin commit)

- **Hacer:** quita `requestPhotoSignature` de `ProfileRepository` y su implementación, `generatePhotoSignature` de `ProfileApiService`, `CloudinarySignedUploader.kt`, `PhotoSignatureResponse` si queda sin uso, el parámetro `cloudinaryUploader` de `MediaRepositoryImpl` y las pruebas que solo cubren ese cargador en `PublishingApiTest.kt`. Antes, busca con grep todos los usos, incluidos los ViewModels y los fakes de prueba. Si alguna pantalla todavía usa la firma, **detente y pregunta**.
- **Verificar:** los comandos móviles de §2.6, todos en verde (cobertura ≥ 80 %, detekt sin hallazgos, 0 secretos).
- **Terminado cuando:** `grep -rn "photo-signature\|PhotoSignature\|CloudinarySignedUploader" app/src` no devuelve nada y Gradle sale `BUILD SUCCESSFUL`.

#### A4. Reintento de moderación ante fallo transitorio (Spec 017, `T017-03`)

- **Hoy:** `OpenAiTextModerationService` y `OpenAiImageModerationService` tienen timeout (15 s por defecto) pero **ningún reintento**. Un 429 o 5xx momentáneo manda contenido legítimo a cuarentena.
- **Hacer:** un solo reintento ante `HttpRequestException`, 429, 500, 502, 503 y 504, con espera configurable (`OpenAiSettings:Moderation:RetryDelayMilliseconds`, por defecto 500, máximo 2000) y número de reintentos configurable (`OpenAiSettings:Moderation:MaxRetries`, por defecto 1, máximo 2), ambos validados al arrancar. Respeta `Retry-After` si llega y es ≤ 2 s. **Presupuesto total**, que es el requisito no funcional: timeout × (1 + reintentos) + esperas ≤ 20 s, porque la Lambda corta a los 29 s. Si la configuración lo excede, la validación falla al arrancar. Después de agotar los reintentos, el comportamiento es **igual que hoy: fail-closed a cuarentena**. No uses Polly ni paquetes nuevos; un bucle simple basta.
- **Pruebas:** doble HTTP con 503 y luego 200 → aprobado con 2 llamadas; 503 y 503 → cuarentena; timeout → cuarentena; JSON malformado → cuarentena **sin** reintento; configuración que excede el presupuesto → falla la validación.
- **Terminado cuando:** `T017-03` está cerrada. `T017-04` y `T017-08` también se cierran si el bloque C de la ejecución anterior ya cubrió la integración en ofertas, publicaciones, comentarios y la moderación previa a Cloudinary; verifícalo con grep en los handlers y cita archivos y pruebas.

#### A5. Límite de tasa en API Gateway (G-10, control compensatorio; Spec 045)

- **Hoy:** el stage `$default` del HTTP API (`SensusInfraStack.cs`) no tiene límite de tasa. G-10 declara como control compensatorio el "*throttling* de API Gateway", pero no existe.
- **Hacer:** límite por defecto en el stage (`DefaultRouteSettings` con `ThrottlingRateLimit` y `ThrottlingBurstLimit`), con valores leídos del contexto de CDK (`apiThrottleRate`, por defecto 50; `apiThrottleBurst`, por defecto 100) y documentados en `plan.md` con su razonamiento: 2 usuarios de prueba y carga baja, muy por encima de lo esperado y por debajo de lo que vacía el Free Tier. No reemplaza los límites por usuario de la Spec 013.
- **Pruebas:** prueba de síntesis CDK (o una aserción sobre la plantilla, si `infra` tiene pruebas; si no, `dotnet build` de infra y una aserción con `Template.FromStack` en un proyecto de pruebas nuevo `infra/tests`, solo si es trivial; si no, documéntalo y verifícalo con `cdk synth` en el CI). Debe seguir pasando cdk-nag.
- **Terminado cuando:** G-10 cambia a `Mitigada en código el <fecha> (throttling de stage 50/100, Spec 045 FR-045-xx; WAF pendiente de aprobación de costo)`.

#### A6. Pruebas automatizadas de la pausa nocturna (Spec 051, casilla abierta del checklist)

- **Hoy:** `infra/assets/night-switch/index.py` no tiene pruebas. El checklist de 051 lo registra como observación de los principios V y VIII.
- **Hacer:** `infra/assets/night-switch/test_index.py` con `unittest` y `unittest.mock`, sin dependencias nuevas. Como el módulo crea clientes `boto3` al importarse, parchea `boto3.client` y las variables de entorno **antes** de importarlo. Casos:
  - `is_night`: 22:29 → falso; 22:30 → verdadero; 06:59 → verdadero; 07:00 → falso.
  - `reconcile` elige `stop` o `start` según la hora.
  - Una acción desconocida lanza `ValueError`.
  - `stop` pone la concurrencia en 0 y solo toca recursos con la etiqueta `sensus:night-switch=<env>`.
  - `start` restaura la concurrencia **al final**.
  - Ningún log contiene variables de entorno con IDs.

  Agrega un paso en `ci.yml` (job `iac`): `python -m unittest discover -s infra/assets/night-switch -p "test_*.py"`, con `actions/setup-python` fijado por SHA (obtenlo con `git ls-remote https://github.com/actions/setup-python refs/tags/v5`).
- **Terminado cuando:** las pruebas pasan en local con Python 3.12 o superior y la casilla de 051 queda marcada con evidencia.

#### A7. Pruebas de `FR-004-05` (Spec 004, `T004-11`)

- **Hoy:** el código existe (`LogoutCommand(string? DeviceId)`, validación de 1 a 100 caracteres y desactivación previa a la revocación), pero el checklist pide probar 5 casos y no hay pruebas de integración de `deviceId`.
- **Hacer:** pruebas de integración de los 5 casos:
  1. Sin `deviceId`: comportamiento de siempre.
  2. Dispositivo propio: se desactiva.
  3. Dispositivo de otra cuenta: no se toca y el logout responde 204.
  4. Dispositivo inexistente: 204.
  5. Identificador inválido (vacío, solo espacios o más de 100 caracteres): 400 **y la sesión sigue viva** (el mismo JWT todavía funciona).

  Verifica además que otros dispositivos del mismo usuario no se desactivan.
- **Terminado cuando:** `T004-11` y la casilla del checklist están cerradas con los nombres de las pruebas.

---

### BLOQUE B — Cierre documental con evidencia (~6 h)

**Método obligatorio para cada tarea o casilla abierta** (aplícalo una por una, sin cerrar en bloque):

1. Lee qué pide la tarea o casilla.
2. Busca la evidencia en el código: `grep -rn` del handler, endpoint, índice o prueba. Anota archivo y línea.
3. Si la evidencia es una prueba, **córrela** (`--filter`) y anota el conteo.
4. Si existe y pasa: marca `Completada (<archivo>; <Prueba> n/n)`.
5. Si no existe: es un hueco real. Si es pequeño (menos de 1 h), impleméntalo siguiendo §2.3. Si es grande, déjalo `Pendiente`, regístralo en "Hallazgos" y sigue.
6. Las casillas de **Definition of Ready** (spec aprobada, requisitos medibles, dependencias resueltas, alcance declarado, impacto evaluado) se marcan solo si el `spec.md` cumple lo que dicen. Revisa que cada FR tenga CA medible y que las dependencias citadas estén implementadas.
7. El **Constitution Check** se marca por principio con su evidencia. Para el Principio VIII cita: STRIDE en `plan.md` (G-11 cerrada) y escaneos bloqueantes en `ci.yml` (gitleaks, Snyk, SonarCloud, cdk-nag, SBOM CycloneDX y atestación), además de `dast-zap.yml`. Marca VIII como PASS **solo** si la última corrida de CI de `Back_Rodrigo` está en verde, consultada por la API pública (§2.2). Si no lo está, déjalo con la nota "pendiente de CI verde" y no lo marques.

#### B1. Specs 001, 002, 003, 005, 006 y 031 (casi todas sus tareas en `Pendiente`, con el código existente)

Tareas típicas y dónde buscar la evidencia:

- **"Actualizar y validar el contrato OpenAPI"**: `specs/NNN-*/contracts/openapi.yaml` contra los endpoints reales. Corre `OpenApiContractTests`.
- **"Mover o rotar el secreto JWT"** (`T001-05`, `T002-02`): `appsettings.json` tiene `"Secret": ""` y SSM lo provee (G-01 cerrada, Spec 045). Ciérralo citando ambos. La rotación del valor es una acción del usuario (5.Z).
- **"Índice único de correo contra MongoDB real"**: busca en `MongoDbContext.cs` y en las pruebas de Testcontainers de usuarios.
- **"Cuenta suspendida"** (`T002-04`): pruebas de login con sanción (Spec 020).
- **"Inmutabilidad de correo y roles"** (`T003-04`): `User.UpdateProfile` y sus pruebas.
- **"Retirar el endpoint legacy `/api/v1/comercio/*`"** (`T005-03`, `T006-02`): la Spec 008 ya los retiró. Grep en `src/Sensus.Api` sin resultados, más la prueba de 008. Ciérralo como `Completada (retirado por Spec 008, <prueba>)`.
- **"2dsphere con explain"** (`T005-05`): pruebas de Mongo con `explain` de comercios cercanos (Spec 053).
- **031 completa:** `MerchantEndpoints.cs` `MapPut("/profile")`, `UpdateMerchantProfileCommand`, recálculo de `location`/`geohash`, pruebas de comercio ajeno (403) y categoría y coordenadas inválidas (400). Si falta alguna prueba, escríbela.

#### B2. Specs 008, 013, 015, 016, 019, 022, 027 (DoR) y 034 (DoR)

Solo tienen casillas abiertas en el checklist (DoR, DoD y Constitution Check). Usa el mismo método. Específicos:

- **015:** métricas y alarmas en `SensusInfraStack.cs` (alarmas de errores, throttles y presupuesto).
- **016:** `/health/live` y `/health/ready`, con sus pruebas y con `post-deploy-smoke.mjs`.
- **019:** purga y liberación de cuarentena (`PurgeQuarantinedMediaCommandHandler`, `ReleaseQuarantinedMediaCommandHandler`).
- **022:** carga moderada (bloque C3 de la ejecución anterior).

#### B3. Principio VIII pendiente en 020, 026, 039 y 053

Las casillas dicen "faltan escaneos bloqueantes de CI". Ya existen (`ci.yml`, `dast-zap.yml`). Ciérralas según la regla 7 del método, solo con CI verde. La casilla de 020 menciona G-02 y G-04: G-04 está `Parcial` solo por el móvil, así que cita el estado exacto.

#### B4. Spec 017: `T017-04` y `T017-08`

Ver A4. `T017-08` decía "sigue abierta la cuarentena previa a Cloudinary": el bloque C3 lo resolvió (`UploadMediaCommandHandler` modera antes de `CloudinaryMediaStorage`). Ciérrala citando el archivo y `UploadMediaCommandHandlerTests`.

#### B5. Spec 034: `T034-07` ("Bloqueada por diseño de cuarentena")

El bloque C2 de la ejecución anterior implementó el estado `PendingModeration`, el `202` y el reintento. Verifica que `T034-07` quedó cubierta y ciérrala citando `ProcessPendingOfferModerationCommandHandlerTests`. Si queda algo distinto del texto de la tarea, es un hallazgo.

#### B6. Spec 028 (backlog)

No implementes nada. En `tasks.md` cambia los estados a `Pendiente (backlog, specs/README.md)`. No marques casillas.

---

### BLOQUE C — Runbooks de lo que depende del usuario (~1 h)

No ejecutes nada en vivo. Escribe en `docs/avance-cierre-specs-2026-11-26.md` un runbook **por cada** punto de 5.Z: comandos exactos, resultado esperado, qué tarea o casilla se cierra y con qué texto de evidencia. Así, cuando el usuario lo ejecute de día, Claude puede cerrar el papel en minutos.

---

### BLOQUE D — Verificación final y memoria (~1 h)

1. Verificación global de la sección 6.
2. Recuento final por spec. Debe quedar en cero, salvo lo de 5.Z y 028:
   ```bash
   for f in specs/[0-9]*/tasks.md; do grep -E "^\| *T[0-9]" "$f" | grep -E "\| *(Pendiente|Parcial)" ; done
   for f in specs/[0-9]*/checklist.md; do grep -H "\- \[ \]" "$f"; done
   ```
   Cada línea que quede debe tener su marca `(acción del usuario: …)` o `(backlog)`.
3. `memory.md`: versión PATCH siguiente, historial, "Última tarea completada", "Próximos pasos" y estado real de G-xx.
4. `specs/README.md`: estado de cada spec coherente con su `tasks.md`.

---

### 5.Z Acciones del usuario (el ejecutor no las hace; escribe sus runbooks)

| # | Acción | Cierra |
|---|---|---|
| 1 | PR `Back_Rodrigo` → `develop` → `main` y despliegue aprobado en el environment `production` | Verificaciones posteriores al despliegue de 045, 047 (cdk-nag, SBOM y atestación en CI), 048 y 051 (`T051-09`, `T051-10`) |
| 2 | Crear el environment `production` en GitHub y cargar `AWS_DEPLOY_ROLE_ARN` y `AWS_IAC_ROLE_ARN` con las salidas del stack; después, borrar `AWS_ACCESS_KEY_ID` y `AWS_SECRET_ACCESS_KEY` | `T045-20`, `T045-21` |
| 3 | SSM `/sensus/prod/SesEmailSettings/ConfigurationSetName` = `sensus-transactional-prod` después del despliegue | `T029-24` |
| 4 | Esperar la salida del sandbox de SES y probar la recuperación de contraseña desde la app | `T029-23` |
| 5 | Rotar el secreto JWT en SSM (nuevo valor aleatorio de 64 bytes; cierra sesión a todos) | Evidencia de rotación de 001 y 002 |
| 6 | Cargar `/sensus/prod/push/fcm-service-account` (JSON de Firebase) y probar push en los 2 celulares | `T027-07`, `T027-12`, `T027-13`, casillas de 033, `T044-13` |
| 7 | Invocar a mano la pausa nocturna (`stop` y `start`) de día y verificar CA-051-01 y CA-051-02 | `T051-08` |
| 8 | Un turno real de Sensi y revisar el contador en DynamoDB | `T049-10` y casillas de 049 |
| 9 | Validar amigos en vivo después del despliegue | `T050-07` y la casilla de 050 |
| 10 | Correr `dast-zap.yml` de día y `load-smoke.mjs` (5 rps, sin `/ai`) | G-02 (ejecución), G-13 (p95), casillas de 042 |
| 11 | `cdk diff` con credenciales para 049 y 055 | Casillas de 049 y 055 |
| 12 | Dar de alta el TOTP de administrador | Verificación en vivo de 062 |
| 13 | Aviso de privacidad en la landing y FT-23 (fuera de este repo) | Casillas de 042 y 055 |

---

## 6. Verificación global al cerrar cada bloque

```powershell
dotnet build sensus-back.sln -warnaserror
dotnet format sensus-back.sln --verify-no-changes
dotnet test tests/Sensus.Application.UnitTests --collect:"XPlat Code Coverage"
dotnet test tests/Sensus.ArchitectureTests
dotnet test tests/Sensus.E2ETests
dotnet test tests/Sensus.IntegrationTests
node .github/scripts/check-specs.mjs specs
node --test .github/scripts/load-smoke.test.mjs .github/scripts/run-ai-evals.test.mjs .github/scripts/post-deploy-smoke.test.mjs
node .github/scripts/run-ai-evals.mjs --mode deterministic --output-dir $env:TEMP\evals-det
python -m unittest discover -s infra/assets/night-switch -p "test_*.py"
dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror
git status --short
```

- Cobertura ≥ 80 % en Domain y Application con `node .github/scripts/check-coverage.mjs <dir> 80 Sensus.Domain Sensus.Application`.
- Escaneo de secretos antes de entregar. Si Docker funciona:
  `docker run --rm -v "C:/Users/Rodrigo/source/repos/sensus-back:/repo" zricethezav/gitleaks:v8.24.3 dir /repo --redact`
  Debe decir `no leaks found`. Si encuentra un dato de prueba, cámbialo; no agregues reglas de exclusión.
- Si tocaste el móvil (A3), corre sus comandos de §2.6.

---

## 7. Reporte (en `docs/avance-cierre-specs-2026-11-26.md`, en español)

Al inicio, un **RESUMEN EJECUTIVO** de 15 líneas como máximo. Después:

1. Tabla por punto: `Punto | Estado (Hecho / Parcial / Bloqueado) | Evidencia (pruebas y conteos) | Qué falta`.
2. Tabla por spec: `Spec | Tareas cerradas / total | Casillas cerradas / total | Pendiente y por qué`.
3. Verificación global con números exactos.
4. **Hallazgos:** huecos reales encontrados en B que no cerraste, con archivo y propuesta.
5. Runbooks de 5.Z.
6. Decisiones o riesgos que requieren respuesta del usuario, cada uno como pregunta concreta.

Nunca digas "verificado en producción" sin haberlo comprobado. Nunca reportes conteos de pruebas que no corriste. Nunca marques una casilla sin evidencia.
