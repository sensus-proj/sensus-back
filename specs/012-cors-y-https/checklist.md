# Checklist 012 — CORS y HTTPS por entorno

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 012 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: Ninguna
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Producción no usa AllowAnyOrigin
- [x] HTTPS es obligatorio fuera de desarrollo
- [x] Los orígenes están en configuración, no en código

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (middleware HTTP global sin endpoints de negocio)
- [x] IaC actualizada y `cdk diff` revisado si aplica
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS (`CorsAndHttpsSecurityTests.cs`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Security/CorsAndHttpsSecurityTests.cs` | ☑ |
| Contrato OpenAPI | N/A (Middleware HTTP transversal) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | CORS por `Cors:AllowedOrigins`, sin AllowAnyOrigin; HSTS y redirección HTTPS fuera de Development (`Program.cs`). Falta T012-05. |
| 2026-11-23 | Equipo Sensus | COMPLETADO | T012-05 implementada en `CorsAndHttpsSecurityTests.cs` (5/5 pruebas en verde: preflight permitido y denegado, HSTS en Prod, sin HSTS en Dev, redirect HTTP->HTTPS). Checklist cerrado al 100%. |
