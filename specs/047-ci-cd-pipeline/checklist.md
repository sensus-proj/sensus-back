# Checklist 047 — Pipeline CI-CD y calidad de software (DevSecOps)

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

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
