# Checklist 051 — Pausa nocturna de la infraestructura

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 051 está aprobada por el responsable (2026-11-19)
- [x] Horario confirmado: 22:30 a 07:00
- [x] Costo aprobado (opción A, excepción EX-003)
- [x] Dependencias resueltas: 015, 045, 048

## B. Específicos de este spec

- [x] Solo se borran recursos con la etiqueta `sensus:night-switch`
- [x] La API recibe tráfico solo cuando hay salida a internet
- [x] Horario y zona horaria en contexto de CDK
- [x] La función es idempotente
- [ ] Verificado en AWS tras el despliegue (T051-08)

## C. Antes de entregar (Definition of Done)

- [x] IaC actualizada y `cdk diff` revisado (sin reemplazos ni borrados de recursos existentes)
- [x] Modelo de amenazas en `plan.md`
- [x] Logs sin secretos ni PII
- [x] Constitution Check en PASS o brecha registrada
- [ ] Prueba automatizada de la función Python en CI (no existe aún; registrada como observación de V y VIII)

## D. Constitution Check

- [x] **I** — PASS
- [x] **II** — PASS
- [x] **III** — PASS
- [x] **IV** — PASS
- [x] **V** — PASS CON OBSERVACIONES
- [x] **VI** — PASS CON OBSERVACIONES (EX-003)
- [x] **VII** — PASS
- [x] **VIII** — PASS CON OBSERVACIONES

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| IaC | `infra/src/Sensus.Infra/SensusInfraStack.CacheAndSchedule.cs`, `infra/assets/night-switch/index.py` | ☑ |
| `cdk diff` | Cuenta 784656681844, us-east-2, 2026-11-19: 37 recursos nuevos; modificados la Lambda, su rol y permiso, y el presupuesto | ☑ |
| Prueba de franja horaria | 7 casos límite en verde | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Implementado, pendiente de despliegue | Se despliega por CI al llegar a `main` |
