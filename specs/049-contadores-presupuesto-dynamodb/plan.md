# Plan 049 — Contadores de presupuesto y cuota de Sensi IA en Amazon DynamoDB

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS CON OBSERVACIONES |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## 1. Resumen técnico

Se introduce `IBudgetCounterStore` y su implementación sobre DynamoDB. `AiBudgetGuard` conserva su
contrato `IAiBudgetGuard` y sus reglas; solo cambia su dependencia. El almacén devuelve `null` ante
cualquier fallo y el guardián lo trata como *fail-closed*.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Sin cambios |
| Application | `IBudgetCounterStore` en `Common/Interfaces` |
| Infrastructure | `DynamoDbBudgetCounterStore`, `AiBudgetStoreSettings`, refactor de `AiBudgetGuard`, registro de `IAmazonDynamoDB` y del almacén, paquete `AWSSDK.DynamoDBv2` |
| Api | Sin cambios de endpoints; `appsettings.json` añade la sección `AiBudgetStore` |
| infra (CDK) | Tabla DynamoDB, permiso IAM mínimo, variable `AiBudgetStore__TableName` |

## 3. Modelo de datos e índices

Tabla `sensus-sensi-budget-{env}`: clave de partición `pk` (S); atributos `amount` (N) y `expiresAt`
(N, segundos Unix, TTL nativo). Claves lógicas: `spend:{yyyyMM}`, `spend:reasoning:{yyyyMM}`,
`daily:{userId}:{yyyyMMdd}`. Todas las consultas son por clave primaria; no hay recorridos de tabla.

## 4. Seguridad

- Rol de Lambda con `dynamodb:GetItem` y `dynamodb:UpdateItem` solo sobre el ARN de la tabla.
- Sin secretos: credenciales del rol de ejecución. TLS por defecto del SDK.
- No se almacena PII: el identificador de persona solo forma parte de la clave del contador diario, con TTL de 2 días.
- *Fail-closed* ante cualquier excepción del SDK.

## 5. Observabilidad y rendimiento

Logs de advertencia sin claves ni valores. Las operaciones propagan `CancellationToken`.

## 6. Costo AWS

DynamoDB `PAY_PER_REQUEST` dentro del Free Tier permanente. Estimado: 0.00 USD/mes. No se crea VPC ni NAT.

## 7. Estrategia de pruebas

- Unitarias de `AiBudgetGuard` con `IBudgetCounterStore` simulado (disponible, fallo, techo, cuota, alerta).
- Pruebas de `DynamoDbBudgetCounterStore` con doble de `IAmazonDynamoDB`: forma de la solicitud, fallo, *timeout*, TTL vencido, sin configuración.
- `T049-09`: ejecutar DynamoDB Local efímero con el Testcontainers existente, en un puerto local asignado dinámicamente. Crear una tabla de prueba con el esquema de producción y comprobar incrementos concurrentes, persistencia del TTL original y lectura a cero después del vencimiento. Sin recursos AWS adicionales ni cambios de permisos.
- `T049-11`: comprobar la decisión, el registro de gasto y la cuota de `AiBudgetGuard` contra DynamoDB Local. Distinguir una clave ausente o vencida de un registro existente con importe o vencimiento inválidos. En el segundo caso, el acceso al modelo se bloquea. El reloj UTC compartido permite verificar los periodos sin depender de la fecha del equipo.
- Verificación de IaC con `cdk synth`/`cdk diff`.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec 049, ADR 0013 y enmienda v2.2.0 |
| II. Clean Architecture | PASS | Contrato en Application, SDK solo en Infrastructure |
| III. CQRS y contratos | PASS | Sin cambios de API |
| IV. Seguridad y privacidad | PASS | IAM mínimo, sin PII ni secretos, *fail-closed* |
| V. Verificación automatizada | PASS local | DynamoDB Local ejecutado con Testcontainers (T049-09) |
| VI. Costo cero e IaC | PASS | Free Tier permanente, CDK, etiquetas |
| VII. Rendimiento y observabilidad | PASS CON OBSERVACIONES | NFR-049-04 pendiente de prueba de carga |

## 9. Riesgos de implementación

Compatibilidad de la versión del SDK con .NET 10; retraso del TTL (mitigado en FR-049-07).

## 10. Orden de implementación

Enmienda y ADR → contrato → almacén → guardián → DI/configuración → CDK → pruebas → verificación.
