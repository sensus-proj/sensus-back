# Tareas 049 — Contadores de presupuesto y cuota de Sensi IA en Amazon DynamoDB

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Backend completado |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T049-01 | Enmienda v2.2.0, ADR 0013 y alineación de `AGENTS.md` | Docs | FR-049-04 | — | Hecha |
| T049-02 | Crear `IBudgetCounterStore` | Application | FR-049-01 | T049-01 | Hecha |
| T049-03 | Implementar `DynamoDbBudgetCounterStore` y `AiBudgetStoreSettings` | Infrastructure | FR-049-02, FR-049-06, FR-049-07 | T049-02 | Hecha |
| T049-04 | Refactorizar `AiBudgetGuard` a `IBudgetCounterStore` | Infrastructure | FR-049-03 | T049-02 | Hecha |
| T049-05 | Registrar DI y sección `AiBudgetStore` en `appsettings.json` | Api / Infrastructure | FR-049-06 | T049-03 | Hecha |
| T049-06 | Tabla DynamoDB, IAM mínimo y variable de entorno en CDK | infra | FR-049-04, FR-049-05 | T049-01 | Hecha |
| T049-07 | Pruebas unitarias de `AiBudgetGuard` | Tests | FR-049-03 | T049-04 | Hecha |
| T049-08 | Pruebas de `DynamoDbBudgetCounterStore` con doble del SDK | Tests | FR-049-02, FR-049-07 | T049-03 | Hecha |
| T049-09 | Pruebas con DynamoDB Local (CI / Testcontainers) | Tests | FR-049-02, FR-049-07 | T049-08 | Completada |
| T049-10 | `cdk synth`/`cdk diff` y verificación posterior al despliegue | infra | FR-049-04 | T049-06 | Parcial (stack/tabla desplegados por CI; pendiente prueba en vivo de un turno Sensi) |
| T049-11 | Verificar el guardián de cuota contra DynamoDB Local y bloquear contadores inválidos | Infrastructure / Pruebas | FR-049-03, FR-049-07 | T049-09 | Verificada en DynamoDB Local |

## Notas de ejecución

`cdk deploy` no se ejecuta localmente sin autorización explícita del responsable; el despliegue de producción se ejecuta por CI/CD (`deploy-serverless.yml`, corridas en `main` incluyendo `44610076245`). Las pruebas de `DynamoDbBudgetCounterStoreIntegrationTests` y `DynamoDbLocalBudgetCounterStoreTests` (atomicidad bajo concurrencia, expiración, guardián de cuota y TTL `expiresAt`) compilan en solución y se ejecutan contra `amazon/dynamodb-local:2.6.1`. Queda pendiente únicamente validar end-to-end en producción un turno real de Sensi incrementando el contador DynamoDB y medir el p95 desde Lambda.
