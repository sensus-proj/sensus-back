# Tareas 034 — Creación de oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Parcial |
| Alineado a | `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T034-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-034-01` | — | Contrato actual documentado. Respuestas 202 y 409 pendientes de implementación. |
| T034-02 | Implementar: Entidad Offer con invariantes de precio, vigencia y stock | Domain | `FR-034-01` | T034-01 | Completada |
| T034-03 | Implementar: CreateOfferCommand, validador y orquestación de moderación | Application | `FR-034-01` | T034-02 | Parcial |
| T034-04 | Implementar: Persistencia, referencias a media y encolado de moderación | Infrastructure | `FR-034-01` | T034-03 | Parcial |
| T034-05 | Implementar: Endpoint POST /api/v1/offers | Api | `FR-034-01` | T034-04 | Completada |
| T034-06 | Modelar Offer y sus invariantes | Domain | `FR-034-02` | T034-05 | Completada |
| T034-07 | Encadenar moderación y publicación | Application | `FR-034-03` | T034-06 | Bloqueada por diseño de cuarentena compatible con la constitución vigente |
| T034-08 | Implementar el límite de ofertas activas | Infrastructure | `FR-034-05` | T034-07 | Pendiente |
| T034-09 | Probar el 202 de moderación pendiente | Api | `FR-034-07` | T034-08 | Pendiente |
| T034-10 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-034-01` | T034-09 | Parcial. Pruebas del contrato actual incorporadas. |
| T034-11 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-034-01` | T034-10 | Completada |
| T034-12 | Registrar evidencias del Constitution Check | Docs | `FR-034-01` | T034-11 | Completada |
| T034-13 | Guardar un borrador privado, proteger sus transiciones y verificar el contrato HTTP | Domain / Application / Pruebas | `FR-034-04` | T034-02 | Verificada localmente |
| T034-14 | Verificar que la imagen de una oferta pertenece al comercio y fue moderada con el mismo título y descripción | Application / Infrastructure / Pruebas | `FR-034-03` | T034-05 | Verificada localmente |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
