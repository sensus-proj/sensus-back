# Checklist 013 — Control de tasa de peticiones

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 013 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 012
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El 429 incluye Retry-After
- [x] Los umbrales están en configuración
- [x] El login tiene límite más estricto

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde (`RateLimitingTests`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado (no aplica: política transversal HTTP `429` reflejada en endpoints de catálogo)
- [x] IaC actualizada y `cdk diff` revisado si aplica (`DefaultRouteSettings` 50/100 en `SensusInfraStack.cs`; `SensusInfraStackTests` 2/2)
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
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (`SecurityExtensions.AddRateLimiter`, `DefaultRouteSettings` 50/100 y CI `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Security/RateLimitingTests.cs` | ☑ |
| Contrato OpenAPI | No aplica (transversal; `429` documentado por endpoint) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | Límites por IP con políticas `auth-strict`, `recover`, `read-heavy` y `sensitive-write`. Faltan límite por usuario, cabecera Retry-After y umbrales en configuración. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-15 | Antigravity | PASS | Umbrales externalizados a `RateLimitingSettings` en `appsettings.json`, límites por usuario autenticado y por IP, cabecera `Retry-After` con respuesta ProblemDetails (429) y pruebas de integración en `RateLimitingTests.cs`. |
