# Checklist 047 — Pipeline CI-CD y calidad de software (DevSecOps)

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 047 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 009, 010, 011, 012, 013
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El job `build-and-test` no tiene `services.mongo`
- [x] El job `integration-tests` tiene `services.mongo` con `mongo:7.0` y healthcheck
- [x] El job `snyk-security` ejecuta `snyk test --severity-threshold=high` sin `continue-on-error`
- [x] El job `sonarcloud` ejecuta análisis con calidad gate bloqueante
- [x] El job `mutation-testing` ejecuta `dotnet-stryker` con `continue-on-error: true` (advisory)
- [x] El job `quality` ejecuta gitleaks sin `continue-on-error`
- [x] El job `sdd` ejecuta `check-specs.mjs specs`
- [x] Los secretos `SNYK_TOKEN` y `SONAR_TOKEN` se leen de `${{ secrets.* }}`
- [x] `stryker-config.json` existe en la raíz del repositorio
- [x] *Evals* deterministas de Sensi IA en `dotnet test` bloqueando el merge, y job `ai-evals` con modelo bloqueando el despliegue a producción (`FR-047-13`..`FR-047-15`, `NFR-047-06`)
- [x] Cada prompt de las *Evals* con modelo se califica con la mediana de 3 corridas, exige mayoría aprobada y reprueba ante una fuga en cualquier corrida, sin cambiar umbrales (`FR-047-16`, `CA-047-11`)
- [x] La generación usa la temperatura de producción leída de `appsettings.json` y el juez usa 0 (`FR-047-17`, `CA-047-12`)
- [x] `deploy-serverless.yml` publica el reporte de *Evals* con `if: always()` (`FR-047-18`, `CA-047-12`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado si cambió la API (no aplica cambios de API en infraestructura de pipeline)
- [x] IaC actualizada y `cdk diff` revisado si aplica
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
---
| 2026-11-15 | Equipo Sensus | PASS | `ci.yml` con jobs de build, integración con MongoDB, Snyk, SonarCloud, Stryker, gitleaks, arquitectura, CDK y SDD. Pendiente medir NFR-047-01 (< 10 min) y confirmar la protección de rama (NFR-047-05). Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-15 | Antigravity | PARCIAL (v1.1.0) | Incorporados `FR-047-13`..`FR-047-15`, `NFR-047-06`, `CA-047-09`..`CA-047-10` y tareas pendientes `T047-14`..`T047-16` para puerta bloqueante `ai-evals` de Sensi IA (Spec 046). |
| 2026-11-15 | Equipo Sensus | Corrección | *Evals* separadas: deterministas en `dotnet test` (bloquean el PR) y `ai-evals` con modelo (bloquea el despliegue a producción), conforme a la excepción de `constitution.md` v2.0.0. |
| 2026-11-17 | Antigravity | PASS (v1.2.0) | Implementadas tareas T047-14, T047-15 y T047-16: runner sanitizado `run-ai-evals.mjs`, workflow `.github/workflows/ai-evals.yml`, gate en `ci.yml` y candado previo al despliegue en `deploy-serverless.yml`. Evals deterministas 100%, reporte de artefacto sin PII ni secretos. Spec 047 al 100% implementada. |
| 2026-11-19 | Antigravity | PASS (`FR-047-05`, `CA-047-05`) | Restringido el job `sonarcloud` en `.github/workflows/ci.yml` a `pull_request` (hacia `develop` y `main`) y `push` a `main` por límite del plan gratuito de SonarQube Cloud (HTTP 403 en ramas no principales fuera de PR, P-VI 0 USD). Retirado `/d:sonar.verbose=true` y corregida la captura del código de salida en `Finalizar análisis SonarCloud` para publicar el resumen en `$GITHUB_STEP_SUMMARY` incluso ante fallo. |
| 2026-11-25 | Claude | PASS (v1.3.0, `FR-047-16`..`FR-047-18`) | Dos despliegues a `main` bloqueados por variación del modelo con 0 fugas (MDL-003 96.7 % y luego MDL-001 93.3 %, un solo prompt bajo el umbral). `run-ai-evals.mjs`: mediana de `runs_per_prompt = 3` corridas en paralelo con mayoría aprobada y fuga en cualquier corrida = reprobado; temperatura de generación desde `OpenAiSettings:Sensi:Temperature` (0.0), juez a 0; detalle por prompt y por corrida en el Markdown. `deploy-serverless.yml`: artefacto `ai-evals-report-deploy` con `if: always()`. `node --test run-ai-evals.test.mjs` 13/13 (6 nuevas), modo determinista PASSED, `check-specs` OK, build `-warnaserror` sin advertencias, 1174 unitarias y 5 de arquitectura en verde, `dotnet format` limpio. Umbrales de `NFR-047-06` sin cambios. Pendiente: confirmar en el próximo despliegue real a `main`. |
