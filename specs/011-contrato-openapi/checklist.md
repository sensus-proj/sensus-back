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
| 2026-11-21 | Beto (Sprint 5) | IMPLEMENTADO | Fuente única OpenAPI (`/openapi/v1.json`) servida por Swagger / Swagger UI con esquema Bearer JWT y validada en `OpenApiContractTests`. |
| 2026-11-26 | Claude | CERRADA | Estado de `spec.md` alineado a `tasks.md` (todas completadas) y al checklist sin casillas abiertas. Evidencia: `OpenApiContractTests` en `tests/Sensus.IntegrationTests/Contracts` y documento único `/openapi/v1.json` con esquema Bearer JWT. |

| 2026-11-26 | Claude | IMPLEMENTADO (`FR-011-05`, G-15) | `Program.cs` usaba `IsDevelopment() || isLambdaRuntime` para Swagger, así que producción publicaba `/swagger` y `/openapi/v1.json` y, además, nunca aplicaba HSTS (estaba en el `else`). Nuevo `HostingSecurityPolicy`: docs solo en Development, HSTS también en Lambda, sin redirección HTTPS en Lambda (API Gateway solo acepta HTTPS). Pruebas: `ProductionEnvironment_DoesNotExposeApiDocs` (2 casos, 404) y `HostingSecurityPolicy_ResolvesPipelineByEnvironmentAndLambda` (4 casos); `CorsAndHttpsSecurityTests` + `OpenApiContractTests` 12/12 en verde. Pendiente: confirmar en producción tras desplegar (`/swagger/index.html` → 404 y cabecera `Strict-Transport-Security`). |