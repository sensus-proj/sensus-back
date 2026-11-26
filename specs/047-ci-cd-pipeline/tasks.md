# Tareas 047 — Pipeline CI-CD y calidad de software (DevSecOps)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado (`T047-01`..`T047-20` completadas) |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T047-01 | Reestructurar `ci.yml`: separar integración en job propio con `services.mongo` | DevOps | `FR-047-03` | — | Completada |
| T047-02 | Agregar job `snyk-security` con `snyk test --severity-threshold=high` bloqueante | DevOps | `FR-047-04` | T047-01 | Completada |
| T047-03 | Agregar job `sonarcloud` con calidad gate bloqueante | DevOps | `FR-047-05` | T047-01 | Completada |
| T047-04 | Agregar job `mutation-testing` con `dotnet-stryker` (advisory) | DevOps | `FR-047-06` | T047-01 | Completada |
| T047-05 | Crear `stryker-config.json` para mutación en `Sensus.Application.UnitTests` | DevOps | `FR-047-06` | — | Completada |
| T047-06 | Eliminar `continue-on-error: true` de cobertura y gitleaks | DevOps | `FR-047-07` | T047-01 | Completada |
| T047-07 | Verificar que secretos (`SNYK_TOKEN`, `SONAR_TOKEN`) usen `${{ secrets.* }}` | Seguridad | `FR-047-12` | T047-01 | Completada |
| T047-08 | Crear skill `quality-gates-sensus` con gates locales y CI | Documentación | `FR-047-08` | — | Completada |
| T047-09 | Actualizar `agents/security-reviewer.md` con checklist Snyk/SonarCloud | Documentación | `FR-047-04` | — | Completada |
| T047-10 | Actualizar `agents/test-qa.md` con comandos de gates CI | Documentación | `FR-047-08` | — | Completada |
| T047-11 | Ejecutar localmente: `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, `check-specs.mjs specs` | Verificación | `FR-047-01`, `FR-047-09`, `FR-047-10` | T047-01 | Completada |
| T047-12 | Actualizar `specs/README.md` y `specs/requirements.md` con spec 047 | Docs | `FR-047-01` | T047-11 | Completada |
| T047-13 | Registrar evidencias del Constitution Check | Docs | `FR-047-01` | T047-12 | Completada |
| T047-14 | Definir runner de *Evals* automatizados de Sensi IA (precisión Markdown, herramientas MCP, tono, inyecciones, redacción PII y *Hard Rules*) y script de evaluación de umbrales (`NFR-047-06`) | Tests / CI | `FR-047-13`, `FR-047-14`, `NFR-047-06` | T047-13 | Completada |
| T047-15 | Agregar job `ai-evals` (`workflow_dispatch` y `schedule`) que ejecuta las *Evals* con modelo y publica reporte sanitizado sin PII ni secretos; las *Evals* deterministas corren en `dotnet test` | CI | `FR-047-13`, `FR-047-15`, `NFR-047-05` | T047-14 | Completada |
| T047-16 | Validar que una *Evals* determinista fallida bloquea el merge y que `deploy-serverless.yml` no despliega sin una ejecución de `ai-evals` en verde | CI / Docs | `FR-047-14`, `NFR-047-05`, `NFR-047-06` | T047-15 | Completada |
| T047-17 | Calificar cada prompt con la mediana de `runs_per_prompt` corridas (mayoría aprobada, fuga en cualquier corrida reprueba) y declarar `runs_per_prompt: 3` en el manifiesto | Tests / CI | `FR-047-16` | T047-16 | Completada (`aggregatePromptRuns`, `runs_per_prompt: 3`) |
| T047-18 | Leer la temperatura de generación de `OpenAiSettings:Sensi:Temperature` y fijar el juez en 0 | Tests / CI | `FR-047-17` | T047-17 | Completada (`resolveGenerationTemperature`) |
| T047-19 | Publicar `artifacts/ai-evals` con `if: always()` en `deploy-serverless.yml` | CI | `FR-047-18` | — | Completada (paso `Publicar reporte sanitizado de Evals (FR-047-18)`) |
| T047-20 | Cubrir `FR-047-16` y `FR-047-17` en `run-ai-evals.test.mjs` con servidor OpenAI simulado (mediana, mayoría, fuga en una corrida, temperatura enviada) | Tests | `FR-047-16`, `FR-047-17` | T047-18 | Completada (pruebas 8 a 13, 13/13 en verde) |
| T047-21 | Integrar `Cdklabs.CdkNag` (`AwsSolutionsChecks`) en `infra/` con reconocimientos justificados (`G-03`) | IaC / Seguridad | `FR-047-19` | T047-20 | Completada en código (verificada localmente por `app.Synth()`, pendiente primer `cdk synth` de CI) |
| T047-22 | Generar artefacto SBOM CycloneDX (`6.3.2`) en `ci.yml` y `deploy-serverless.yml` y atestación SLSA 2 (`G-04`) | CI / Seguridad | `FR-047-20` | T047-20 | Completada en workflows (pendiente verificación de artefactos en primera corrida CI/CD) |
| T047-23 | Workflow DAST pasivo `.github/workflows/dast-zap.yml` y `.zap/rules.tsv` (`G-02`) usando OpenAPI generado localmente | CI / Seguridad | `FR-047-21` | T047-20 | Completada en código (pendiente primera ejecución diurna tras PR) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- El pipeline no debe tener `continue-on-error: true` en ningún paso de seguridad, cobertura, formato, SDD o arquitectura.

---
