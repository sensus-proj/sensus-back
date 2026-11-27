# Checklist 028 — Webhooks entrantes (backlog)

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 028 está aprobada y sin preguntas bloqueantes (backlog)
- [ ] Los requisitos tienen identificador y criterio medible (backlog)
- [ ] Las dependencias están resueltas: 009, 014 (backlog)
- [ ] El alcance y las exclusiones están declarados (backlog)
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado (backlog)

## B. Específicos de este spec

- [ ] La spec permanece en backlog hasta que exista un proveedor (backlog)
- [ ] No se implementa sin decisión de producto (backlog)
- [ ] No aparece en el catálogo de endpoints vigente (backlog)

## C. Antes de entregar (Definition of Done)

- [ ] Código en la capa correcta y test de arquitectura en verde (backlog)
- [ ] Pruebas unitarias y de integración añadidas y en verde (backlog)
- [ ] Cobertura ≥ 80 % en Domain y Application (backlog)
- [ ] Contrato OpenAPI actualizado (backlog)
- [ ] IaC actualizada y `cdk diff` revisado si aplica (backlog)
- [ ] Logs con correlation-id y sin PII (backlog)
- [ ] Revisión de seguridad: sin secretos, autorización verificada (backlog)
- [ ] Constitution Check en PASS o brecha registrada (backlog)

## D. Constitution Check

- [ ] **I. Especificación y trazabilidad** — PASS (backlog)
- [ ] **II. Clean Architecture** — PASS (backlog)
- [ ] **III. CQRS y contratos** — PASS (backlog)
- [ ] **IV. Seguridad y privacidad** — PASS (backlog)
- [ ] **V. Verificación automatizada** — PASS (backlog)
- [ ] **VI. Costo cero e IaC** — PASS (backlog)
- [ ] **VII. Rendimiento y observabilidad** — PASS (backlog)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☐ |
| Plan | [`plan.md`](plan.md) | ☐ |
| Tareas | [`tasks.md`](tasks.md) | ☐ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☐ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☐ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| — | — | — | — |
