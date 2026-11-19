# Tareas 027 — Registro de dispositivo para push

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Parcial |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T027-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-027-01` | — | Implementado |
| T027-02 | Implementar: Entidad Device e IPushDispatcher | Domain | `FR-027-01` | T027-01 | Implementado |
| T027-03 | Implementar: RegisterDeviceCommand y DispatchNotificationCommand | Application | `FR-027-01` | T027-02 | Implementado |
| T027-04 | Implementar: Repositorio de dispositivos y adaptador simulado existente | Infrastructure | `FR-027-01` | T027-03 | Implementado |
| T027-05 | Implementar: Endpoint PUT /api/v1/users/device-token | Api | `FR-027-01` | T027-04 | Implementado |
| T027-06 | Definir IPushDispatcher y la entidad Device | Domain | `FR-027-03` | T027-05 | Implementado |
| T027-07 | Implementar el adaptador FCM HTTP v1 con credenciales gestionadas en AWS | Infrastructure | `FR-027-04` | T027-06 | Código verificado con dobles HTTP. Credencial de pruebas en SSM pendiente |
| T027-08 | Resolver la reasignación de token entre usuarios | Application / Infrastructure | `FR-027-06` | T027-07 | Implementado. Pendiente ejecutar la regresión en MongoDB real |
| T027-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-027-01` | T027-08 | Implementado |
| T027-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-027-01` | T027-09 | Implementado |
| T027-11 | Registrar evidencias del Constitution Check | Docs | `FR-027-01` | T027-10 | Implementado |
| T027-12 | Registrar `sensus.com` en Firebase de pruebas, integrar la configuración en Kotlin y verificar permiso y renovación del token | Android | `FR-027-04` | — | Parcial |
| T027-13 | Verificar en un Android físico un aviso enviado por el backend y la apertura de la pantalla del evento | Pruebas | `FR-027-03` | T027-12, T027-15, T027-16 | Pendiente |
| T027-14 | Distinguir rechazo definitivo de fallo operativo en el puerto y gestionar la desactivación en el registro y el despacho | Application | `FR-027-05` | T027-06 | Verificado en unitarias y MongoDB real, incluida la conservación de un token rotado |
| T027-15 | Componer el adaptador y mapear indisponibilidad de registro a ProblemDetails 503 | Api | `FR-027-04` | T027-07, T027-14 | Implementado |
| T027-16 | Probar FCM con respuestas HTTP controladas y la ruta de registro con integración | Pruebas | `FR-027-03` | T027-15 | Implementado |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
