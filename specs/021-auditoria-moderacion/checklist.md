# Checklist 021 — Auditoría de moderación

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 021 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 017, 020
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Los registros no se pueden modificar (append-only estricto en dominio y repositorio)
- [x] La consulta está paginada (20 por defecto, 100 máximo)
- [x] Solo administradores acceden (RBAC servidor verificado)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 90 % en Domain y Application
- [x] Contrato OpenAPI actualizado (v1.1.0)
- [x] IaC actualizada y `cdk diff` revisado (N/A — sin cambios en CDK IaC; almacenamiento en MongoDB Atlas Free Tier con índices gestionados por la aplicación)
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
- [x] **VIII. Nivel Fintech** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Moderation/Audit/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Endpoints/ModerationAuditEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-22 | Antigravity | Aprobado | Implementación append-only completada de punta a punta con pruebas y cobertura > 90% |
| 2026-11-26 | Claude | CERRADA | Casilla de IaC marcada como no aplicable (sin cambios en CDK; colección MongoDB con índices de la aplicación). Checklist sin casillas abiertas. |
