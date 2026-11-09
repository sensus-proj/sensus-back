# Plan 045 — Infraestructura Serverless como Código (AWS CDK)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (Implementado) |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

---

## 1. Resumen técnico

Implementar la infraestructura serverless del backend Sensus utilizando **AWS CDK v2 en C#** dentro del directorio `infra/`, junto con el adaptador oficial `Amazon.Lambda.AspNetCoreServer.Hosting` en `Sensus.Api`. Para soportar `.NET 10` sin depender de contenedores ECR ni incurrir en costos adicionales, la función AWS Lambda se configura con runtime `PROVIDED_AL2023` (Amazon Linux 2023) y arquitectura `ARM_64` (Graviton2), ejecutando el binario compilado (`bootstrap`). El tráfico HTTPS ingresa por **AWS API Gateway v2 (`HttpApi`)** con integración proxy hacia la Lambda. Los secretos se referencian desde **AWS Systems Manager (SSM) Parameter Store** (nivel gratuito perpetuo) y la observabilidad se centraliza en **Amazon CloudWatch** con retención de 7 días y alarmas de errores y presupuesto.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Sin cambios |
| Application | Sin cambios |
| Infrastructure | Sin cambios |
| Api | Paquete `Amazon.Lambda.AspNetCoreServer.Hosting` y registro `builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi)` en `Program.cs` |
| IaC (`infra/`) | Proyecto independiente `infra/src/Sensus.Infra/Sensus.Infra.csproj`, `SensusInfraStack.cs`, `Program.cs` y configuración `infra/cdk.json` |

## 3. Modelo de datos e índices

No aplica: la persistencia reside en MongoDB Atlas (M0 Free Tier).

## 4. Seguridad

- **Mínimo privilegio IAM**: El rol de la función Lambda solo concede permisos sobre el `LogGroup` propio y lectura de parámetros bajo el prefijo `/sensus/<env>/*` en SSM Parameter Store.
- **Secretos sin costo**: Se utiliza SSM Parameter Store en lugar de AWS Secrets Manager ($0.40/secreto/mes), evitando costos fijos y manteniendo cero secretos en código o repositorio.
- **Transporte seguro**: API Gateway termina TLS 1.2+ de forma nativa con certificados gestionados por AWS.
- **CORS restringido**: Orígenes configurables por contexto de CDK, sin comodín `*` en producción.

## 5. Observabilidad y rendimiento

- **CloudWatch LogGroup**: `/aws/lambda/sensus-api-<env>` con retención estricta de 7 días (`RetentionDays.ONE_WEEK`).
- **Alarmas de CloudWatch**:
  - `SensusApi5xxAlarm`: Se activa cuando los errores 5xx superan el umbral en un periodo de 5 minutos.
  - `SensusLambdaErrorsAlarm`: Se activa ante fallos o *throttling* en la invocación de la Lambda.
- **Rendimiento ARM64**: La arquitectura `ARM_64` en Graviton2 ofrece mejor rendimiento precio/cómputo para cargas `.NET`.

## 6. Costo AWS

| Recurso | Servicio AWS | Consumo estimado | Límite Free Tier | Costo mensual estimado |
|---|---|---|---|---|
| API HTTP | Amazon API Gateway v2 | < 100k req/mes | 1,000,000 req/mes | **0.00 USD** |
| Cómputo API | AWS Lambda (`ARM_64`, 512 MB) | < 100k req/mes | 1M req + 400,000 GB-s/mes (perpetuo) | **0.00 USD** |
| Logs y Métricas | Amazon CloudWatch | < 500 MB (7 días retención) | 5 GB logs + 10 alarmas (perpetuo) | **0.00 USD** |
| Parámetros y Secretos | AWS SSM Parameter Store | < 15 parámetros estándar | 10,000 parámetros (perpetuo) | **0.00 USD** |
| Control presupuestario | AWS Budgets | 1 presupuesto mensual | 2 presupuestos gratuitos | **0.00 USD** |

- **Total estimado**: **0.00 USD / mes**.
- **Sin NAT Gateway ni VPC de pago**: Salida directa a internet para conectar con MongoDB Atlas, Cloudinary y OpenAI.

## 7. Estrategia de pruebas

- **Compilación estricta**: `dotnet build infra/src/Sensus.Infra/Sensus.Infra.csproj -warnaserror` con 0 advertencias.
- **Síntesis de CDK**: `cdk synth` desde `infra/` validado en local y en el job `iac` de `.github/workflows/ci.yml`.
- **Regresión completa**: Ejecución de las 201 pruebas de la solución (`sensus-back.sln`) para garantizar que `AddAWSLambdaHosting` es 100 % transparente para Kestrel y `WebApplicationFactory`.

## 8. Constitution Check

Evaluación previa y posterior a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | Spec 045 completa con 4 artefactos y requisitos `FR-045-01` a `FR-045-10` |
| II | PASS | `Domain` y `Application` intactos; el adaptador Lambda vive exclusivamente en `Sensus.Api` e `infra/` es un proyecto aislado |
| III | PASS | Contratos HTTP y ProblemDetails intactos a través del proxy HTTP API v2 |
| IV | PASS | Rol IAM de mínimo privilegio, secretos en SSM Parameter Store, TLS 1.2+ en API Gateway |
| V | PASS | Job `iac` en CI se activa automáticamente al detectar `infra/cdk.json`; 201 pruebas en verde |
| VI | PASS | Cierra la brecha del Principio VI: cómputo en Lambda detrás de API Gateway, costo 0 USD y presupuesto con alarma en AWS Budgets |
| VII | PASS | Logs en CloudWatch con retención de 7 días y alarmas de errores y throttling definidas en IaC |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- **Empaquetado .NET 10 para Lambda**: Dado que AWS Lambda utiliza `provided.al2023` para `.NET 10`, el comando de publicación debe generar el binario ejecutable llamado `bootstrap` (mediante `<AssemblyName>bootstrap</AssemblyName>` al publicar para Lambda o empaquetado con `dotnet lambda package`). El stack permite apuntar al directorio de artefactos publicados o a un placeholder válido durante `cdk synth` en CI.

---
