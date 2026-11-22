# Plan 051 — Pausa nocturna de la infraestructura

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS CON OBSERVACIONES |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

La VPC se define sin NAT fijo. Una función Python (`sensus-night-switch-<entorno>`) crea y destruye el NAT, su IP
elástica y la ruta `0.0.0.0/0` de las subredes privadas, y conmuta la concurrencia de la API y las acciones de las
alarmas. Como CloudFormation no define esos recursos, no hay deriva del stack. Dos programaciones de EventBridge
Scheduler la invocan con `stop` y `start`, y un `Trigger` de CDK la invoca con `reconcile` al crearse.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| IaC (`infra/`) | `SensusInfraStack.CacheAndSchedule.cs`: VPC, endpoint de DynamoDB, grupos de seguridad, Valkey Serverless, función de pausa, rol de mínimo privilegio, dos `AWS::Scheduler::Schedule` y `Trigger` |
| IaC (`infra/assets/night-switch`) | `index.py` con `stop`, `start` y `reconcile` |
| Aplicación | Sin cambios |

## 3. Modelo de datos e índices

No aplica.

## 4. Seguridad

- Rol propio con mínimo privilegio. Crea NAT e IP solo con la etiqueta `sensus:night-switch` en la solicitud;
  borra solo recursos con esa etiqueta; las rutas, la concurrencia y las alarmas se limitan a sus ARN.
- El rol del programador solo puede invocar esta función y exige `aws:SourceAccount` (confused deputy).
- Concurrencia reservada de 1 en la función para impedir ejecuciones simultáneas.
- Logs sin secretos ni datos personales.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| La función se usa para borrar recursos ajenos | Elevación de privilegios | Borrado condicionado a la etiqueta propia; ARN acotados | Bajo |
| Un tercero invoca la programación desde otra cuenta | Suplantación | `aws:SourceAccount` en la confianza del rol del programador | Bajo |
| La reanudación falla y deja la API caída | Denegación de servicio | Reintentos, idempotencia, invocación manual documentada y alarma de errores de la función que nunca se silencia (FR-051-08) | Bajo |
| Silenciar alarmas oculta un incidente real de noche | Repudio | Solo de 22:30 a 07:00, con la API sin tráfico; se reactivan siempre al reanudar | Medio |
| La IP elástica nueva cada día rompe la lista de acceso de MongoDB Atlas | Denegación de servicio | Supuesto documentado en la spec | Medio |

## 5. Observabilidad y rendimiento

- Log JSON por ejecución con acción y entorno, retención de 7 días.
- Las alarmas de la Spec 015 se silencian solo durante la pausa.
- Alarma `sensus-night-switch-errors-<entorno>` (FR-051-08, T051-10): métrica estándar `Errors` de la función, suma en 5 min
  ≥ 1, datos faltantes como no incumplidos, acción al tema SNS de alertas. **No** forma parte de la lista que la función
  silencia (`ALARM_NAMES`) y el rol de la función no tiene permiso sobre ella. Motivo: el 2026-11-19 el arranque de las
  07:00 falló y, al quedar las alarmas silenciadas, la caída de 5.5 h solo se detectó por el reporte de un usuario.
  Costo: séptima alarma de 10 gratuitas; la métrica `Errors` de Lambda no tiene costo.

## 6. Costo AWS

| Recurso | Sin pausa | Con pausa (15.5 h/día) |
|---|---|---|
| NAT Gateway (0.045 USD/h + datos) | ≈ 33 USD | ≈ 21 USD |
| IP elástica pública (0.005 USD/h) | ≈ 3.6 USD | ≈ 2.3 USD |
| Valkey Serverless (mínimo 100 MB) | ≈ 6 USD | ≈ 6 USD |
| Función de pausa y EventBridge Scheduler | ≈ 0 USD (nivel gratuito) | ≈ 0 USD |
| **Total aproximado** | **≈ 43 USD/mes** | **≈ 29–32 USD/mes** |

Costo aprobado por el responsable el 2026-11-19 (opción A), cubierto por créditos de AWS. Excepción EX-003 en el
Apéndice E de la constitución. Presupuesto de AWS Budgets elevado a 40 USD/mes con alerta al 80 %.

## 7. Estrategia de pruebas

- Prueba de la lógica de franja horaria (`is_night`) en los límites 22:29, 22:30, 00:00, 06:59 y 07:00.
- `cdk synth` y `cdk diff` contra la cuenta real, sin reemplazos ni borrados de recursos existentes.
- Verificación posterior al despliegue: invocar `stop` y `start` a mano y comprobar CA-051-01 y CA-051-02.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-051-01` a `FR-051-07` y 4 NFR con criterios medibles |
| II | PASS | Solo IaC; sin cambios en capas de la aplicación |
| III | PASS | No expone contratos |
| IV | PASS | Mínimo privilegio, sin secretos en logs |
| V | PASS CON OBSERVACIONES | La lógica horaria se probó; la función completa solo se puede probar contra AWS tras el despliegue |
| VI | PASS CON OBSERVACIONES | Recursos con costo (NAT, IP, Valkey) bajo la excepción EX-003, todo definido en `infra/` |
| VII | PASS | Logs y alarmas; reanudación ≤ 5 min |
| VIII | PASS CON OBSERVACIONES | Modelo de amenazas en §4; sin prueba automatizada en CI de la función Python |

## 9. Riesgos de implementación

- El NAT tarda entre 1 y 3 minutos en estar disponible; la API vuelve a recibir tráfico al terminar.
- Si un despliegue reemplaza las tablas de rutas privadas, se pierde la ruta hasta la siguiente reanudación o
  una invocación manual con `start`.

## 10. Orden de implementación

1. `T051-01` VPC sin NAT fijo y endpoint de DynamoDB
2. `T051-02` Función `night-switch` con `stop`, `start` y `reconcile`
3. `T051-03` Rol de mínimo privilegio
4. `T051-04` Programaciones de EventBridge Scheduler
5. `T051-05` `Trigger` de reconciliación al desplegar
6. `T051-06` Pruebas de franja horaria, `cdk synth` y `cdk diff`
7. `T051-07` Documentación, ADR y Constitution Check
8. `T051-08` Verificación tras el despliegue
