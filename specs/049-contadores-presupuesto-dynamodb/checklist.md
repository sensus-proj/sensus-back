# Checklist 049 — Contadores de presupuesto y cuota de Sensi IA en Amazon DynamoDB

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] Spec con requisitos medibles y criterios de aceptación
- [x] Enmienda v2.2.0 y ADR 0013 registrados
- [x] Costo estimado declarado (0.00 USD)

## B. Específicos de este spec

- [x] `IBudgetCounterStore` en Application y SDK solo en Infrastructure
- [x] Tabla con TTL, `PAY_PER_REQUEST` y etiquetas en CDK
- [x] IAM limitado a `GetItem` y `UpdateItem` sobre la tabla
- [x] *Fail-closed* probado (error, *timeout*, sin configuración)
- [ ] Pruebas con DynamoDB Local (T049-09)

## C. Antes de entregar (Definition of Done)

- [x] Build con `-warnaserror` y `dotnet format` limpios
- [ ] Pruebas en verde (864) y cobertura ≥ 80 % en Domain y Application — pruebas en verde; cobertura no medida en esta sesión
- [ ] `cdk synth` y `cdk diff` revisados — `synth` revisado; `diff` pendiente (requiere credenciales de AWS)
- [x] `tasks.md` refleja el estado real
- [x] `memory.md` actualizado

## D. Constitution Check

- [x] Resultado registrado en `plan.md` §8

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ (613 en verde, sin cambios propios) |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Ai/` | ☑ (`AiBudgetGuardTests`, `DynamoDbBudgetCounterStoreTests`) |
| Contrato OpenAPI | No aplica (sin endpoints) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Claude (sesión) | Parcial | Enmienda v2.2.0, ADR 0013, código, CDK y pruebas (864 en verde, build y `format` limpios). Pendiente: DynamoDB Local (T049-09), `cdk diff`, `cdk deploy` autorizado y verificación en producción |
