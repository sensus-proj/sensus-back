# Tareas 022 — Carga de multimedia

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T022-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-022-01` | — | Implementado |
| T022-02 | Implementar: UploadMediaCommand y validador de tipo y tamaño | Application | `FR-022-01` | T022-01 | Implementado |
| T022-03 | Implementar: Adaptador Cloudinary y verificación de firma de bytes | Infrastructure | `FR-022-01` | T022-02 | Implementado |
| T022-04 | Implementar: Endpoint multipart POST /api/v1/media/upload | Api | `FR-022-01` | T022-03 | Implementado |
| T022-05 | Validar MIME real por firma de bytes | Application | `FR-022-02` | T022-04 | Implementado |
| T022-06 | Implementar el almacenamiento seguro en Cloudinary | Infrastructure | `FR-022-04` | T022-05 | Implementado |
| T022-07 | Probar el límite de 4,5 MB y el 413 | Api | `FR-022-03` | T022-06 | Implementado |
| T022-08 | Añadir la limpieza de archivos huérfanos | Infrastructure | `FR-022-05` | T022-07 | Implementado |
| T022-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-022-01` | T022-08 | Implementado |
| T022-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-022-01` | T022-09 | Implementado |
| T022-11 | Registrar evidencias del Constitution Check | Docs | `FR-022-01` | T022-10 | Implementado |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
