# Spec 045 — Infraestructura Serverless como Código (AWS CDK)

| Campo | Valor |
|---|---|
| ID | 045 |
| Slug | `infraestructura-serverless` |
| Estado | Implementado |
| Ola | 7 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 009, 012, 014, 015, 016 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

---

## 1. Contexto y problema

El Principio VI de la constitución (`constitution.md`) y el ADR 0005 exigen que todo el backend de Sensus opere sobre servicios serverless de AWS dentro del nivel gratuito (meta de costo **0 USD**), y que todo recurso se aprovisione exclusivamente mediante Infraestructura como Código (IaC) con **AWS CDK en C#** dentro del directorio `infra/`. Durante el Sprint 1 se utilizó de manera táctica una instancia EC2 (Decisión D22) y el directorio `infra/` permanecía pendiente de creación. Esta especificación formaliza e implementa el soporte dual Kestrel/Lambda en `Sensus.Api` y el proyecto de AWS CDK en `infra/` para desplegar AWS Lambda detrás de AWS API Gateway (HTTP API v2), con observabilidad en Amazon CloudWatch, gestión de secretos en AWS Systems Manager (SSM) Parameter Store y alarma de presupuesto en AWS Budgets.

## 2. Alcance

**Incluye**

- Adaptador de ejecución dual en `Sensus.Api` mediante `Amazon.Lambda.AspNetCoreServer.Hosting` (`LambdaEventSource.HttpApi`), manteniendo compatibilidad 100 % con Kestrel en desarrollo local y EC2
- Proyecto AWS CDK en C# en `infra/src/Sensus.Infra/Sensus.Infra.csproj` y configuración `infra/cdk.json`
- Función AWS Lambda con runtime `provided.al2023` (Amazon Linux 2023) y arquitectura `ARM_64` (Graviton2) para ejecutar binarios `.NET 10` self-contained (`bootstrap`)
- AWS API Gateway v2 (`HttpApi`) con integración proxy hacia la Lambda (`$default`), CORS configurable y terminación TLS automática
- Grupo de logs en Amazon CloudWatch (`/aws/lambda/sensus-api`) con retención de 7 días para cumplir con el límite gratuito de 5 GB
- Alarma de errores 5xx y alarma de *throttling* en CloudWatch
- Alarma de presupuesto mensual en AWS Budgets con umbral de 1.00 USD para garantizar la meta de costo cero
- Lectura de configuraciones sensibles desde AWS Systems Manager (SSM) Parameter Store (100 % gratuito, evitando el cobro de Secrets Manager)
- Etiquetado obligatorio (`project`, `environment`, `owner`) en todos los recursos del stack
- Validación automática de `cdk synth` y `cdk diff` en el job `iac` de `.github/workflows/ci.yml`

**No incluye**

- Instancias EC2, ECS, EKS o contenedores permanentes en producción
- NAT Gateways o VPCs con costo fijo mensual (la Lambda utiliza salida directa administrada hacia MongoDB Atlas, Cloudinary y OpenAI)
- AWS Secrets Manager (sustituido por SSM Parameter Store para mantener costo 0 USD)
- Ejecución automática de `cdk deploy` sin autorización explícita del responsable del proyecto

## 3. Actores

- Desarrollador / Operador DevSecOps (sintetiza y audita cambios de infraestructura en PR)
- Pipeline CI/CD (ejecuta `cdk synth` y `cdk diff` automáticamente)
- Cliente de la API (consume endpoints HTTPS a través de API Gateway + Lambda)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-045-01 | Habilitar hospedaje dual en `Sensus.Api` con `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` sin alterar la ejecución local en Kestrel | Must |
| FR-045-02 | Crear el proyecto AWS CDK en C# dentro de `infra/` con `cdk.json` funcional | Must |
| FR-045-03 | Definir la función AWS Lambda con runtime `PROVIDED_AL2023`, arquitectura `ARM_64`, memoria configurable (512 MB por defecto) y timeout de 29 segundos | Must |
| FR-045-04 | Definir AWS API Gateway HTTP API v2 con ruta `$default` integrada a la función Lambda y configuración CORS explícita | Must |
| FR-045-05 | Definir `LogGroup` de Amazon CloudWatch con retención de 7 días y política de eliminación controlada | Must |
| FR-045-06 | Definir alarmas de CloudWatch para errores 5xx de API Gateway y errores/throttling de la función Lambda | Must |
| FR-045-07 | Definir presupuesto en AWS Budgets (`CfnBudget`) con límite mensual de 1.00 USD y alerta al superar el 80 % | Must |
| FR-045-08 | Configurar rol IAM de mínimo privilegio para la función Lambda (únicamente escritura en CloudWatch Logs y lectura de parámetros SSM de Sensus) | Must |
| FR-045-09 | Aplicar etiquetas obligatorias (`project=sensus`, `environment`, `owner=sensus-team`) a todos los recursos del stack | Must |
| FR-045-10 | Exponer como `CfnOutput` la URL pública del API Gateway y el ARN de la función Lambda | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-045-01 | Costo mensual de infraestructura en AWS | 0.00 USD (100 % dentro del Free Tier perpetuo/anual) |
| NFR-045-02 | Tiempo de síntesis (`cdk synth`) en CI | < 60 segundos |
| NFR-045-03 | Retención de logs en CloudWatch | 7 días (< 5 GB acumulados) |
| NFR-045-04 | Privilegios IAM | Mínimo privilegio estricto (cero comodines `*` sobre recursos sensibles) |
| NFR-045-05 | Compatibilidad con pruebas existentes | 100 % de las pruebas unitarias, de integración, E2E y arquitectura en verde |

## 6. Criterios de aceptación

### CA-045-01: Ejecución dual Kestrel y Lambda
- **Dado**: el proyecto `Sensus.Api` con `Amazon.Lambda.AspNetCoreServer.Hosting`
- **Cuando**: se ejecuta localmente o en las pruebas de integración/E2E con `WebApplicationFactory`
- **Entonces**: inicia normalmente en Kestrel y el 100 % de las pruebas pasan sin errores

### CA-045-02: Síntesis de plantilla CloudFormation (`cdk synth`)
- **Dado**: el directorio `infra/` con `cdk.json` y `Sensus.Infra.csproj`
- **Cuando**: se compila el proyecto de infraestructura y se sintetiza el stack `SensusInfraStack`
- **Entonces**: genera una plantilla CloudFormation válida con la función Lambda, HTTP API, LogGroup, alarmas y presupuesto sin errores ni warnings

### CA-045-03: Cumplimiento de Costo Cero y Free Tier
- **Dado**: la plantilla sintetizada por `SensusInfraStack`
- **Cuando**: se inspeccionan los recursos creados
- **Entonces**: no incluye instancias EC2, NAT Gateways, Load Balancers ni Secrets Manager con costo fijo

### CA-045-04: Etiquetado normativo
- **Dado**: el stack `SensusInfraStack`
- **Cuando**: se sintetizan los recursos
- **Entonces**: el stack aplica las etiquetas `project`, `environment` y `owner`

## 7. Contratos

No modifica contratos de negocio de los endpoints RESTful; provee la capa de transporte y cómputo serverless en AWS (`https://<api-id>.execute-api.<region>.amazonaws.com`).

## 8. Exclusiones

- No aprovisiona bases de datos en AWS (se utiliza MongoDB Atlas M0 Free Tier).
- No aprovisiona S3 ni CloudFront para multimedia (se utiliza Cloudinary Free Tier según ADR 0010).
- No aprovisiona ElastiCache en AWS (el caché Redis utiliza instancia externa o degradación graceful sin costo fijo).

---
