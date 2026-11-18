# Tareas 047 — Pipeline CI-CD y calidad de software (DevSecOps)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado (`T047-01`..`T047-16` completadas) |
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

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- El pipeline no debe tener `continue-on-error: true` en ningún paso de seguridad, cobertura, formato, SDD o arquitectura.

---
