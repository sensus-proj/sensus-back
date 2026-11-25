# Plan 047 — Pipeline CI-CD y calidad de software (DevSecOps)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Implementado |
| Versión | 1.3.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

---

## 1. Resumen técnico

Implementar y corregir el pipeline CI-CD de GitHub Actions para cumplir con los requisitos DevSecOps del profesor y los principios de la constitución v1.2.0. El pipeline ejecuta compilación, pruebas unitarias, pruebas de integración con MongoDB real, seguridad (Snyk + gitleaks), calidad de código (SonarCloud), mutación (Stryker.NET), formato, cobertura y trazabilidad SDD, todos con gates bloqueantes.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| `.github/workflows/ci.yml` | Reestructurar: separar integración en job propio con `services.mongo`, agregar Snyk, SonarCloud, Stryker.NET, eliminar `continue-on-error` de gates críticos |
| `stryker-config.json` | Crear configuración de pruebas de mutación para `Sensus.Application.UnitTests` |
| `.opencode/skills/quality-gates-sensus/SKILL.md` | Documentar gates locales y CI |
| `.opencode/agents/security-reviewer.md` | Agregar checklist de Snyk/SonarCloud |
| `.opencode/agents/test-qa.md` | Agregar comandos de gates CI |
| `.github/workflows/ci.yml` + suite de *Evals* IA | *Evals* deterministas dentro de `dotnet test` (bloquean el PR) y job `ai-evals` con `workflow_dispatch` y `schedule` que llama al proveedor real; `deploy-serverless.yml` exige su última ejecución en verde. Umbrales `NFR-047-06`, sin PII ni secretos en logs |
| `.github/scripts/run-ai-evals.mjs` (v1.3.0) | `FR-047-16`: cada prompt corre `runs_per_prompt` veces en paralelo; se reporta la mediana, las corridas aprobadas y el detalle de cada corrida. `FR-047-17`: la temperatura de generación se lee de `OpenAiSettings:Sensi:Temperature` en `src/Sensus.Api/appsettings.json` (respaldo `0`); el juez usa `0` |
| `specs/046-asistente-sensi-ia/evals-manifest.json` | Nuevo campo `runs_per_prompt: 3` (versión 2.1.0) |
| `.github/workflows/deploy-serverless.yml` | `FR-047-18`: paso `Publicar reporte de Evals` con `actions/upload-artifact` fijado por SHA e `if: always()` |

### 2.1 Motivo del cambio v1.3.0

El 2026-11-25 dos despliegues consecutivos a producción quedaron bloqueados por variación del modelo, no por regresiones: en un intento MDL-003 promedió 96.7 % y en el siguiente MDL-001 promedió 93.3 %, en ambos casos con 0 fugas. Bastaba que el juez calificara 0.8–0.9 un solo prompt para reprobar el caso completo. La generación corría a `temperature: 0.2` mientras producción no fijaba temperatura, y el reporte no se publicaba en el despliegue. La mediana de 3 corridas reduce el ruido sin bajar ningún umbral (AGENTS.md §11).

Costo: 8 prompts × 3 corridas × 2 llamadas `gpt-4o-mini` ≈ 48 llamadas por despliegue, menos de 0.01 USD; dentro de `EX-001`. Tiempo: las corridas de un prompt van en paralelo, el paso sigue por debajo de 2 minutos.

## 3. Modelo de datos e índices

No aplica: pipeline de infraestructura de desarrollo, sin persistencia propia.

## 4. Seguridad

- `SNYK_TOKEN` y `SONAR_TOKEN` como GitHub Secrets (nunca en YAML)
- Gitleaks bloqueante para detectar secretos versionados
- Snyk bloqueante para vulnerabilidades ≥ high
- SonarCloud calidad gate para code smells y security hotspots
- Logs sin PII, sin tokens, sin contenido moderado

## 5. Observabilidad y rendimiento

- Reporte de cobertura publicado como artifact (14 días de retención)
- Resultados de pruebas (trx) publicados
- Tiempo total objetivo: < 10 minutos

## 6. Costo AWS

Sin costo incremental: GitHub Actions (público), SonarCloud (plan gratuito), Snyk (plan gratuito para repos públicos), CDK sin costo en Free Tier.

## 7. Estrategia de pruebas

- **Unitarias**: `dotnet test` con filtro excluyendo `Integration`
- **Integración**: `tests/Sensus.IntegrationTests` contra MongoDB real vía Docker service
- **Arquitectura**: `Sensus.ArchitectureTests` con NetArchTest
- **Mutación**: `dotnet-stryker` sobre `Sensus.Application.UnitTests` (advisory)
- **Cobertura**: Coverlet con umbral ≥ 80 % en Domain y Application

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | Requisitos `FR-047-01` a `FR-047-18` y `NFR-047-01` a `NFR-047-06` con criterios de aceptación medibles |
| II | PASS | Cambios acotados a `.github/`, `.opencode/` y `stryker-config.json` |
| III | PASS | No se exponen entidades de dominio; pipeline es infraestructura de desarrollo |
| IV | PASS | Snyk, gitleaks, SonarCloud, secretos en GitHub Secrets |
| V | PASS | Gates bloqueantes para cobertura, seguridad, formato, SDD, arquitectura. La mediana de corridas (`FR-047-16`) conserva los umbrales de `NFR-047-06` y el fallo cerrado ante fugas |
| VI | PASS | Costo 0 USD con Free Tier; IaC con CDK en `infra/` (pendiente de crear) |
| VII | PASS | Cobertura, métricas de pipeline, reporte de pruebas |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- El contenedor MongoDB en GitHub Actions puede fallar si el runner no tiene Docker habilitado (se mitigó usando `mongo:7.0` con healthcheck).
- Snyk y SonarCloud requieren secrets configurados en el repositorio de GitHub antes de que el pipeline pase.
- `dotnet-stryker` como herramienta global puede tardar en instalarse en el primer run.

---
