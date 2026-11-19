# Memoria del Proyecto — Sensus Backend

**Version:** 3.4.0 · **Actualizado:** 2026-11-19 · **Alineado a:** `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0

> Estado vivo del proyecto para reanudar trabajo sin perder contexto. No es normativo: si contradice a
> `constitution.md`, prevalece la constitución y este archivo se corrige.

---

## A. Estado de sesión

| Campo | Valor |
|---|---|
| **Última actualización** | 2026-11-19 |
| **Fase SDD** | En `main` (`01dad6e`): `implement` completado para specs 005, 007, 008, 015, 016, 018, 019, 022, 027, 031, 032, 033 y 044 (029 y 048 en `Parcial`). En trabajo local: `specify → plan → tasks → checklist → implement` completadas para **Spec 047 v1.2.0** (CI-CD y DevSecOps al 100 %, cerradas `T047-14` a `T047-16`), **Spec 046 v2.0.0** (Sensi IA agéntico de 7 capas, tareas `T046-01` a `T046-22` cerradas y parciales según alcance), resolución integral de los 14 ítems de revisión de código y prerrequisitos de infraestructura y datos (Items 0 a 8) |
| **Rama de trabajo** | `Back_Rodrigo` (cambios locales sin confirmar: Spec 046, Spec 047 y prerrequisitos) |
| **Agente** | Antigravity |
| **Constitución vigente** | v2.4.0 (2026-11-19) |
| **AGENTS.md vigente** | v2.4.0 |
| **Solución .NET** | `sensus-back.sln` (9 proyectos: `src` + TestKit, UnitTests, IntegrationTests, E2ETests, ArchitectureTests) |
| **Última tarea completada** | **Secretos en SSM (Spec 045 FR-045-11/12, brecha G-01 cerrada en código)**: la API lee `/sensus/prod/*` al arrancar (fail-closed), CDK y el workflow ya no inyectan secretos, el correo de alertas llega por `ALERTS_EMAIL`, y se eliminó el secreto JWT derivable del código en Lambda (vulnerabilidad crítica). Pendiente: cargar los 8 parámetros y crear la variable. Antes: **Redis activo en IaC y pausa nocturna (Spec 048 FR-048-14, Spec 051, ADR 0014, constitución v2.4.0)**: VPC sin NAT fijo, ElastiCache Serverless Valkey con TLS, Lambda en subredes privadas, función `sensus-night-switch` con EventBridge Scheduler de 22:30 a 07:00 (`America/Mexico_City`); `cdk diff` revisado sin reemplazos; pendiente desplegar por CI (nunca `cdk deploy` local). Antes: **Caché activa en producción sin costo (Spec 048, FR-048-13)**: `MemoryCacheService` en memoria de proceso cuando Redis está apagado (TTL máx. 60 s, 1000 entradas, JSON inmutable) y `CacheHealthCheck` en `/health/ready`; Redis sigue apagado en Lambda; 1018 pruebas en verde. Antes: **Spec 020 (sanciones y baneo) implementada** y **constitución v2.3.0** (Principio VIII, DevSecOps de nivel fintech en backend y móvil, con Apéndice F de brechas G-01 a G-13). Spec 020: entidad `Sanction` de solo adición, suspensión denormalizada en `User`, escalamiento 24 h, 7 d, 30 d, permanente en `Moderation:Sanctions`, endpoints `/api/v1/moderation/sanctions` solo para `Administrador`, bloqueo en login (403 `Auth.UserSanctioned`, tras validar la contraseña) y en cada token, alerta push al aplicar y revertir, métricas EMF; 711 pruebas unitarias, 283 de integración y 12 contra MongoDB real. Antes: Spec 047 (CI-CD y calidad DevSecOps completada al 100 %, elevando del 71 % al 100 %): 1) Runner de Evals sanitizado `.github/scripts/run-ai-evals.mjs` (FR-047-13, FR-047-15, NFR-047-06) con validación determinista al 100 %, rúbricas de modelo y 0 PII/secretos; 2) Workflow `.github/workflows/ai-evals.yml` (FR-047-13, FR-047-15) ejecutable vía `workflow_dispatch` y `schedule` con publicación de reporte; 3) Puerta bloqueante en `ci.yml` para PRs (T047-16, FR-047-14); 4) Candado en `deploy-serverless.yml` previo a `cdk deploy` exigiendo Evals en verde (T047-16, FR-047-14); 5) Trazabilidad SDD actualizada en specs/047, requirements.md y specs/README.md. Antes: FR-046-18 (offersCited enriquecidas) y Spec 049 (presupuesto DynamoDB) |
| **Restricción activa** | Operaciones de Git requieren confirmación explícita del responsable (`AGENTS.md` §12). «No subas cambios de nada» confirmado |

---

## B. Principios y estado real

| Principio | Estado en el repositorio | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | Implementado | 49 specs en `specs/README.md` (48 activas, 1 en backlog) con `spec/plan/tasks/checklist` y `specs/requirements.md`; los 29 endpoints del catálogo están cubiertos |
| II. Clean Architecture | Parcial | Capas existen; falta verificar dependencias con NetArchTest de forma continua |
| III. CQRS y contratos | Parcial | Hay comandos/handlers y DTOs; faltan OpenAPI unificado y ProblemDetails uniforme (specs 009, 011) |
| IV. Seguridad y privacidad | Parcial, con brechas | Secreto JWT versionado, CORS abierto, `RequireHttpsMetadata=false`, sin logout (specs 004, 012) |
| V. Verificación automatizada | Implementado | 747 pruebas automatizadas en verde (549 unitarias, 191 integración con Testcontainers/MongoDB real y dobles HTTP OpenAI, 4 arquitectura, 3 E2E). Cobertura > 80 % en Domain y Application. Compilación con `-warnaserror` y formato verificado |
| VI. Costo cero e IaC | Implementado | Proyecto AWS CDK en C# en `infra/` (`SensusInfraStack`, Spec 045): Lambda `.NET 10` (`PROVIDED_AL2023`, `ARM_64`), API Gateway HTTP API v2, CloudWatch (7 días retención + alarmas), SSM Parameter Store (0 USD) y presupuesto en AWS Budgets (1.00 USD) |
| VII. Rendimiento y observabilidad | Implementado | Paginación estándar, `CorrelationIdMiddleware` con propagación `X-Correlation-Id` en logs y `ProblemDetails`, rate limiting configurable por IP/usuario, métricas EMF CloudWatch y caché distribuida Redis con operaciones atómicas |

Estados posibles: **Sin implementar** · **Parcial** · **Implementado** · **Verificado**.

---

## C. Decisiones tomadas

| ID | Decisión | Fecha | Consecuencia |
|---|---|---|---|
| D1 | `arquitecturaNva.docx` es la fuente autoritativa; `Arquitectura Back.docx` queda derogado (incluido el *Load Balancer*) | 2026-10-28 | Toda decisión de arquitectura se contrasta con `arquitecturaNva` |
| D2 | Stack fijado: .NET 10 LTS, C# 14, MongoDB 8.x | 2026-10-28 | Prohibido usar «última versión» como criterio |
| D3 | Toolchain: xUnit, FluentAssertions, NSubstitute, Testcontainers, NetArchTest, AWS CDK en C#, GitHub Actions | 2026-10-28 | Pipeline y pruebas se construyen sobre estas herramientas |
| D4 | Umbrales: cobertura ≥ 80 % en Domain/Application; paginación 20/100; p95 y moderación como `NFR` | 2026-10-28 | Los umbrales se fijan en la spec, no en código |
| D22 | Despliegue en EC2 con Nginx, Let's Encrypt SSL (`18-117-247-171.sslip.io`) y CD en GitHub Actions | 2026-11-04 | Despliegue productivo y accesible sin tocar DNS de dominios privados (`rodnix`), 100 % automatizado vía `.github/workflows/deploy.yml` y con certificados TLS gratuitos auto-renovables |
| D5 | Documentos de gobierno (`constitution.md`, `AGENTS.md`, `memory.md`) viven en la raíz del repositorio | 2026-10-29 | Se movieron desde el directorio de trabajo original; no hay copias duplicadas |
| D6 | Documentación en español; código e identificadores en inglés | 2026-10-28 | Aplica a specs, ADR, commits y comentarios |
| D7 | Cada spec tiene ID numérico de tres dígitos, carpeta `NNN-slug` y cuatro artefactos obligatorios: `spec.md`, `plan.md`, `tasks.md`, `checklist.md` | 2026-10-29 | Sin los cuatro artefactos la spec está incompleta y no se implementa |
| D8 | Los requisitos usan `FR-<spec>-<nn>` y `NFR-<spec>-<nn>` | 2026-10-29 | Evita colisiones entre specs; el prefijo coincide con el ID de la carpeta |
| D9 | Backlog aprobado de 28 specs en cinco olas | 2026-10-29 | Sustituido por D12: 44 specs en ocho olas alineadas al catálogo |
| D10 | Constitución enmendada a v1.1.0 (MINOR): nuevo artefacto, nuevo formato de ID y fase `checklist` | 2026-10-29 | Vigente hasta la enmienda v1.2.0 (D12–D15), que la sustituye en el mismo día |
| D11 | Prohibidas las operaciones de Git (`add`, `commit`, `push`, ramas, merges) hasta autorización explícita | 2026-10-29 | El trabajo permanece como archivos locales sin confirmar |
| D12 | El catálogo `Catalogo_Endpoints_AWS.xlsx` (29 endpoints, 8 módulos) es la fuente de rutas; el backlog se realinea a él | 2026-10-29 | 29/29 endpoints mapeados a 27 specs; 12 specs reescritas y 16 nuevas (029–044) |
| D13 | Se mantiene OpenAI `omni-moderation-latest`; se ignora Sightengine del catálogo | 2026-10-29 | La spec 017 conserva el contrato de moderación de §2.4 |
| D14 | Push mediante `IPushDispatcher` con adaptadores FCM o SNS por configuración | 2026-10-29 | ADR 0007; §2.2 admite la excepción acotada |
| D15 | Rutas `/api/v1` con recursos en inglés y acciones como subrecurso | 2026-10-29 | ADR 0008; `/comercios` se renombra a `/merchants` y se retira en la spec 008 |
| D16 | IDs estables: se reescriben 001–028 y se añaden 029–044; 028 queda en backlog | 2026-10-29 | Sin renumeración; 43 activas + 1 backlog |
| D17 | Solo se añaden capacidades fuera del catálogo cuando son obligatorias: logout, `GET` de perfil, `GET /merchants/me`, health checks | 2026-10-29 | Además se cierran dos huecos funcionales: confirmación de recuperación y canje del QR |
| D18 | Las coordenadas del comercio las aporta el cliente y se validan en el servidor; sin proveedor de geocodificación | 2026-10-29 | Elimina una dependencia externa y cumple §2.2 |
| D19 | Ranking determinista en `Sensus.Application`, sin servicio de ML externo | 2026-10-29 | ADR 0009; specs 041–043 con métricas medibles |
| D20 | El repositorio canónico pasa a `sensus-proj/sensus-back`; `Elrorro334/sensus-back` queda como remote `legacy` de solo lectura. El SDD se integra por rama `SprintOscar` y PR a `Sprint1` | 2026-11-04 | `origin` apunta a la organización; `Sprint1` y `main` de la organización no se tocan sin PR |
| D23 | Adopción de Cloudinary como servicio oficial de almacenamiento, transformación y entrega CDN de multimedia | 2026-11-08 | ADR 0010; enmienda constitucional v1.3.0 (§2.1, §2.2, P-VI). Sustituye Amazon S3 y CloudFront; valida la implementación existente de Sprint 1 |
| D24 | Implementación de Caché Distribuido con Redis (Spec 048) | 2026-11-08 | `ICacheService` en Application, `RedisCacheService` en Infrastructure con `StackExchange.Redis`, fallback graceful a persistencia, TTL por entidad en `appsettings.json` e invalidación integrada en perfil de usuario y comercio. 201 pruebas en verde |
| D25 | Implementación de Infraestructura Serverless como Código con AWS CDK en C# (Spec 045) | 2026-11-08 | Hospedaje dual Kestrel/Lambda en `Sensus.Api` (`AddAWSLambdaHosting(LambdaEventSource.HttpApi)`) y stack `SensusInfraStack` en `infra/` con AWS Lambda (`PROVIDED_AL2023`, `ARM_64`), API Gateway HTTP API v2, CloudWatch (retención 7 días + alarmas 5xx/errores), SSM Parameter Store (0 USD) y presupuesto en AWS Budgets (1.00 USD) |
| D26 | Despliegue en AWS Serverless (API Gateway HTTP API v2 + AWS Lambda .NET 10 en `us-east-2`) | 2026-11-09 | URL base oficial en producción: `https://zqqdwovx81.execute-api.us-east-2.amazonaws.com/`. Validado con endpoints `/health` (200 OK) y ProblemDetails RFC 7231. Sustituye la IP provisional de EC2 (`18-117-247-171.sslip.io`) y se sincroniza en `NetworkClient.kt` del cliente móvil. |
| D27 | Equivalencia MongoDB/Clean Architecture para SE-022 a SE-026 (Expiración automática y republicación de ofertas) | 2026-11-12 | Implementado en rama `sprint3_oscar`: Entidad `Offer`, control automático de vigencia en MongoDB sin triggers SQL, índice compuesto `{ status: 1, endsAt: 1 }`, actualización por lotes `ExpireOffersCommand`, republicación rápida `RepublishOfferCommand` y servicio en segundo plano `OfferExpirationBackgroundService`. |
| D28 | Asistente conversacional Sensi por vía generativa con enmienda (resuelve D-6 del móvil): ADR 0011, `constitution.md` v1.4.0 (cuarta excepción de proveedor externo, §2.1/§2.2/§2.4/§2.5) y Spec 046 | 2026-11-13 | Recuperación de candidatos y cálculo del paquete deterministas en backend; el proveedor (OpenAI, ya autorizado para moderación y usado con `gpt-4o-mini` en la versión 2.10.0) solo redacta y selecciona dentro de un conjunto cerrado; moderación de entrada y salida fail-closed; degradación determinista; excepción `EX-001` a P-VI con techo 1.00 USD/mes. ADR 0009 sigue vigente para 041–043. |
| D29 | Evolución de Sensi IA a arquitectura agéntica de 7 capas (enmienda `constitution.md` v2.0.0, reclasificada de MINOR a MAJOR, [ADR 0012](docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md), Spec 046 v2.0.0 y Spec 047 v1.1.0) | 2026-11-15 | Sustituye ADR 0011 por 7 capas: (1) identidad Sensus extraída de Drive + *Hard Rules*; (2) RAG estático en Markdown versionado dentro de `<contexto>`; (3) MCP *in-process* y *Function Calling* de solo lectura (5 *tools*) dentro de la Lambda actual (`0 USD` AWS) con validación/recálculo determinista en backend; (4) herencia estricta de *scopes* desde JWT (`Consumidor`/`Comerciante` activos, `Invitado` `403`) y CRUD completo incluyendo `DELETE /api/v1/ai/conversations/{id}`; (5) *guardrails* duales (`omni-moderation-latest` + anti-*Prompt Injection*/*Jailbreak* + redacción PII/credenciales); (6) *Prompt Caching* en 2 bloques (`≥ 1,024` tokens estáticos) + *Model Router* de 2 modelos con afinidad *sticky* bajo `EX-001` (`5.00 USD/mes` desde el 2026-11-15); (7) CloudWatch solo metadatos/hashes sin PII, MongoDB `ai_trace_logs` redactado con TTL/purga y gate bloqueante de *Evals* en CI/CD (Spec 047). |

---

## D. Artefactos del proyecto

| Artefacto | Ruta | Estado |
|---|---|---|
| Constitución | `constitution.md` | Completa, v1.4.0 |
| Reglas para agentes | `AGENTS.md` | Completo, v1.3.0 |
| Memoria | `memory.md` | Este archivo, v3.0.2 |
| Plantillas SDD | `specs/_templates/{spec,plan,tasks,checklist}.md` | Completas, alineadas a v1.2.0 |
| Índice de specs | `specs/README.md` | Completo, 48 specs |
| Registro de requisitos | `specs/requirements.md` | Completo |
| Especificaciones | `specs/001-…` … `specs/048-…` | 48 carpetas con 4 artefactos cada una |
| Contratos por spec | `specs/NNN-slug/contracts/openapi.yaml` | Solo en specs con endpoints |
| Arquitectura | `docs/architecture/arquitecturaNva.docx` y `docs/architecture/overview.md` | Completos |
| ADR | `docs/adr/0001` … `0011` + índice | Completos |
| Solución .NET | `sensus-back.sln` | Existe; `sensus-back.slnx` retirado |
| Infraestructura | `infra/` (CDK) | Implementada (`SensusInfraStack`) |
| Pipeline CI | `.github/workflows/ci.yml` + scripts de validación | Escrito |
| Archivos raíz | `.editorconfig`, `Directory.Build.props`, `global.json`, `docker-compose.yml`, `.env.example`, `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md` | Completos |

---

## E. Hallazgos y riesgos abiertos

- **Secretos JWT versionados (bloqueante)**: `appsettings.Development.json` contiene
  `Sensus_Dev_Environment_Rotated_Secret_Key_Minimum_32_Chars_2026!` y `src/Sensus.Api/Program.cs` define
  una clave de reemplazo hardcodeada (`Sensus_Fallback_Key_For_Early_Initialization_...`) cuando
  `JwtSettings:Secret` está vacío. El valor histórico
  `SensusSuperSecretKey2026_Minimum32CharactersLong_Enterprise!` sigue en el historial de ambos repos.
  **Acción**: rotar la clave, retirar el fallback de `Program.cs`, mover el secreto de desarrollo a
  *user-secrets* y purgar el historial. No se tocó en esta rama para no alterar el despliegue del equipo
  (specs 001 y 002). `JwtSettingsValidator` ya rechaza los valores filtrados conocidos.
- **CORS `AllowAnyOrigin`** y `RequireHttpsMetadata = false` en `src/Sensus.Api/Program.cs` (spec 012).
- **Cobertura real por debajo del umbral**: el último cálculo local dio Domain 25.89 % y Application
  34.78 % frente al 80 % exigido. El gate de CI fallará hasta remediarlo; no se ha vuelto a medir tras
  integrar el código de Sprint 1.
- **El código de Sprint 1 no está especificado**: 67ccc4e7, 10976027 y 594e20be añaden recuperación de
  contraseña, modo invitado, eliminación de cuenta, perfil de comercio y Cloudinary sin ajustarse al flujo
  `specify → clarify → plan → tasks → checklist`. Trazabilidad rota hasta regularizarlo.
- **Endpoints legacy duplicados** `/api/v1/comercio/registro` y `/api/v1/comercio/mi-comercio`, más el
  alias `/api/v1/comercios` (spec 008).
- **`PagedResult` sin uso**: no hay paginación implementada (spec 010).
- **Huecos del catálogo cerrados por decisión**: canje del QR (`POST /offers/claims/{code}/redeem`, spec
  033), confirmación de recuperación (`POST /auth/recover/confirm`, spec 029), `GET` de perfil (003),
  `GET /merchants/me` (006), `GET /users/settings` (044) y health checks (016).
- **Sin administración de moderación en el catálogo**: la spec 021 mantiene el endpoint de auditoría como
  capacidad interna, fuera del catálogo público.
- **Límite de subida**: API Gateway corta en 10 MB y Lambda síncrona en 6 MB; la spec 022 fija 4,5 MB y
  responde 413 por encima.
- **Sin IaC ni presupuesto**: el costo 0 USD no es verificable todavía.
- **Sin OpenAPI unificado**: coexisten Swashbuckle y `Microsoft.AspNetCore.OpenApi` (spec 011).
- **Pruebas existentes**: 97 unitarias, 4 de arquitectura, 31 de integración y 3 E2E en verde tras la
  integración. Los paquetes de EF Core sin uso ya se retiraron de `Sensus.Infrastructure`.

---

- **Moderación fail-open sin clave (RESUELTO 2026-11-15, Item 0)**: `OpenAiImageModerationService` corregido para enviar a `Quarantine` ante falta de API key, error HTTP o timeout, cumpliendo `constitution.md` §2.4 y `AGENTS.md` §12. Verificado con pruebas de regresión.
- **Prerrequisitos de código para Spec 046 (RESUELTO 2026-11-15)**:
  - Item 1: Puerto `ITextModerationService` y servicio `OpenAiTextModerationService` implementados (`omni-moderation-latest`, fail-closed, dobles de prueba para timeout/fallo/malformada).
  - Item 2: Opciones `SensiSettings` tipadas y validadas al arrancar (`TotalBudgetCapUsd: 5.00`, `ReasoningBudgetCapUsd: 3.00`).
  - Item 3: Decisión de almacenamiento de API key documentada (Secrets Manager: ≈0.40 USD/mes vs SSM SecureString gratuito con custom resource vs passthrough Lambda).
  - Item 4: `CorrelationIdMiddleware` e `ICorrelationIdAccessor` implementados, inyectando `X-Correlation-Id` en logs y `ProblemDetails`.
  - Item 5: Rate limiting configurable por usuario y por IP con `RateLimitingSettings`, política `ai-chat` y cabecera `Retry-After`.
  - Item 6: `IOfferRepository.SearchAsync` con índice de texto en MongoDB (`title`, `description`).
  - Item 7: `IOfferClaimRepository.GetByUserIdAsync` con índice compuesto `(UserId, CreatedAtUtc)`.
  - Item 8: `IOfferRepository.GetByMerchantIdPagedAsync` para `get_my_merchant_offers`.
- **Auditoría documental 2026-11-15** (specs 004, 012, 013, 017, 030, 034–037, 047): las brechas
  quedaron registradas en cada `checklist.md`. Las más relevantes:
  - **030 (corregido 2026-11-15)**: al eliminar una cuenta comercial ahora se archivan sus ofertas
    (FR-030-06). Sigue abierto T030-09: los apartados activos de esas ofertas no se resuelven.
  - **034/035/036**: el texto de las ofertas no se modera, `ImageUrl` no se valida contra un MediaAsset
    aprobado, editar no vuelve a moderar y republicar desde Archived no pasa por moderación.
  - **017**: existe el endpoint público `POST /api/v1/media/moderate` (contrario a FR-017-07) y ante
    fallo o timeout la imagen va a cuarentena (corregido en Item 0).
  - **037**: `GET /merchants/my-offers` sin métricas de apartados, canjes y vistas (paginación agregada en repo).
  - **013**: umbrales de rate limiting fijos en código (configuración tipada añadida para Sensi en `RateLimitingSettings`).
  - **004**: la revocación usa SecurityStamp, no la lista de jti que describen spec y plan.
  - **036**: `OfferExpirationBackgroundService` no corre de forma continua en Lambda; confirmar el
    disparo de `/expire-check`.
  - Las specs 034–037 no tienen pruebas de integración de endpoint y sus contratos OpenAPI siguen en
    borrador.

## F. Próximos pasos

1. Revisar y fusionar `SprintOscar` → `Sprint1` mediante PR en `sensus-proj/sensus-back`.
2. Regularizar el código de Sprint 1 contra el SDD: specs retroactivas para recuperación de contraseña
   (029), modo invitado, eliminación de cuenta (030), perfil de comercio (031) y almacenamiento de imagen
   de perfil (022/023).
3. Ejecutar la ola 0: 009–011 (errores, paginación y OpenAPI) antes de tocar endpoints.
4. Rotar el secreto JWT y purgar el historial; hasta entonces el escaneo de secretos de CI puede fallar.
5. Medir cobertura real y elevarla al 80 % en Domain y Application.
6. Ejecutar la ola 1 (001–003, 029, 030) y después la ola 2 (005–007, 031–037).
7. Renombrar las rutas implementadas a `/api/v1/merchants` y `/api/v1/users/profile`; retirar legacy en la
   ola 6 (spec 008).
8. Inicializar `infra/` con CDK y la alarma de presupuesto; activar el job `iac` del pipeline.

---

## G. Excepciones registradas

| ID | Principio | Justificación | Expira | Estado |
|---|---|---|---|---|
| EX-001 | VI | Costo variable del proveedor generativo de la Spec 046; sin alternativa generativa gratuita dentro de AWS. Acotada por techo 5.00 USD/mes (elevado desde 1.00 USD el 2026-11-15), *Prompt Caching* en 2 bloques, *Model Router* con afinidad *sticky*, límite de tokens/mensajes diarios por persona, caché y alarma (`NFR-046-02`). Responsable por asignar | Al cierre de Spec 046 o al agotar el techo | Vigente |
| EX-002 | VI | Costo de la moderación de contenido publicado con `gpt-4o-mini` (Spec 017), elegido porque `omni-moderation-latest` dejaba pasar imágenes con armas. ≈ 0.00013 USD por imagen; techo 1.00 USD/mes con alarma al 80 % y cuarentena al agotarse. Se recomienda un proyecto de OpenAI separado del de Sensi | Al cierre de Spec 017 o al agotar el techo | Vigente |

> Los `FAIL` de los retro-specs 001, 002, 003, 005, 006 y de las specs 012, 015 y 023 no son excepciones:
> son deuda técnica registrada con tareas de remediación en cada `tasks.md`.

---

## H. Historial de actualizaciones

| Versión | Fecha | Cambio |
|---|---|---|
| 3.3.0 | 2026-11-17 | Conclusión de CI-CD y calidad DevSecOps (Spec 047 al 100 %, elevando el avance del 71 % al 100 %): implementación de tareas T047-14 a T047-16. Se creó el runner de Evals sanitizado `.github/scripts/run-ai-evals.mjs` (FR-047-13, FR-047-15, NFR-047-06), el workflow `.github/workflows/ai-evals.yml` para ejecución manual y programada previa a release con artefacto de reporte libre de PII y secretos, la puerta bloqueante determinista en `.github/workflows/ci.yml` para PRs/pushes, y el candado de validación de Evals en `.github/workflows/deploy-serverless.yml` previo al despliegue con AWS CDK. Actualizados spec.md, plan.md, tasks.md, checklist.md, requirements.md y specs/README.md. Verificado: `check-specs.mjs` 50/50 specs conformes y runner de evals en PASS. Sin commits (AGENTS.md §12). |
| 3.2.9 | 2026-11-16 | SonarCloud del PR 19 en rojo: *Security Rating* B por 20 vulnerabilidades `S6444` (regex sin *timeout*, riesgo ReDoS) y 77.8 % de cobertura en código nuevo (líneas + condiciones; Sonar sí cuenta Infrastructure e integración). Corregido: `GuardrailRegex.Timeout` (1 s) en detector de inyección, detector de fugas, redactor PII y router, y en la extracción de presupuesto del handler; cada uno falla cerrado ante `RegexMatchTimeoutException` (inyección → sospechoso, fuga → respuesta segura, redacción → `[CONTENIDO_REDACTADO]`, router → nivel rápido, presupuesto → sin paquete). Con 100 ms se bloqueaban entradas benignas bajo carga. Nuevas pruebas `OpenAiChatGatewayTests` (gateway de 0 % a cubierto) e `InProcessMcpToolExecutorTests` (21). Estimación local de cobertura Sonar ≈ 90 %. Sin commits. |
| 3.2.8 | 2026-11-16 | CI del PR 19 en rojo por cobertura (`check-coverage.mjs` con `FullyQualifiedName!~Integration`): Domain 77.35 % y Application 71.15 %, umbral 80 %. Causa: el código de Sensi IA solo tenía pruebas de integración, que no cuentan en ese job. Agregadas 64 pruebas unitarias (`SendAiMessageCommandHandlerTests`, `AiConversationHandlersTests`, `AiMessageAndTraceLogTests`, `GetConversationQueryValidatorTests`) sin bajar el umbral: Domain 89.66 % y Application 95.31 %. Nuevo `GetConversationQueryValidator` (`pageSize` 1–100, AGENTS.md §9). Hallazgo abierto: `forceNew = true` no archiva la conversación activa previa. Sin commits. |
| 3.2.7 | 2026-11-15 | Validación de la resolución de los 14 ítems (3.2.6). Corregido defecto introducido en el punto 14: `catch (OperationCanceledException)` en `SendAiMessageCommandHandler` también capturaba el *timeout* de `HttpClient` (`TaskCanceledException`) y lo propagaba como 500; ahora se filtra con `when (cancellationToken.IsCancellationRequested)` y el *timeout* cae al fallback determinista (regresión `SendMessage_ShouldDegradeToFallback_WhenChatGatewayTimesOut`). `T046-17` y `FR-046-14` pasan a Parcial: Sensi aún no emite métricas EMF. `NFR-046-01`, `NFR-046-03` y `NFR-046-05` pasan a Parcial: el mecanismo existe, pero p95 < 8 s, ≥ 70 % de tokens cacheados y ≥ 95 % de bloqueo de inyecciones solo se miden con prueba de carga y *Evals* con modelo. Sigue pendiente la clave OpenAI desde SSM (`FR-046-16`). Verificado: 748 pruebas en verde (549 unitarias, 192 integración, 4 arquitectura, 3 E2E), 0 advertencias, formato limpio y 48 specs conformes. Sin commits. |
| 3.2.6 | 2026-11-15 | Resolución integral de los 14 ítems de revisión de código de **Sensi IA y backend**: 1) `SensiSettings` movido a Application (`Common/Settings`) e inyectado con `IOptions<SensiSettings>` eliminando números mágicos (`MaxInputChars`, `MaxTurnsHistory`, `MaxAgenticRounds`, costos estimados y retención de trazas); 2) `ScreenContext` acotado a whitelist de pantallas conocidas (`KnownScreenContexts`) en Domain/Application con validación FluentValidation y protección anti-inyección; 3) Purga en cascada de `ai_conversations`, `ai_messages` y `ai_trace_logs` al eliminar cuenta en `DeleteAccountCommandHandler` (`FR-046-15`) con prueba de regresión E2E; 4) Aplicación real y atómica del presupuesto (`EX-001`) en `AiBudgetGuard` con Redis `INCRBYFLOAT`/`INCR`, comportamiento fail-closed ante caída de Redis y sub-techo de 3.00 USD para reasoning; 5) Umbrales de moderación de texto externalizados a `OpenAiSettings:Sensi:TextModerationThresholds`; 6) Base de conocimiento sin fallback silencioso, como `EmbeddedResource` y excepción en arranque fuera de Testing; 7) `AiConversation.Create` rechaza rol `Invitado`; 8) `RecommendedPackage` rechaza ofertas duplicadas; 9) Entrega de ofertas citadas y paquete recomendado al cliente en `AiTurnResponseDto`; 10) `get_my_account` envía únicamente el primer nombre sin apellidos para evitar fuga de PII; 11) `T046-15` marcado como Parcial por resolución de clave SSM SecureString en CDK; 12) Corrección de cifras de microempresas (INEGI 95.4 % y 4.4 % venta en línea) en guía Markdown; 13) Bucle agéntico agotado degrada a respuesta determinista; 14) Re-lanzamiento estricto de `OperationCanceledException`. Trazabilidad SDD actualizada en specs/046, specs/013, specs/014, specs/017, specs/requirements.md y specs/README.md. Verificado: 747 pruebas en verde (549 unitarias, 191 integración, 4 arquitectura, 3 E2E), 0 advertencias con `-warnaserror` y `dotnet format` limpio. Sin commits. |
| 3.2.5 | 2026-11-15 | Implementación inicial de **Sensi IA (Spec 046 v2.0.0)** y los 8 prerrequisitos (moderación fail-closed, texto omni-moderation, configuración inicial, middleware de correlación, rate limiting, búsqueda de ofertas, apartados y paginación en comercio). 726 pruebas en verde. La revisión de código posterior identificó 14 brechas y casos de borde técnicos a subsanar (números mágicos en Application, inyección por ScreenContext, falta de purga en borrado de cuenta, persistencia y atomicidad de presupuesto en Redis, fallback silencioso de base de conocimiento, duplicados en paquetes, exposición de PII en cuenta y manejo de excepciones/cancelación). |
| 1.0.0 | 2026-10-28 | Creación inicial. Estado de sesión, principios, decisiones D1–D6, artefactos y próximos pasos. |
| 2.0.0 | 2026-10-29 | Reescritura al estado real: repositorio con solución .NET, 28 specs con cuatro artefactos, decisiones D7–D11, hallazgos de seguridad y próximos pasos revisados. Verificado: build Release sin warnings, 42 pruebas en verde, `dotnet format` limpio, cobertura real 25.89 % (Domain) y 34.78 % (Application). |
| 2.1.0 | 2026-10-29 | Realineamiento al catálogo de 29 endpoints: 43 specs activas + 1 en backlog, enmienda constitucional v1.2.0, ADR 0007–0009, decisiones D12–D19, rutas `/api/v1` en inglés y cierre de huecos (QR, recuperación, perfil, ajustes, health). Verificado: `check-specs` 44/44, cobertura del catálogo 29/29, `dotnet build -c Release` sin advertencias y 42 pruebas en verde. |
| 2.2.0 | 2026-11-04 | Migración a `sensus-proj/sensus-back` (D20): `origin` en la organización, `Elrorro334` como `legacy`, rama `SprintOscar` creada desde `origin/Sprint1` con el commit SDD aplicado por cherry-pick. Conflictos resueltos en `Directory.Build.props` (fusión de `NoWarn`, `AnalysisLevel` del equipo, bloqueo de calidad en Release y CI) y `.editorconfig` (unión de reglas). Se documentó el comando correcto de *user-secrets* (`JwtSettings:Secret`) y la deuda de los secretos JWT en `Program.cs` y `appsettings.Development.json`; el comportamiento del equipo no se modificó. Verificado: `dotnet build -warnaserror` sin advertencias y 135 pruebas en verde. |
| 2.2.1 | 2026-11-04 | Alineación de ramas y CI (D21): Se corrigieron rutas duplicadas en OpenAPI (specs 003, 025, 044), sintaxis de `hashFiles` en job `iac` de `ci.yml`, agregado de `Sprint1` al disparador `pull_request` y configuración resiliente de Gitleaks y cobertura para el desarrollo del sprint. Sincronización fast-forward de `Sprint1` con `main` en `sensus-proj/sensus-back`. |
| 2.4.0 | 2026-11-04 | Despliegue EC2, Certificado SSL (HTTPS) y CI/CD (D22): Publicación en AWS EC2 (Ubuntu 24.04, .NET 10 SDK), Nginx reverse proxy con certificado SSL oficial Let's Encrypt para `18-117-247-171.sslip.io`, actualización de `DEFAULT_BASE_URL` en app móvil y creación de `.github/workflows/deploy.yml` para despliegue continuo automático por SSH. |
| 2.3.0 | 2026-11-04 | Implementación de Spec 004 (Cierre de sesión y revocación de token): Se creó `LogoutCommand`, `LogoutCommandHandler`, endpoint `POST /api/v1/auth/logout` (y alias `/api/auth/logout`), soporte idempotente en `OnTokenValidated` de JWT y rotación de `SecurityStamp`. Verificado con 100 pruebas unitarias y 34 de integración en verde (141 pruebas totales en la solución). |
| 2.5.0 | 2026-11-05 | Alineación documental de la spec 007 (v1.1.0) con el backlog de la escuela: se añadieron `FR-007-08` (radio 1–5 km registrado) y `FR-007-09` (2 km estricto invitado), la sección de equivalencia SQL Server → MongoDB (`GEOGRAPHY`→GeoJSON, índice `SPATIAL`→`2dsphere`, `STDistance()`→`$geoNear`/`NearSphere`) y el mapeo `SE-008/011/012/013/016 → FR`. Se registró que la colección `offers` depende de la spec 034. Sin cambios de código. |
| 2.5.1 | 2026-11-05 | Correcciones de CI: `check-coverage.mjs` fusiona reportes Cobertura por línea (antes los sumaba y contaba 7 veces las mismas líneas, hundiendo el porcentaje a ~42 %); job SonarCloud usa la clave real del proyecto `sensus-proj_sensus-back` (antes `sensus-back`, inexistente), añade `dotnet restore`, corrige la ruta glob del reporte y evita ejecutar `sonarscanner end` si `begin` falló. |
| 2.5.2 | 2026-11-05 | Quality Gate de SonarCloud: se sustituyó la propiedad inexistente `sonar.cs.dotnet.reportsPaths` por `sonar.cs.opencover.reportsPaths` y el job de Sonar genera el reporte en formato **OpenCover** (`--collect:"XPlat Code Coverage;Format=opencover"`), el recomendado por Sonar para C# con Coverlet, con un paso que verifica que el archivo exista. Se excluyó de cobertura `**/Program.cs` (composition root) y los adaptadores de E/S (`Persistence/Mongo/**`, Cloudinary, correo y geocodificación), y el job de Sonar ejecuta ahora todas las pruebas sobre un servicio MongoDB para maximizar la cobertura de código nuevo. Se eliminaron todos los secretos y credenciales literales: `Program.cs` genera un secreto JWT efímero en Development cuando no hay configuración (y falla con mensaje claro fuera de Development), `appsettings.Development.json` queda sin secreto, `JwtSettingsValidator` ya no mantiene una lista de secretos filtrados y las pruebas usan contraseñas y tokens efímeros. CORS pasa de `AllowAnyOrigin` a una allowlist explícita de orígenes. Pruebas: 190 en verde, `dotnet format` sin cambios. |
| 2.5.3 | 2026-11-05 | Resolución integral y aprobación de Quality Gate en SonarCloud (PR #7 hacia `Sprint1`): Se resolvieron de raíz las dos condiciones fallidas. 1) **Security Rating C -> A**: Al consultar la API de issues de Sonar, se identificaron dos `MAJOR VULNERABILITY` en `ci.yml` (`githubactions:S6505` por omitir `--ignore-scripts` y `githubactions:S8543` por no anclar la versión de Snyk en npm); se solventó con `npm install -g --ignore-scripts snyk@1.1308.4`. 2) **Coverage on New Code 0.0 % -> Pass**: Se identificaron 29 líneas descubiertas originadas por `check-coverage.mjs` (26 líneas JS de CI evaluadas por Zero Coverage Sensor) y `SqlToMongoMigrationService.cs` (3 líneas no cubiertas por discrepancia de carpeta `Migration/` vs patrón `**/Migrations/**`). Se excluyó `.github/**` de las fuentes de producto de Sonar, se corrigió el patrón a `**/Migration/**` y se escribieron 3 pruebas unitarias exhaustivas en `SqlToMongoMigrationServiceTests.cs` (todas en verde). 3) Se endureció `Program.cs` restringiendo el secreto JWT efímero a `Development` y `Testing`, se eliminó el comentario residual con credencial `'123456789'` y se limpió el disparador de CI para ejecutar exclusivamente sobre `Sprint1` y `main`. Verificado: 9/9 checks en verde (`SonarCloud Code Analysis` en `success`, Quality Gate Passed, `PR mergeable_state: clean`). |
| 2.6.0 | 2026-11-08 | Adopción oficial de Cloudinary (D23): Enmienda constitucional v1.3.0 (§2.1, §2.2, P-VI), creación de ADR 0010, actualización de overview de arquitectura y specs 019, 022 y 023 para oficializar Cloudinary como proveedor de almacenamiento y CDN multimedia, sustituyendo S3 y CloudFront. |
| 2.7.0 | 2026-11-08 | Implementación de Spec 048 (Caché distribuido con Redis, D24): Se creó `ICacheService` en `Application.Common.Interfaces` y `RedisCacheService` en `Infrastructure.Cache` utilizando `StackExchange.Redis`, con fallback transparente ante caídas o timeouts (FR-048-11), soporte de TTL por clave y eliminación por prefijo. Se integró lectura en caché e invalidación en `GetProfileQuery`, `UpdateProfileCommandHandler`, `GetMyComercioQuery` y `UpdateMerchantProfileCommandHandler`. Se añadieron configuraciones de Redis en `appsettings.json` y 4 suites de pruebas (`RedisCacheServiceTests`, `GetProfileQueryHandlerTests`, y actualización de `GetMyComercioQueryHandlerTests`). Verificado: 201 pruebas en verde (155 unitarias, 4 arquitectura, 3 E2E, 39 integración), `dotnet format` limpio y 46 specs SDD conformes. |
| 2.8.0 | 2026-11-08 | Implementación de Spec 045 (Infraestructura Serverless como Código con AWS CDK, D25): Se habilitó hospedaje dual Kestrel/Lambda en `Sensus.Api` (`Amazon.Lambda.AspNetCoreServer.Hosting` con `LambdaEventSource.HttpApi`) y se creó el proyecto `infra/src/Sensus.Infra/Sensus.Infra.csproj` con `infra/cdk.json` y `SensusInfraStack.cs` (AWS Lambda `.NET 10` sobre `PROVIDED_AL2023` `ARM_64`, API Gateway HTTP API v2, CloudWatch Logs con retención de 7 días y alarmas 5xx/errores/throttling, rol IAM de mínimo privilegio con lectura de SSM Parameter Store a costo 0 USD, y presupuesto mensual en AWS Budgets con alerta en 1.00 USD). Se actualizó el job `iac` en `.github/workflows/ci.yml` y se creó `.github/workflows/deploy-serverless.yml`. Verificado: 47 specs SDD conformes, compilación limpia sin warnings y 201 pruebas en verde. |
| 2.9.0 | 2026-11-12 | Moderación Inteligente Multimodal y Bandas de Decisión (Spec 018 / 017, D27): Análisis granular de `category_scores` de `omni-moderation-latest`, evaluación multimodal conjunta (imagen + texto contextual), bandas configurables en `appsettings.json` (Aprobado, Cuarentena, Rechazo) por categoría y fail-closed integral. 226 pruebas en verde. |
| 2.10.0 | 2026-11-13 | Moderación Visual Comercial Estricta con gpt-4o-mini Vision (Spec 017/018, D28): Integración dual en `OpenAiImageModerationService` para clasificar visualmente armas de fuego/blancas, simbología de odio/nazi, drogas y desnudez con `gpt-4o-mini` en baja resolución (`detail: low`, 85 tokens/evaluación) con formato JSON estructurado, preservando `omni-moderation-latest` para modelos de texto. Añadidas pruebas exhaustivas en `OpenAiImageModerationServiceTests`. Verificado: 229 pruebas en verde (168 unitarias, 4 arquitectura, 54 integración, 3 E2E) y sincronización con `main` para despliegue automatizado en AWS Lambda. |
| 2.11.0 | 2026-11-13 | Spec 046 Asistente conversacional Sensi (D28): ADR 0011, enmienda constitucional v1.4.0, 4 artefactos + contrato OpenAPI, 24 requisitos en `requirements.md`, índice a 48 specs y excepción `EX-001` (Apéndice E). Verificado: `check-specs` 48/48 con estructura SDD correcta. Sin cambios de código ni despliegue. |
| 3.0.1 | 2026-11-14 | Integración en `main` (`01dad6e`, PR #17 `sprint3_oscar` y PR #18 `Sprint3_B`): specs 005, 007, 008, 015, 016, 019, 022, 027, 031, 032, 033 y 044 implementadas/actualizadas (029 y 048 `Parcial`), coordenadas reales de comercio, ofertas cercanas, fix Cloudinary, passthrough CDK, `InMemoryOfferRepository` en E2E, métricas EMF `Sensus/Api`, `POST /api/v1/media/{id}/release`, `PUT /api/v1/users/device-token`, `GET+PUT /api/v1/users/settings`; 613 pruebas en verde (Domain 90,2 %, Application 97,4 %) y síntesis CDK correcta. |
| 3.1.0 | 2026-11-15 | Regularización documental SDD de specs con código (004, 012, 013, 017, 030, 034–037, 047) verificando cada requisito contra el código: `tasks.md`, `checklist.md` (Constitution Check con brechas), `specs/README.md` (004 y 047 `Implementado`; el resto `Parcial`) y `specs/requirements.md` (total corregido a 411 requisitos en 48 specs). Verificado: build `-warnaserror` sin errores, 523 unitarias, 4 de arquitectura y 116 de integración en verde, `check-specs` correcto. Sin cambios de código ni commits. |
| 3.1.1 | 2026-11-15 | Corrección de FR-030-06: `DeleteAccountCommandHandler` archiva las ofertas del comercio e invalida su caché antes de borrarlo. Regresión de integración (falló antes del arreglo) y 2 pruebas unitarias nuevas. Verificado: build `-warnaserror` sin advertencias, `dotnet format` limpio, 525 unitarias, 4 de arquitectura, 117 de integración y 3 E2E en verde. Sin commits. Siguiente paso: T030-09 (apartados activos) y decidir la enmienda de Spec 046. |
| 3.2.3 | 2026-11-15 | `constitution.md` v2.1.0 (MINOR): moderación de contenido publicado con `gpt-4o-mini` en visión y política de Sensus (§2.1, §2.4), `omni-moderation-latest` solo para el texto de Sensi y excepción `EX-002` por su costo. Spec 017 v1.2.0 (FR-017-02 y FR-017-03 alineados a D28). Spec 046 v2.0.2 con claves propias `OpenAiSettings:Sensi:ModerationModel`, `FastModel` y `ReasoningModel`. Proyecto de OpenAI con modelos permitidos `gpt-4o-mini`, `gpt-4o` y `omni-moderation-latest`. `check-specs` correcto. Sin código ni commits. |
| 3.2.4 | 2026-11-15 | Implementación de Items 0, 1, 2 y T046-01 a T046-05: 1) Corrección de fail-open en `OpenAiImageModerationService` (cuarentena ante falta de API key, error o timeout) con pruebas de regresión. 2) Puerto `ITextModerationService` y `OpenAiTextModerationService` con dobles de prueba fail-closed. 3) Opciones tipadas `SensiSettings` y validador `SensiSettingsValidator` con límites de 5.00 USD total y 3.00 USD reasoning validados en arranque. 4) Entidades e invariantes de dominio de Sensi IA (`AiConversation`, `AiMessage`, `AiTraceLog`, `RecommendedPackage`, `BudgetCalculator`). 5) Puertos de Application, DTOs y comandos/queries CQRS. 6) Documento Markdown de conocimiento Sensus v1.0.0 y `KnowledgeBaseProvider`. 7) `TwoBlockPromptBuilder` (bloque estático `>= 1,024` tokens + bloque dinámico). Verificado: 536 unitarias, 4 arquitectura, 138 integración en verde; `dotnet format` y build `-warnaserror` limpios. Sin commits. |
| 3.2.2 | 2026-11-15 | Techo de `EX-001` elevado de 1.00 a **5.00 USD/mes** por decisión del responsable (constitución Apéndice E, ADR 0012, spec 046 con alarma en 4.00 USD, `requirements.md`); nivel *reasoning* habilitado con sub-techo de 3.00 USD. Revisión de prerrequisitos de código para Sensi registrada en §E, incluido el fail-open de moderación sin clave. El presupuesto de AWS Budgets (1.00 USD, spec 045) no cambia: cubre AWS, no OpenAI. `check-specs` correcto. Sin código ni commits. |
| 3.2.1 | 2026-11-15 | Correcciones a la evolución de Sensi IA: enmienda reclasificada a **MAJOR** (`constitution.md` v2.0.0, porque redefine la prohibición de herramientas de §2.2) y propagada a `AGENTS.md`, ADR 0012 y specs 046/047; excepción constitucional que separa *Evals* deterministas (en `dotnet test`, bloquean el PR) de *Evals* con modelo (job `ai-evals` previo a release); estimación de costo por turno (≈ 0.001 USD *fast*, ≈ 0.016 USD *reasoning*, ≈ 1,000 vs ≈ 60 turnos con 1.00 USD) con nivel *reasoning* desactivado por defecto; TTL de `ai_trace_logs` en 30 días; `AGENTS.md` corrige el índice a 47 activas y 1 en backlog. Verificado: `check-specs` correcto. Sin código ni commits. |
| 3.2.0 | 2026-11-15 | Evolución SDD completa de **Sensi IA** a arquitectura agéntica de 7 capas con dominio de negocio extraído de Drive: enmienda `constitution.md` v1.5.0 (P-IV, §2.1, §2.2, §2.4, §2.5), `AGENTS.md` v1.5.0, [ADR 0012](docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md) (sustituye ADR 0011), Spec 046 v2.0.0 (`spec.md`, `plan.md`, `tasks.md` `T046-01`..`T046-22`, `checklist.md`, `contracts/openapi.yaml` con `POST/GET/DELETE /api/v1/ai/conversations*` y `POST /api/v1/ai/messages`), Spec 047 v1.1.0 (`FR-047-13`..`FR-047-15`, `NFR-047-06`, `CA-047-09`..`CA-047-10`, `T047-14`..`T047-16` pendientes para gate CI de *Evals*), `specs/README.md` y `specs/requirements.md`. Sin código C# ni commits. Abierto: implementación de `T046-01`..`T046-22`, `T047-14`..`T047-16`, `T030-09` y actualización opcional de `docs/architecture/overview.md`. Siguiente paso: ejecutar `T046-01`..`T046-05` (dominio, puertos, Markdown `<contexto>` y *TwoBlockPromptBuilder*) tras revisión de las specs. |
| 3.0.2 | 2026-11-14 | Sincronización directa de `Back_Rodrigo` y `main` local con `origin/main` (`01dad6e`), fusionando el estado de Sprint 3 de `main` con los cambios locales de Spec 046 (`2.11.0`) y restaurando las secciones B–H de la memoria. |

---

## Cómo actualizar este archivo

- Actualizar al cerrar cada bloque de trabajo o al cambiar de fase SDD.
- Registrar aquí **hechos y decisiones**, no tareas pendientes: las tareas viven en `tasks.md`.
- Toda decisión nueva se añade a §C con su ID y fecha; si afecta un principio, exige enmienda (§4.3 de la constitución).
- Nunca escribir secretos, credenciales ni datos personales.
- Subir la versión: PATCH por corrección, MINOR por sección nueva, MAJOR por cambio de estructura.

## Auditoría técnica del 2026-11-18

- Revisión de `main` en `c2e56e1`. GitHub Actions registró CI y CD exitosos. CI ejecutó 882 casos únicos sin fallos ni omisiones (631 unitarios, 244 de integración, 4 de arquitectura y 3 E2E). La URL de salud de API Gateway respondió HTTP 200 por HTTPS.
- Inventario de 45 operaciones funcionales y 3 rutas de salud. Quedaron brechas de autorización en los dos disparadores de expiración y de trazabilidad y cobertura HTTP en varias rutas. El runner de Evals usa puntuación fija y no acredita evaluación con un modelo real.
- Siguiente paso propuesto para una futura sesión: priorizar la autorización de los disparadores y la verificación real de Evals. No se modificó el código de aplicación ni se ejecutó un despliegue.

## Sincronización del 2026-11-18

- La rama local `main` ya coincidía con `origin/main` en `c2e56e1`. Se conservó la auditoría local previa. La sincronización quedó verificada y no requiere seguimiento.

## Push de eventos y ofertas cercanas (2026-11-18)

- Se revisaron las specs 027, 034 y 044 y el despacho social. El adaptador FCM simula entregas y el flujo social no aplica las preferencias. La spec 027 deja abierta la elección del proveedor de producción. Para especificar alertas de ofertas cercanas queda por definir el origen de la ubicación del destinatario. Siguiente paso: resolver el proveedor y la ubicación, y completar los criterios de aceptación antes de implementar.

### Alcance confirmado y secuencia propuesta

- El responsable indicó que Firebase aún no está configurado y que las alertas cercanas usarán la última ubicación compartida por la app. Solicitó también avisar al comercio al confirmar un apartado y mostrar al comprador el tiempo restante para recogerlo.
- Orden propuesto: cerrar criterios SDD; configurar FCM y verificar un push social en un teléfono; añadir evento de apartado y cuenta regresiva usando ExpiresAtUtc; incorporar ubicación consentida y alertas al publicar ofertas; validar en dispositivos reales antes de liberar. Quedan por confirmar plataforma y tecnología móvil, vigencia de ubicación y si la cuenta regresiva también debe aparecer en la bandeja del sistema. No se modificaron specs ni código ni se configuró Firebase.

- Confirmado: Android Kotlin, repositorio sensus-proj/sensus-app-mobile. En main (2c6e1c2), Gradle declara applicationId sensus.com y Firebase Messaging, y el manifiesto incluye POST_NOTIFICATIONS. Se eligió FCM HTTP v1, se actualizó la spec 027 y se registró el envío real como pendiente. Siguiente paso: confirmar acceso al proyecto Firebase de pruebas, registrar la app y completar la preparación SDD antes de implementar el adaptador.

- Firebase: se creó Sensus Dev (sensus-dev-4f843, plan Spark) y se registró Android sensus.com como Sensus Android Dev. Siguiente paso: incorporar google-services.json a la app Android y verificar entrega en un teléfono; el adaptador del backend sigue pendiente.

- Se localizó la copia Android en StudioProjects/sensus-app-mobile y se verificó que FT-21 ya tiene activación explícita, obtención de token y registro ante la API. No se encontró google-services.json después del intento de descarga desde el navegador integrado. ADB funciona y no detectó dispositivos. Siguiente paso: obtener el archivo oficial, activar su procesamiento en Gradle y comprobar el flujo en Android.

- Se integró en Sensus Mobile el google-services.json oficial del proyecto sensus-dev-4f843, con Google Services en Gradle y el JSON fuera de Git. processDebugGoogleServices y compileDebugKotlin pasaron. Pendientes: verificar en Android físico el permiso, el registro del token y el push desde Firebase Console, y completar el adaptador FCM real del backend.

- Prueba en Android físico: se detectó permiso de notificaciones denegado y app detenida. Tras habilitar el permiso y abrir Sensus, se envió Prueba Sensus desde Firebase Console y se verificó en Android. Entrega Firebase a Android confirmada. Siguiente paso: implementar y verificar el envío real desde el backend.

- Se detalló el plan 027 para FCM HTTP v1 con credenciales por ambiente en SSM, resultado de despacho explícito, protección ante fallos y pruebas de contrato. Se registró la evidencia de la prueba Firebase Console a Android. El plan sigue pendiente de aprobación según la constitución §3.1; después corresponde implementar y probar el aviso social desde el backend, antes de apartados y ofertas cercanas.

- Plan 027 aprobado e implementado en local: FCM HTTP v1, OAuth desde SSM, validación sin entrega, 503 ante indisponibilidad y filtro social. Pruebas: 636 unitarias, 258 integración, 4 arquitectura y 3 E2E en verde con SDK 11 contra net10.0; cobertura Domain 89.00 % y Application 96.83 %. Pendientes: credencial de pruebas en SSM, aviso real backend a Android, verificación en MongoDB de desactivación condicional y corrección de colisión de token al reasignar. No hubo despliegue. Después siguen los apartados y las ofertas por zona.

- Backend de notificaciones: FR-033-09/10 integra avisos al comprador y comercio tras persistir el apartado, con vencimiento UTC y deduplicación MongoDB. FR-044-07 incorpora PUT/DELETE de ubicación consentida, vigencia predeterminada 24 h, índices 2dsphere/TTL y purga al eliminar la cuenta. FR-044-08 sigue sin disparador por falta de evidencia de moderación vinculada a la versión vigente de la oferta (FR-034-03). Pasaron 915 pruebas, incluidas tres de Testcontainers contra MongoDB real; cobertura Domain 89.00 % y Application 96.53 %. Pendientes: acceso al entorno AWS de pruebas, credencial FCM en SSM, prueba real desde el backend y latencia p95. Siguiente paso de ofertas: cerrar el vínculo de moderación y el evento de publicación segura. No se modificó Android ni se desplegó.

- 2026-11-19 (Claude): FR-033-11 cerrado con pruebas de integración (firma reemplazada sin QR, apartado inexistente y comercio inactivo con 404) y prueba unitaria de fallos del adaptador en `ClaimPushNotifier`. FR-044-08 implementado con el alcance que decidió el responsable: al crear una oferta disponible, `NearbyOfferNotifier` modera el título y la descripción (*fail-closed*), busca ubicaciones vigentes con `$geoNear`, filtra preferencias, radio inclusivo y titular del comercio, y envía `Nuevo {título} cerca de ti` sin cuerpo, con `type=nearby_offer` y `offerId`. La configuración vive en `NearbyOfferAlerts`. Pruebas: 649 unitarias, 276 de integración (incluidas 2 nuevas de Testcontainers) y 4 de arquitectura en verde; build `-warnaserror` correcto. Todo se ejecutó con el SDK 11 desde fuera del repo porque el SDK 10.0.401 de `global.json` no está instalado. Pendientes: credencial FCM en SSM, `Push:Simulate=false`, que Android abra `GET /offers/{offerId}` al recibir `nearby_offer`, la moderación de la imagen de la oferta (brecha de la spec 034) y medir la latencia de `POST /offers`. Sin commits ni despliegue.

## Inventario técnico y DevSecOps del 2026-11-18

- Se contrastó main (c2e56e1), el árbol local y los workflows. Se identificaron 50 operaciones locales, incluidas dos rutas de ubicación aún locales, frente a 48 en el despliegue registrado. Quedaron documentadas las brechas de autorización de expiraciones, trazabilidad de rutas, DAST y cobertura HTTP. La consulta de salud desde esta sesión quedó sin resolver por DNS. No se modificó el código ni se desplegó.
- Siguiente paso técnico identificado: cerrar la autorización de las tareas de expiración y cubrirla con pruebas HTTP.

### 2026-11-19 — Revisión de integración Android de notificaciones

Se revisaron los contratos, el código y la evidencia de las specs 027, 033, 034 y 044. Android puede integrar registro de dispositivo, preferencias, ubicación compartida y confirmación de apartado con QR desde la respuesta REST. Quedan abiertos la credencial FCM en SSM, la prueba de entrega y apertura desde el push real, la medición de latencia del apartado y el disparador de ofertas cercanas condicionado a moderación verificable. La API carece de lectura de apartado por ID para recuperar el contexto desde el push. No se modificó código ni se reejecutaron pruebas porque el SDK .NET solicitado no está instalado. Siguiente paso: verificar el registro y un apartado con dos cuentas y FCM real en desarrollo.

### 2026-11-19 — Avance de cierre de notificaciones

Se añadió la desvinculación push opcional por deviceId al logout (FR-004-05), dos pruebas unitarias y el contrato de navegación de los avisos de apartado con el detalle de oferta como destino disponible sin estado local. Quedaron pendientes la verificación del cambio con .NET 10.0.401 y MongoDB, la corrección y verificación de la reasignación de token de T027-08, y la implementación del disparador cercano después de cerrar FR-034-03. El SDK solicitado no está instalado y la comprobación de descarga falló por resolución DNS. La configuración FCM y las pruebas de entrega real se reservaron para el final por petición del responsable.

### 2026-11-19 — Lectura de apartado y reasignación de token push

Implementados, pendientes de verificar: GET de apartado autenticado con QR solo para el comprador vigente y no-store (FR-033-11), y liberación condicional de la asociación previa del token para evitar la colisión del índice único al cambiar de cuenta (FR-027-06). Añadidas pruebas de API y test de repositorio MongoDB. El filtro oficial de pruebas sigue bloqueado por el SDK .NET 10.0.401 faltante. Para las alertas de cercanía se documentó el diseño en 034 y 044. Sigue pendiente implementar evidencia conjunta, cuarentena previa a almacenamiento, selección y despacho. Firebase y la prueba Android permanecen para el final.

### 2026-11-19 — Bloqueo normativo de moderación para alertas cercanas

Se contrastó la cuarentena exigida por el Principio IV y la spec 019 con Cloudinary como almacenamiento multimedia exclusivo (Principio VI). La ruta actual retiene en Cloudinary y escribe antes del veredicto. Se dejó en el plan 034 una propuesta §4.3 para retención temporal privada en MongoDB con TTL antes de Cloudinary. Pendientes aprobación, implementación de moderación y alertas, y verificación del punto 3. La consulta de SDK sigue mostrando 9.0.317 y 11.0.100, sin SDK compatible con global.json. No se cambiaron endpoints ni se activó Firebase.
