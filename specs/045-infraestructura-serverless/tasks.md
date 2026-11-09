# Tareas 045 — Infraestructura Serverless como Código (AWS CDK)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T045-01 | Añadir paquete `Amazon.Lambda.AspNetCoreServer.Hosting` a `Sensus.Api` | Api | `FR-045-01` | — | Completada |
| T045-02 | Registrar `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` en `Sensus.Api/Program.cs` | Api | `FR-045-01` | T045-01 | Completada |
| T045-03 | Crear proyecto `infra/src/Sensus.Infra/Sensus.Infra.csproj` con `Amazon.CDK.Lib` | IaC | `FR-045-02` | — | Completada |
| T045-04 | Crear configuración `infra/cdk.json` para síntesis y despliegue en C# | IaC | `FR-045-02` | T045-03 | Completada |
| T045-05 | Implementar `SensusInfraStack.cs` con función Lambda (`PROVIDED_AL2023`, `ARM_64`), LogGroup (7 días) y Rol IAM de mínimo privilegio (SSM + Logs) | IaC | `FR-045-03`, `FR-045-05`, `FR-045-08` | T045-03 | Completada |
| T045-06 | Configurar AWS API Gateway v2 (`CfnApi`, `CfnIntegration`, `CfnRoute`, `CfnStage`) con integración proxy hacia la Lambda y CORS | IaC | `FR-045-04`, `FR-045-10` | T045-05 | Completada |
| T045-07 | Definir alarmas de CloudWatch (errores 5xx y fallos/throttling de Lambda) y presupuesto mensual en AWS Budgets (1.00 USD) | IaC | `FR-045-06`, `FR-045-07` | T045-05 | Completada |
| T045-08 | Aplicar etiquetas obligatorias (`project`, `environment`, `owner`) en `Program.cs` de `Sensus.Infra` | IaC | `FR-045-09` | T045-05 | Completada |
| T045-09 | Verificar compilación de `Sensus.Infra`, regresión de las 201 pruebas de `sensus-back.sln` y validación de `.github/workflows/ci.yml` | Pruebas | `NFR-045-05` | T045-02, T045-08 | Completada |
| T045-10 | Actualizar `specs/README.md`, `specs/requirements.md` y `memory.md` | Docs | `FR-045-02` | T045-09 | Completada |

## Notas de ejecución

- El adaptador `Amazon.Lambda.AspNetCoreServer.Hosting` es un no-op cuando la aplicación no se ejecuta dentro del entorno de AWS Lambda (`AWS_LAMBDA_FUNCTION_NAME` ausente), preservando al 100 % la ejecución en Kestrel y `WebApplicationFactory`.
- La carpeta `infra/` se compila como aplicación independiente invocada por `cdk synth` mediante `cdk.json`.
