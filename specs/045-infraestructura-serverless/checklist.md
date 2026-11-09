# Checklist 045 — Infraestructura Serverless como Código (AWS CDK)

Alineado a `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 045 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador (`FR-045-01` a `FR-045-10`, `NFR-045-01` a `NFR-045-05`) y criterio medible
- [x] Las dependencias están resueltas
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo (meta 0 USD) y rendimiento fue evaluado

## B. Específicos de este spec

- [x] `Amazon.Lambda.AspNetCoreServer.Hosting` registrado en `Sensus.Api/Program.cs` con `LambdaEventSource.HttpApi`
- [x] Proyecto `infra/src/Sensus.Infra/Sensus.Infra.csproj` creado y compilando sin warnings
- [x] `infra/cdk.json` creado para activar el job `iac` en GitHub Actions
- [x] Función AWS Lambda configurada con runtime `PROVIDED_AL2023` (Amazon Linux 2023) y arquitectura `ARM_64`
- [x] AWS API Gateway v2 (`HttpApi`) configurado con integración proxy hacia la Lambda y permiso de invocación
- [x] `LogGroup` de CloudWatch configurado con retención de 7 días
- [x] Alarmas de CloudWatch definidas para errores 5xx, errores de Lambda y throttling
- [x] Presupuesto en AWS Budgets (`CfnBudget`) definido con umbral de 1.00 USD
- [x] Rol IAM de mínimo privilegio con acceso a SSM Parameter Store (sin costo de Secrets Manager) y sin NAT Gateway
- [x] Etiquetas normativas (`project`, `environment`, `owner`) aplicadas al stack

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4 pruebas de arquitectura superadas)
- [x] Suite completa de pruebas de la solución en verde (201 pruebas superadas)
- [x] `infra/src/Sensus.Infra/Sensus.Infra.csproj` compila con `-warnaserror` sin advertencias
- [x] `dotnet format sensus-back.sln --verify-no-changes` verificado en verde
- [x] `node .github/scripts/check-specs.mjs specs` ejecutado y válido (47 specs conformes)
- [x] Revisión de seguridad: cero secretos hardcodeados en el código de infraestructura
- [x] Constitution Check en PASS

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS (Spec 045 con 4 artefactos completos)
- [x] **II. Clean Architecture** — PASS (Aislamiento total de `Domain` y `Application`)
- [x] **III. CQRS y contratos** — PASS (Proxy transparente HTTP API v2)
- [x] **IV. Seguridad y privacidad** — PASS (Mínimo privilegio IAM, SSM Parameter Store gratuito, TLS 1.2+)
- [x] **V. Verificación automatizada** — PASS (Job `iac` activo al existir `infra/cdk.json`, 201 tests en verde)
- [x] **VI. Costo cero e IaC** — PASS (Cumple 100 % con Lambda + API Gateway + Budgets 0 USD en CDK C#)
- [x] **VII. Rendimiento y observabilidad** — PASS (Logs en CloudWatch con retención 7 días y alarmas en código)
