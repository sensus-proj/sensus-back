# Checklist 049 — Contadores de presupuesto y cuota de Sensi IA en Amazon DynamoDB

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

El backend está completado. Quedan las comprobaciones en AWS y los escaneos de CI para el cierre formal.

## A. Antes de empezar (Definition of Ready)

- [x] Spec con requisitos medibles y criterios de aceptación
- [x] Enmienda v2.2.0 y ADR 0013 registrados
- [x] Costo estimado declarado (0.00 USD)

## B. Específicos de este spec

- [x] `IBudgetCounterStore` en Application y SDK solo en Infrastructure
- [x] Tabla con TTL, `PAY_PER_REQUEST` y etiquetas en CDK
- [x] IAM limitado a `GetItem` y `UpdateItem` sobre la tabla
- [x] *Fail-closed* probado (error, *timeout*, sin configuración)
- [x] Pruebas con DynamoDB Local (`DynamoDbBudgetCounterStoreIntegrationTests` + `DynamoDbLocalBudgetCounterStoreTests` + `ci.yml` con `amazon/dynamodb-local:2.6.1`) (T049-09)
- [x] El guardián registra gasto y cuota en DynamoDB Local, aplica los topes y bloquea ante registros inválidos (T049-11)

## C. Antes de entregar (Definition of Done)

- [x] Build con `-warnaserror` y `dotnet format` limpios
- [x] Pruebas en verde y cobertura ≥ 80 % en Domain y Application
- [ ] `cdk synth` y `cdk diff` revisados — `synth` revisado; `diff` pendiente (requiere credenciales de AWS)
- [x] Síntesis local de este árbol verificada. La plantilla incluye DynamoDB con clave `pk`, TTL `expiresAt`, `PAY_PER_REQUEST` y permisos `GetItem` y `UpdateItem` limitados a la tabla.
- [ ] Turno de Sensi en el entorno desplegado con incremento real observado en DynamoDB y p95 desde Lambda
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
| 2026-11-19 | Antigravity | Parcial (T049-09 cerrada) | Implementada `DynamoDbBudgetCounterStoreIntegrationTests` (concurrencia atómica, expiración y TTL `expiresAt`) cableada a `amazon/dynamodb-local:2.6.1` con digest SHA-256 en `.github/workflows/ci.yml` (`T049-09` completada). Verificado: build Release `-warnaserror` 0 advertencias/errores, `dotnet format` limpio, 1110 pruebas .NET locales en verde (746 unitarias, 4 arquitectura, 3 E2E, 357 integración excluyendo `NotificationMongoRepositoryTests` por Docker detenido) y cobertura `Sensus.Domain` 91.23 % / `Sensus.Application` 93.94 %. Tabla ya desplegada en prod por CD (`44610076245`); pendiente prueba en vivo de un turno Sensi en producción (`T049-10`). |
| 2026-11-22 | Codex | Parcial (T049-11 verificada) | `DynamoDbLocalBudgetCounterStoreTests` ejecutada contra `amazon/dynamodb-local:2.6.1` validando guardián de cuota y contadores corruptos. `origin/main` registra que CD desplegó la tabla en la corrida `44610076245`. Queda pendiente evidencia del turno y la latencia en AWS. |

## G. Validación en el entorno desplegado

1. Con acceso de lectura al entorno AWS de pruebas, revisar `cdk diff` y verificar el nombre configurado de la tabla de presupuesto para la Lambda.
2. Con una cuenta de prueba bajo cuota y presupuesto, anotar los valores iniciales del contador diario y del gasto mensual. Enviar un turno autenticado a `POST /api/v1/ai/messages` que genere una respuesta del modelo.
3. Verificar en DynamoDB que la cuota aumentó en una unidad y el gasto mensual aumentó por el costo contabilizado. En un turno de razonamiento, verificar también su contador específico.
4. Tomar una muestra de lecturas y escrituras medidas desde la Lambda para calcular el p95 por operación. El objetivo es < 100 ms (NFR-049-04). Guardar región, tamaño de muestra, resultado y referencia del diff. La evidencia omite claves de contador, tokens y contenido del turno.
