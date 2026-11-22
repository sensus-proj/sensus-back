# Checklist 040 — Reportes de contenido

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 040 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas o desacopladas (`IReportTargetService` sobre `offers`, `feed_posts`, `feed_comments`)
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Ningún reporte legalmente excluido (CSAM / abuso infantil) se persiste (`ContentReportPolicy.IsProhibitedLegalContent`)
- [x] La identidad del reportante queda protegida (sin PII en DTO ni logs)
- [x] El umbral de revisión (`ContentReports:AutoReviewThreshold = 3`) está probado
- [x] No se puede reportar dos veces el mismo objetivo en 24 h (`409 Conflict` + índice único `ux_content_reports_dedup`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml`)
- [x] IaC sin cambios requeridos
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Reports/ContentReportTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Reports/ContentReportEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-21 | Beto (Sprint 5) | IMPLEMENTADO | Soporta objetivos `offer`, `post` y `comment`, exclusión legal sin persistencia, deduplicación 24 h y bandera de revisión automática configurada. |
