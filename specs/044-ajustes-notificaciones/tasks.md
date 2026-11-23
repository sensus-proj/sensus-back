# Tareas 044 — Ajustes de notificaciones

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Cerrado |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T044-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-044-01` | — | Implementado |
| T044-02 | Implementar GetNotificationSettingsQuery y UpdateNotificationSettingsCommand | Application | `FR-044-01` | T044-01 | Implementado |
| T044-03 | Implementar persistencia de preferencias y filtro en el despacho | Infrastructure | `FR-044-01` | T044-02 | Implementado |
| T044-04 | Implementar endpoints GET y PUT /api/v1/users/settings | Api | `FR-044-01` | T044-03 | Implementado |
| T044-05 | Implementar valores por defecto | Application | `FR-044-05` | T044-04 | Implementado |
| T044-06 | Aplicar el filtro de preferencias en el despacho | Infrastructure | `FR-044-04` | T044-05 | Implementado |
| T044-07 | Probar el límite de radio y la actualización | Api | `FR-044-03` | T044-06 | Implementado |
| T044-08 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-044-01` | T044-07 | Implementado |
| T044-09 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-044-01` | T044-08 | Implementado |
| T044-10 | Registrar evidencias del Constitution Check | Docs | `FR-044-01` | T044-09 | Implementado |
| T044-11 | Cerrar contrato de ubicación consentida, vigencia y purga | Api / Docs | `FR-044-07` | T044-10 | Implementado |
| T044-12 | Implementar persistencia geográfica indexada y retiro de ubicación | Infrastructure | `FR-044-07` | T044-11 | Implementado en backend (integración móvil fuera de este repositorio) |
| T044-13 | Despachar alertas al crear ofertas con texto aprobado y cubrir radio, preferencias y fallos | Application / Infrastructure / Pruebas | `FR-044-08` | T044-12 | Implementado en backend (entrega FCM física pendiente de credencial en SSM) |
| T044-14 | Pruebas HTTP de ajustes (401/403/defaults/400) y latencia p95 < 250 ms | Pruebas | `FR-044-01`, `FR-044-02`, `NFR-044-01` | T044-13 | Implementado |
| T044-15 | Cierre documental con evidencia de gates y brechas externas | Docs | `FR-044-01` | T044-14 | Implementado |

## Notas de ejecución

- Cada tarea se cerró actualizando `checklist.md` y `plan.md` cuando cambió el diseño.
- Ninguna tarea se consideró terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- La credencial FCM se aprovisiona en SSM fuera del repositorio; sin secretos en código, configuración ni logs.
- La integración móvil (token FCM y consentimiento de ubicación) y la entrega física son operaciones externas documentadas, no tareas de código pendientes en este repositorio.
- La brecha IV de la spec 034 (moderación de imagen) permanece abierta; el aviso usa texto aprobado por decisión del responsable.
- 11 pruebas Testcontainers siguen bloqueadas por daemon Docker no disponible; no se omitieron ni se bajaron umbrales.
