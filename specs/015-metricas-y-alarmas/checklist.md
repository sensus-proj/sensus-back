# Checklist 015 — Métricas y alarmas

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 015 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 014
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Cada alarma tiene umbral y destinatario
- [x] El panel mínimo está versionado (`SensusInfraStack.cs:434-450`)
- [x] El costo está dentro del Free Tier

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado (no aplica: telemetría y alarmas CloudWatch sin endpoint REST)
- [x] IaC actualizada y `cdk diff` revisado si aplica (`SensusInfraStack.cs` compilado y probado en `SensusInfraStackTests` 2/2 con `AwsSolutionsChecks`; revisión `cdk diff` con credenciales en §5.Z-1/§5.Z-11)
- [x] Logs con correlation-id y sin PII (`CorrelationIdMiddleware` Spec 014)
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS (`SensusInfraStack.cs` + `SensusInfraStackTests` 2/2)
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (`ALERTS_EMAIL` secreto en CI, SNS sin PII y CI `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/`, `infra/tests/Sensus.Infra.Tests/SensusInfraStackTests.cs` | ☑ |
| Contrato OpenAPI | No aplica (métricas/alarmas CloudWatch) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-14 | Claude (sesión de verificación) | Implementado | Métricas EMF `Sensus/Api` (`RequestLatency`, `ServerErrors`, `Throttled`; 3 de 10 gratuitas) con `RequestMetricsMiddleware`; 5 alarmas (10 gratuitas) con aviso por SNS/correo; panel `sensus-operations-<env>` (3 gratuitos). Síntesis CDK verificada fuera del repositorio. Brechas: `cdk diff` no ejecutado (sin CLI ni credenciales en la sesión); correlation-id depende de 014. `dotnet build -c Release` 0 errores y 0 advertencias; 613 pruebas en verde (4 arquitectura, 498 unitarias, 3 E2E, 108 integración); cobertura combinada Domain 90,2 % y Application 97,4 %. |
