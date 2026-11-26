# Plan 045 — Infraestructura Serverless como Código (AWS CDK)

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (Implementado) |
| Versión | 1.1.0 |
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

### 2.1 Prueba de humo y rollback tras desplegar (v1.1.0, `FR-045-16`, `FR-045-17`)

**Motivo.** El 2026-11-25 el despliegue de `25c5ba2` terminó en verde mientras la API respondía 500 en todas las rutas: la Lambda fallaba al arrancar (`OptionsValidationException` por un parámetro ausente en SSM) y CloudFormation no invoca la función, así que nada lo detectó. La caída duró hasta que se probó a mano.

**Flujo en `deploy-serverless.yml`:**

1. *Capturar versión en producción*: `aws lambda get-function` obtiene `CodeSha256` y la URL temporal del paquete actual; se descarga y se copia a `s3://cdk-hnb659fds-assets-<cuenta>-<región>/rollback/sensus-api-<entorno>/<sha>.zip`. El paquete pesa unos 55 MB, por encima del límite de 50 MB de carga directa, por eso pasa por S3. Si la función no existe, se marca que no hay rollback.
2. `cdk deploy` sin cambios.
3. *Prueba de humo*: si la concurrencia reservada es `0` (pausa nocturna, Spec 051) se omite con aviso. Si no, `node .github/scripts/post-deploy-smoke.mjs --base-url <ApiGatewayUrl>` comprueba `/health/live` → 200, `/health/ready` → 200 y `/api/v1/offers/nearby` sin token → 401; hasta 6 intentos con 10 s de espera y 10 s de tiempo límite por solicitud. Solo registra rutas y códigos, nunca cuerpos.
4. *Rollback* (solo si falló la prueba y hay paquete previo): `aws lambda update-function-code --s3-bucket … --s3-key …`, `aws lambda wait function-updated`, verificación de `CodeSha256` y nueva prueba de humo. El job termina en rojo siempre que la primera prueba falle.

**Alcance del rollback.** Restaura solo el código de la Lambda. Si la causa es de infraestructura (variables, IAM, red), la segunda prueba seguirá fallando y el resumen lo indica. El siguiente `cdk deploy` vuelve a alinear la función con la plantilla, porque CloudFormation reaplica su propio paquete.

**Seguridad y costo.** Usa las credenciales del job de despliegue; no agrega secretos. Los paquetes de rollback ocupan menos de 60 MB cada uno en el bucket de *assets* (dentro de los 5 GB gratuitos de S3); conviene una regla de expiración en el bucket de *bootstrap*, que no gestiona este stack. Costo: 0 USD.

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
- **Correo de alertas (FR-045-12 a FR-045-15, T045-13 a T045-15)** — corrección tras el despliegue fallido del 2026-11-20:
  - `ALERTS_EMAIL` pasa de variable a **secreto** de GitHub: los secretos se enmascaran (`***`) en los logs; las variables no.
    El repositorio es público y el correo se imprimía en claro en cada paso del job.
  - `cdk synth --quiet`: la plantilla completa (con el correo, el ID de cuenta y los orígenes CORS) dejaba de imprimirse en el log.
  - La suscripción de correo del tema SNS se crea con el identificador fijo `SensusAlarmEmailSubscription`; antes CDK derivaba
    el identificador lógico del propio correo.
  - El presupuesto notifica al tema SNS (`SubscriptionType: SNS`) en lugar de a un correo directo. En CloudFormation, cambiar
    los suscriptores de un `AWS::Budgets::Budget` obliga a recrearlo, y con nombre fijo AWS rechaza el duplicado. El tema
    recibe una política que permite publicar solo a `budgets.amazonaws.com` desde esta cuenta (`aws:SourceAccount`).
  - El nombre pasa a `sensus-zero-cost-budget-<entorno>-v2` para que el reemplazo de esta única vez no choque con el existente.
  - El workflow falla antes de desplegar si `ALERTS_EMAIL` está vacío; el CDK ya no usa `alerts@sensus.local` como relleno.
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
