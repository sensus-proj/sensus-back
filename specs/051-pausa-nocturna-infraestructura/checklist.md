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
- [ ] Verificado en AWS tras el despliegue (T051-08) (acción del usuario: invocar `stop`/`start` y comprobar alarma tras despliegue; ver plan cierre §5.Z-7)
- [x] Alarma de errores de la función que la pausa nunca silencia (T051-10; `cdk synth` y `cdk diff` revisados: solo se agrega la alarma)

## C. Antes de entregar (Definition of Done)

- [x] IaC actualizada y `cdk diff` revisado (sin reemplazos ni borrados de recursos existentes)
- [x] Modelo de amenazas en `plan.md`
- [x] Logs sin secretos ni PII
- [x] Constitution Check en PASS o brecha registrada
- [x] Prueba automatizada de la función Python en CI (`infra/assets/night-switch/test_index.py`: 6/6 en verde + paso integrado en `.github/workflows/ci.yml` job `iac`)

## D. Constitution Check

- [x] **I** — PASS
- [x] **II** — PASS
- [x] **III** — PASS
- [x] **IV** — PASS
- [x] **V** — PASS (`test_index.py` 6/6 + integrado en `ci.yml`)
- [x] **VI** — PASS CON OBSERVACIONES (EX-003)
- [x] **VII** — PASS
- [x] **VIII** — PASS (`test_index.py` + `ci.yml` `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| IaC | `infra/src/Sensus.Infra/SensusInfraStack.CacheAndSchedule.cs`, `infra/assets/night-switch/index.py` | ☑ |
| `cdk diff` | Cuenta 784656681844, us-east-2, 2026-11-19: 37 recursos nuevos; modificados la Lambda, su rol y permiso, y el presupuesto | ☑ |
| Prueba de franja horaria | 7 casos límite en verde | ☑ |
| Pruebas Python `night-switch` | `infra/assets/night-switch/test_index.py` (6/6 en verde con `unittest` + CI `.github/workflows/ci.yml`) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Implementado, pendiente de despliegue | Se despliega por CI al llegar a `main` |
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Defecto en producción | El arranque de las 07:00 falló con `UnauthorizedOperation` en `CreateNatGateway` sobre la IP elástica: la condición `aws:RequestTag` no aplica a recursos existentes. Cada reintento dejó una IP suelta (3). Corregido en T051-09 y verificado con `iam simulate-custom-policy` |
| 2026-11-20 | Rodrigo Sánchez (con asistente) | T051-10 implementada | Alarma `sensus-night-switch-errors-<entorno>` sobre la métrica `Errors` de la función (≥ 1 en 5 min) con acción al tema SNS de alertas; fuera de `ALARM_NAMES` y sin permiso del rol de la función, verificado en la plantilla sintetizada. `cdk diff` contra producción: solo `[+]` de la alarma, sin reemplazos. Séptima alarma de 10 gratuitas. Pendiente CA-051-05 tras el despliegue. |
| 2026-11-26 | Antigravity (Bloque A6) | Pruebas Python + CI completadas (`T051-11`) | Creado `infra/assets/night-switch/test_index.py` (6 pruebas: franja 22:29/22:30/06:59/07:00, `reconcile`, acción desconocida `ValueError`, `stop` con filtro por etiqueta `sensus:night-switch`, orden de restauración en `start` y logs sin IDs/secretos) e integrado en `.github/workflows/ci.yml` (`iac` job con `actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065`). |
