# Checklist 011 — Contrato OpenAPI

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 011 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 009, 010
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Un solo documento OpenAPI (`/openapi/v1.json`)
- [x] Todos los endpoints activos están documentados en la fuente única
- [x] Los endpoints protegidos declaran seguridad Bearer JWT

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas de integración añadidas y en verde (`OpenApiContractTests`)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Swagger UI apunta a `/openapi/v1.json` evitando doble generación
- [x] IaC sin cambios requeridos
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización declarada
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
| Pruebas de integración | `tests/Sensus.IntegrationTests/Contracts/OpenApiContractTests.cs` | ☑ |
| Validación CI | `.github/scripts/check-specs.mjs` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-21 | Beto (Sprint 5) | IMPLEMENTADO | Fuente única `Microsoft.AspNetCore.OpenApi` (`/openapi/v1.json`) con esquema Bearer JWT consumida por Swagger UI y validada en `OpenApiContractTests`. |
