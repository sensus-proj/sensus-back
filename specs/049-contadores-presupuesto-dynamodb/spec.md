# Spec 049 — Contadores de presupuesto y cuota de Sensi IA en Amazon DynamoDB

| Campo | Valor |
|---|---|
| ID | 049 |
| Slug | `contadores-presupuesto-dynamodb` |
| Estado | Parcial |
| Ola | 7 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 045, 046, 048 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

---

## 1. Contexto y problema

`AiBudgetGuard` (Spec 046, `EX-001`) necesita contadores atómicos compartidos entre invocaciones de
Lambda: gasto mensual total, gasto mensual del nivel *reasoning* y mensajes diarios por persona. Hoy los
guarda en Redis, pero el despliegue fija `Redis__Enabled=false` y no hay cadena de conexión. El guardián
opera *fail-closed* y **toda** petición a Sensi IA recibe la respuesta determinista degradada; el
modelo nunca se invoca. ElastiCache exige VPC y NAT Gateway (≈ 40 USD/mes) y un Redis externo viola la
prohibición multi-nube (§2.2). Se adopta Amazon DynamoDB dentro del nivel gratuito permanente
([ADR 0013](../../docs/adr/0013-contadores-presupuesto-dynamodb.md), `constitution.md` v2.2.0).

## 2. Alcance

**Incluye**

- Contrato `IBudgetCounterStore` en `Sensus.Application.Common.Interfaces`.
- Implementación `DynamoDbBudgetCounterStore` en `Sensus.Infrastructure` con incrementos atómicos y TTL nativo.
- Refactor de `AiBudgetGuard` para depender de `IBudgetCounterStore` y no de `ICacheService`.
- Tabla DynamoDB, permiso IAM mínimo y variable de entorno en `infra/` (AWS CDK).
- Configuración `AiBudgetStore` (nombre de tabla y *endpoint* opcional para DynamoDB Local).

**No incluye**

- Cambios en reglas de presupuesto, umbrales o `SensiSettings` (siguen en `appsettings.json`, Spec 046).
- Reemplazo de Redis como caché de lecturas (Spec 048); queda opcional.
- Almacenar datos de negocio, conversaciones o PII en DynamoDB.
- Ejecutar `cdk deploy` (solo con autorización explícita).

## 3. Actores

- Persona autenticada (sus mensajes consumen cuota diaria)
- Sistema (`AiBudgetGuard`, `SendAiMessageCommandHandler`)
- Operador (despliega la infraestructura y vigila el consumo del Free Tier)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-049-01 | Definir `IBudgetCounterStore` con incremento atómico numérico, lectura de contador y estado de disponibilidad | Must |
| FR-049-02 | Implementar `DynamoDbBudgetCounterStore` con `UpdateItem ... ADD` atómico, lectura consistente y expiración por atributo TTL | Must |
| FR-049-03 | `AiBudgetGuard` usa `IBudgetCounterStore` y opera *fail-closed* si el almacén no está disponible, falla o expira | Must |
| FR-049-04 | Definir la tabla en CDK: clave `pk`, `PAY_PER_REQUEST`, TTL en `expiresAt`, sin PITR, etiquetas `project`/`environment`/`owner` | Must |
| FR-049-05 | Conceder a la Lambda solo `dynamodb:GetItem` y `dynamodb:UpdateItem` sobre esa tabla y publicar `AiBudgetStore__TableName` | Must |
| FR-049-06 | Resolver nombre de tabla y *endpoint* opcional desde configuración, sin secretos ni valores fijos en código | Must |
| FR-049-07 | Tratar contadores con `expiresAt` vencido como 0 aunque DynamoDB no los haya purgado todavía | Should |

## 5. Requisitos no funcionales

| ID | Categoría | Métrica | Estado |
|---|---|---|---|
| NFR-049-01 | Costo mensual | 0.00 USD; consumo dentro del Free Tier permanente de DynamoDB | Pendiente |
| NFR-049-02 | Seguridad de fallo | 100 % de errores del almacén producen degradación determinista, nunca consumo sin techo | Pendiente |
| NFR-049-03 | Privacidad en logs | Los logs del almacén no incluyen claves, identificadores de persona ni valores | Pendiente |
| NFR-049-04 | Latencia | p95 < 100 ms por operación de contador desde Lambda en la región del despliegue | Pendiente |

## 6. Criterios de aceptación

1. Con la tabla creada y la variable `AiBudgetStore__TableName` presente, `CanExecuteTurnAsync` devuelve `true` bajo presupuesto y cuota.
2. Si el almacén devuelve `null` (sin configuración, error o *timeout*), `CanExecuteTurnAsync` devuelve `false` y el handler degrada.
3. Alcanzado el techo mensual o la cuota diaria, `CanExecuteTurnAsync` devuelve `false`.
4. `RecordSpendAsync` incrementa gasto total, gasto *reasoning* (si aplica) y contador diario con la expiración configurada.
5. `cdk synth` genera la tabla con TTL y el rol sin permisos adicionales sobre otros recursos.

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| DynamoDB purga el TTL con retraso de hasta 48 h | FR-049-07: se compara `expiresAt` en la lectura; las claves incluyen el periodo (`yyyyMM`, `yyyyMMdd`) |
| Latencia añadida por llamadas regionales | Pocas operaciones por mensaje; NFR-049-04 se mide con prueba de carga |
| Pruebas contra DynamoDB real no disponibles localmente | Doble de `IAmazonDynamoDB` ahora; DynamoDB Local con Testcontainers pendiente (T049-09) |
