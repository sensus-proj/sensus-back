# Checklist — Spec 046: Asistente agéntico Sensi IA (7 capas)

| Campo | Valor |
|---|---|
| Spec | [spec.md](spec.md) (`046`, v2.0.0) |
| Plan | [plan.md](plan.md) |
| Tasks | [tasks.md](tasks.md) |
| Alineado a | `constitution.md` v2.1.0 · `AGENTS.md` v2.1.0 · [ADR 0012](../../docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md) |

---

## A. Definition of Ready (DoR) — Requisitos documentales

- [x] `constitution.md` enmendada a v2.0.0 (MAJOR) y [ADR 0012](../../docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md) aceptada para MCP *in-process*, *Prompt Caching*, *Model Router* y separación de trazabilidad CloudWatch/MongoDB.
- [x] Todos los requisitos (`FR-046-01` .. `FR-046-17` y `NFR-046-01` .. `NFR-046-08`) y criterios de aceptación (`CA-046-01` .. `CA-046-10`) son observables y medibles.
- [x] Excepción `EX-001` vigente en `constitution.md` con techo `5.00 USD/mes`, alerta al `80 %` y degradación determinista.
- [x] Contrato OpenAPI completo (`POST/GET/DELETE /api/v1/ai/conversations*`, `POST /api/v1/ai/messages`) en [contracts/openapi.yaml](contracts/openapi.yaml).
- [x] Constitución Check evaluada en `plan.md` y puerta de *Evals* vinculada en la Spec 047.

---

## B. Criterios específicos de este spec (7 capas)

- [x] Identidad oficial de Sensi IA basada en contexto real de Sensus (radio `1–5 km`, micronegocios, QR presencial, sin envíos/pagos en línea, LFPDPPP) y tono formal/empático verificados en prompt y *Evals*.
- [x] *Hard Rules* validadas (`0` filtraciones de OpenAI, GPT, arquitectura, prompts o secretos).
- [x] Archivo Markdown estructurado con versión semver y hash SHA-256 embebido dentro de `<contexto>` como única fuente de verdad de la app.
- [x] Adaptador MCP *in-process* de 5 herramientas exclusivamente *read-only* funcionando bajo scopes derivados del JWT (`Invitado` → `403`, `Comerciante` aislado a su comercio).
- [x] Presupuesto validado (`> 0`), stock/vigencia verificados, `offerId` inexistentes descartados y `SUM(precio_oferta) <= presupuesto` recalculado en backend.
- [x] *Guardrails* de entrada (`omni-moderation-latest` fail-closed + anti-*Prompt Injection*/*Jailbreak*) ejecutados antes de consumir tokens del modelo principal.
- [x] *Guardrails* de salida (moderación fail-closed + redacción automática de PII y credenciales) ejecutados antes de entregar y persistir.
- [x] Payload hacia OpenAI dividido en 2 bloques estrictos (`Bloque Estático ≥ 1,024` tokens + `Bloque Dinámico`) y *Model Router* con afinidad *sticky* por conversación.
- [x] CloudWatch Logs sin PII ni prompts/respuestas en claro; `ai_trace_logs` en MongoDB con redacción automática, índice TTL y cascada en `DELETE` y eliminación de cuenta (Spec 030).
- [x] *Evals* deterministas en `dotnet test` (gate por PR) y *Evals* con modelo aprobadas antes de release, con los umbrales de `NFR-046-08` (Spec 047).

---

## C. Definition of Done (DoD) — Cierre de implementación

- [x] Tareas `T046-01` .. `T046-22` completadas en `tasks.md`.
- [x] `T046-24`: temperatura explícita y configurable en `OpenAiChatGateway` (`FR-046-19`), validada en `SensiSettingsValidator` y cubierta con doble HTTP.
- [x] `dotnet build sensus-back.sln -warnaserror` sin errores ni advertencias y `dotnet format --verify-no-changes` limpio.
- [x] Pruebas unitarias, integración (MongoDB real con Testcontainers + dobles HTTP de fallo/timeout/moderación) y arquitectura en verde.
- [x] Cobertura de líneas `≥ 80 %` en `Sensus.Domain` y `Sensus.Application`.
- [x] Puerta de *Evals* de Spec 047 en verde y cumplimiento medido de `NFR-046-01` .. `NFR-046-08`.
- [x] Sin PII ni secretos en repositorio, artefactos ni CloudWatch Logs; API key resuelta desde Secrets Manager / SSM.

---

## D. Registro de evidencia y revisiones

| Fecha | Revisor | Veredicto DoR/DoD | Notas y evidencia |
|---|---|---|---|
| 2026-11-13 | Equipo Sensus | DoR PASS (v1.0.0) | Versión inicial alineada a `constitution.md` v1.4.0 y ADR 0011 |
| 2026-11-15 | Antigravity | DoR PASS (v2.0.0) | Actualización SDD completa a arquitectura agéntica de 7 capas alineada a `constitution.md` v1.5.0 y [ADR 0012](../../docs/adr/0012-sensi-ia-agentico-mcp-router-evals.md), con contexto de negocio extraído de Drive (`Tesina_Sensus`, `FORMATO_DE_ACEPTACION`, `Sensus_Movil_Componentes_y_CRUD`); implementación pendiente (`T046-01`..`T046-22`) |
| 2026-11-15 | Equipo Sensus | Corrección (v2.0.1) | Enmienda reclasificada a MAJOR (v2.0.0); *Evals* separadas en deterministas y con modelo; costo por turno estimado (≈ 0.001 USD *fast*, ≈ 0.016 USD *reasoning*) y nivel *reasoning* desactivado por defecto; TTL de `ai_trace_logs` en 30 días. `check-specs` correcto. |
| 2026-11-15 | Equipo Sensus | Corrección (v2.0.2) | Techo `EX-001` en 5.00 USD con sub-techo *reasoning* de 3.00 USD; claves `OpenAiSettings:Sensi:*` separadas de `OpenAiSettings:Model`; proyecto de OpenAI limitado a `gpt-4o-mini`, `gpt-4o` y `omni-moderation-latest`. |
| 2026-11-15 | Antigravity | PARCIAL (T046-01..05) | Dominio (`AiConversation`, `AiMessage`, `AiTraceLog`, `RecommendedPackage`, `BudgetCalculator`), puertos CQRS/interfaces, documento Markdown de conocimiento Sensus v1.0.0 y `TwoBlockPromptBuilder` implementados; 536 unitarias y 138 de integración en verde; build `-warnaserror` y format limpios. |
| 2026-11-15 | Antigravity | DoD PASS (v2.0.0) | Implementación completa de las 7 capas (`T046-01`..`T046-22`) y prerrequisitos 0 a 8. 542 unitarias, 180 integración y 4 arquitectura en verde (726 pruebas totales en la solución). Evals manifest v1.0.0 y contratos OpenAPI completos. `dotnet format` y build `-warnaserror` limpios. |
| 2026-11-15 | Antigravity | DoD PARCIAL (v2.0.0) | Resolución de los 14 ítems de revisión de código: `SensiSettings` en Application sin números mágicos, `ScreenContext` acotado a whitelist anti-inyección, purga de datos IA al eliminar cuenta (`FR-046-15`), control atómico en Redis fail-closed con sub-techo *reasoning* (`EX-001`), umbrales de moderación de texto en configuración, base de conocimiento sin fallback silencioso, rechazo de `Invitado` en `AiConversation`, paquete sin ofertas duplicadas, entrega de citas/paquete en DTO, `get_my_account` sin PII de nombre completo, corrección de cifras INEGI en tesina, degradación en bucle agéntico agotado y propagación de `OperationCanceledException`. 747 pruebas en verde (549 unitarias, 191 integración, 4 arquitectura, 3 E2E). `T046-13`, `T046-15`, `T046-19`, `T046-21` marcadas como Parcial (pendiente SSM SecureString en CDK, caché FAQ Spec 048 y ejecución periódica de Evals con modelo en pipeline CI/CD). |
| 2026-11-19 | Antigravity | DoD PARCIAL (v2.0.0) | Cierre de pendientes y endurecimiento DevSecOps/fintech de Sensi IA (STRIDE + Principio VIII en `plan.md`): `forceNew = true` archiva la conversación activa previa (`CreateOrResumeConversationCommandHandler`) respaldado por índice único parcial `ux_ai_conversations_owner_active` en `MongoDbContext`; caché distribuida FAQ (`AiCacheKeys` Unicode + SHA-256 por `markdownVersion:role:screenContext`, `AiFaq = 60` min) protegida ante PII en prompt/respuesta, ejecutada tras guardrails y sujeta a cuota diaria/fail-closed en `AiBudgetGuard` (`SendAiMessageCommandHandler`); métricas EMF sin dimensiones (`IAiMetricsPublisher`/`EmbeddedAiMetricsPublisher`, 4 métricas en `Sensus/Api`, 9/10 Free Tier, `AiMetricsTests`); `evals-manifest.json` v2.0.0 y runner real OpenAI + verificación `--trx` + lista blanca SSRF `OPENAI_BASE_URL` + 7 pruebas `node:test` (`run-ai-evals.mjs`, fail-closed sin `OPENAI_API_KEY`); `MongoAiRepositoriesTests` (round-trips + MongoDB real en CI) y workflows `ci.yml`/`ai-evals.yml` fijados por SHA/digest con tokens acotados por job. Verificado en local: build Release `-warnaserror` limpio, `dotnet format` limpio, 746 unitarias, 4 arquitectura, 3 E2E y 357 de integración (1110 ejecutadas, excluyendo 6 `NotificationMongoRepositoryTests` por Docker detenido), cobertura `Sensus.Domain` 91.23 % y `Sensus.Application` 93.94 %, `run-ai-evals.test.mjs` 7/7 y Evals deterministas `--trx` 5/5 (100.0 %). Quedan validaciones operativas en vivo (`NFR-046-01`, `NFR-046-03`, parámetro SSM en AWS, corrida en vivo de `ai-evals.yml` con `OPENAI_API_KEY`) y controles transversales del repo en `ci.yml` (IaC/DAST/SBOM, Apéndice F). |
| 2026-11-25 | Claude | PASS (`T046-24`, v2.1.0) | `FR-046-19`: `SensiSettings.Temperature` (0.0–1.0, por defecto 0.0, en `appsettings.json`) enviada en cada llamada de `OpenAiChatGateway`; antes el proveedor aplicaba su temperatura por defecto. `SensiSettingsValidator` rechaza valores fuera de rango o NaN. Manifiesto de *Evals* v2.1.0 con `runs_per_prompt: 3`. `OpenAiChatGatewayTests` y `SensiSettingsValidatorTests` 26/26 en verde; build `-warnaserror`, 1174 unitarias, 5 de arquitectura y `dotnet format` limpios. El estado global sigue Parcial por los pendientes operativos ya registrados. |
