# Tareas 003 — Perfil del usuario autenticado

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Remediación pendiente |
| Alineado a | `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T003-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-003-01` | — | Pendiente |
| T003-02 | Cubrir 200 y 401 en pruebas de integración | Api | `FR-003-03` | T003-01 | Pendiente |
| T003-03 | Probar que el DTO no contiene campos sensibles | Application | `FR-003-02` | T003-02 | Pendiente |
| T003-04 | Modelar la inmutabilidad de correo y roles | Domain | `FR-003-05` | T003-03 | Pendiente |
| T003-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-003-01` | T003-04 | Pendiente |
| T003-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-003-01` | T003-05 | Pendiente |
| T003-07 | Registrar evidencias del Constitution Check | Docs | `FR-003-01` | T003-06 | Pendiente |
| T003-08 | Documentar `POST /users/photo-signature` (retro-spec) y su contrato | Docs | `FR-003-07` | — | Hecho |
| T003-09 | Validar `fotoPerfilUrl` contra el host exacto `res.cloudinary.com`, la cuenta propia y la carpeta `sensus/profiles/{userId}`; hoy basta con que el host termine en `cloudinary.com` | Application | `FR-003-06` | T003-08 | Pendiente |
| T003-10 | Moderar la foto de perfil antes de aceptarla: subir por `/media/upload` (moderado) o validar el veredicto antes de guardar la URL | Application | `FR-003-08` | T003-08 | Pendiente |
| T003-11 | Límite de tasa `sensitive-write` en `photo-signature` | Api | `FR-003-09` | T003-08 | Pendiente |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
