# ADR 0013 — Contadores atómicos de presupuesto y cuota de Sensi IA en Amazon DynamoDB

| Campo | Valor |
|---|---|
| Estado | Aceptada |
| Fecha | 2026-11-15 |
| Sustituye a | — (precisa la implementación de `EX-001` de la [ADR 0012](0012-sensi-ia-agentico-mcp-router-evals.md)) |
| Principios afectados | VI, VII |
| Specs relacionados | 049, 046, 048, 045 |

## Contexto

`AiBudgetGuard` (Spec 046, `EX-001`) usaba Redis para tres contadores atómicos: gasto mensual total,
gasto mensual del nivel *reasoning* y mensajes diarios por persona. El despliegue (`SensusInfraStack`)
fija `Redis__Enabled=false` y no existe cadena de conexión, por lo que `ICacheService.IsAvailable` es
`false`. El guardián opera *fail-closed* y toda petición a Sensi IA recibe la respuesta determinista
degradada, sin llegar nunca a OpenAI.

## Alternativas evaluadas

| Alternativa | Costo mensual estimado | Veredicto |
|---|---|---|
| Amazon ElastiCache (nodo o *Serverless*) | ≈ 6–15 USD + NAT Gateway ≈ 32 USD, porque la Lambda debe entrar a una VPC y seguir llegando a MongoDB Atlas, OpenAI y Cloudinary | Descartada: rompe el costo cero y el diseño sin VPC |
| Redis de un proveedor externo gratuito | 0 USD | Descartada: multi-nube prohibida por §2.2 |
| Contadores en MongoDB con `$inc` | 0 USD | Viable, pero acopla presupuesto con la persistencia de negocio |
| **Amazon DynamoDB (`PAY_PER_REQUEST`)** | **0 USD** (Free Tier permanente) | **Elegida** |

## Decisión

Se crea el contrato `IBudgetCounterStore` en `Sensus.Application` y su implementación
`DynamoDbBudgetCounterStore` en `Sensus.Infrastructure`. `AiBudgetGuard` deja de depender de
`ICacheService`.

- Tabla `sensus-sensi-budget-{env}` definida con AWS CDK: clave de partición `pk` (cadena),
  modo `PAY_PER_REQUEST`, TTL nativo sobre `expiresAt`, sin *Point-in-Time Recovery*, etiquetada con
  `project`, `environment` y `owner`.
- Incremento atómico con `UpdateItem` (`ADD`), lectura con `GetItem` consistente.
- Permiso IAM mínimo para el rol de la Lambda: `dynamodb:GetItem` y `dynamodb:UpdateItem` sobre esa tabla.
- Sin VPC: DynamoDB se alcanza por su *endpoint* regional con las credenciales del rol.
- Cualquier error, *timeout* o falta de configuración se traduce en `null` y el guardián responde
  *fail-closed* (degradación determinista), nunca en consumo sin techo.
- Redis queda como caché opcional de lecturas (Spec 048) y deja de ser requisito de Sensi IA.

## Consecuencias

- Sensi IA puede llegar a OpenAI en producción sin cambiar el modelo de costos ni crear una VPC.
- Costo mensual estimado: 0.00 USD. El uso son unas pocas operaciones por mensaje, muy por debajo del Free Tier.
- Se añade el paquete `AWSSDK.DynamoDBv2` (Apache-2.0) a `Sensus.Infrastructure`.
- Las pruebas contra DynamoDB Local con Testcontainers quedan pendientes de un entorno con Docker
  (ver `specs/049-contadores-presupuesto-dynamodb/checklist.md`).
