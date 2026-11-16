# Checklist 047 — Pipeline CI-CD y calidad de software (DevSecOps)

Alineado a `constitution.md` v2.1.0 · `AGENTS.md` v2.1.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 047 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 009, 010, 011, 012, 013
- [x] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El job `build-and-test` no tiene `services.mongo`
- [x] El job `integration-tests` tiene `services.mongo` con `mongo:7.0` y healthcheck
- [x] El job `snyk-security` ejecuta `snyk test --severity-threshold=high` sin `continue-on-error`
- [x] El job `sonarcloud` ejecuta análisis con calidad gate bloqueante
- [x] El job `mutation-testing` ejecuta `dotnet-stryker` con `continue-on-error: true` (advisory)
- [x] El job `quality` ejecuta gitleaks sin `continue-on-error`
- [x] El job `sdd` ejecuta `check-specs.mjs specs`
- [x] Los secretos `SNYK_TOKEN` y `SONAR_TOKEN` se leen de `${{ secrets.* }}`
- `stryker-config.json` existe en la raíz del repositorio
- [ ] *Evals* deterministas de Sensi IA en `dotnet test` bloqueando el merge, y job `ai-evals` con modelo bloqueando el despliegue a producción (`FR-047-13`..`FR-047-15`, `NFR-047-06`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [ ] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [ ] Contrato OpenAPI actualizado si cambió la API
- [x] IaC actualizada y `cdk diff` revisado si aplica
- [ ] Logs con correlation-id y sin PII
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
