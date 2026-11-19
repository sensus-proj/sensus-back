# Tareas 051 — Pausa nocturna de la infraestructura

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado (pendiente de despliegue) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T051-01 | VPC de dos zonas sin NAT fijo y endpoint de puerta de enlace de DynamoDB | IaC | `FR-051-03` | — | Hecho |
| T051-02 | Función `night-switch` (`stop`, `start`, `reconcile`), idempotente | IaC | `FR-051-02` | T051-01 | Hecho |
| T051-03 | Rol de mínimo privilegio con condiciones por etiqueta y ARN acotados | IaC | `NFR-051-02` | T051-02 | Hecho |
| T051-04 | Programaciones de EventBridge Scheduler a las 22:30 y 07:00 (`America/Mexico_City`) | IaC | `FR-051-01` | T051-02 | Hecho |
| T051-05 | `Trigger` de reconciliación al desplegar | IaC | `FR-051-06` | T051-02 | Hecho |
| T051-06 | Prueba de la franja horaria, `cdk synth` y `cdk diff` contra la cuenta real | Pruebas | `FR-051-01` | T051-05 | Hecho |
| T051-07 | Spec, ADR 0014, constitución v2.4.0 y registro de requisitos | Docs | `FR-051-07` | T051-06 | Hecho |
| T051-08 | Tras el despliegue: invocar `stop` y `start` a mano y verificar CA-051-01 y CA-051-02 | Pruebas | `FR-051-04` | despliegue | Pendiente |

## Notas de ejecución

- El despliegue lo hace el workflow `deploy-serverless.yml` al llegar a `main`: inyecta los secretos de la
  Lambda. Un `cdk deploy` local los borraría, porque no están en las variables de entorno de la máquina.
- La función se invoca a mano con `aws lambda invoke --function-name sensus-night-switch-prod --payload '{"action":"start"}'`.
