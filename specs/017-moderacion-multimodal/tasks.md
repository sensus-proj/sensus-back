# Tareas 017 — Moderación multimodal de contenido

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T017-01 | Implementar: Contrato IModerationService y modelo de veredicto | Domain | `FR-017-01` | — | Completada |
| T017-02 | Implementar: Caso de uso de moderación previa a la publicación | Application | `FR-017-01` | T017-01 | Completada |
| T017-03 | Implementar: Cliente de OpenAI con timeout, reintentos y mapeo de respuesta | Infrastructure | `FR-017-01`, `FR-017-10`, `NFR-017-03` | T017-02 | Completada (`ModerationHttpRetryHelper`, `OpenAiSettingsValidator`, `OpenAiTextModerationService`, `OpenAiImageModerationService`; 61/61 integración) |
| T017-04 | Implementar: Integración en los flujos de oferta, publicación y comentario | Api / Application | `FR-017-01`, `FR-017-08` | T017-03 | Completada (`CreateOfferCommandHandler`, `UpdateOfferCommandHandler`, `RepublishOfferCommandHandler`, `CreateFeedPostCommandHandler`, `CreateFeedCommentCommandHandler`, `CreateStoreReviewCommandHandler`, `ReplyStoreReviewCommandHandler`) |
| T017-05 | Definir IModerationService y el modelo de veredicto | Domain | `FR-017-01` | T017-04 | Completada |
| T017-06 | Implementar el cliente con timeout y fail-closed | Infrastructure | `FR-017-04` | T017-05 | Completada |
| T017-07 | Registrar la decisión completa para auditoría | Application | `FR-017-05` | T017-06 | Completada (`MediaAsset`, `Offer`, `StoreReview`, `FeedComment` y `FeedPost` con `IModerationAuditRecorder`). |
| T017-08 | Encadenar la moderación a la creación de ofertas y carga previa a Cloudinary | Application | `FR-017-06` | T017-07 | Completada (`UploadMediaCommandHandler` modera antes de Cloudinary y persiste cuarentena 202; `CreateOfferCommandHandler` y `UpdateProfileCommandHandler` exigen `MediaAsset` aprobado del propietario) |
| T017-09 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-017-01` | T017-08 | Completada |
| T017-10 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-017-01` | T017-09 | Completada |
| T017-11 | Registrar evidencias del Constitution Check | Docs | `FR-017-01` | T017-10 | Completada |
| T017-12 | Añadir y probar la moderación de publicaciones de solo texto con la política de Sensus | Infrastructure | `FR-017-08` | T017-06 | Completada (`OpenAiImageModerationService` + `CreateFeedPostCommandHandler` 202 `Quarantined`). |
| T017-13 | Retirar el endpoint independiente y comprobar que no es accesible | Api / Pruebas | `FR-017-07` | T017-06 | Completada |
| T017-14 | Enviar a cuarentena respuestas incompletas del adaptador de compatibilidad | Infrastructure / Pruebas | `FR-017-04` | T017-06 | Completada |
| T017-15 | Hacer fail-closed `ContentModerationHelper` ante servicio nulo, excepción o retorno nulo | Application / Pruebas | `FR-017-09` | T017-14 | Completada |
| T017-16 | Validar presupuesto temporal (`≤ 20 s`) y reintento único acotado ante 429/5xx en moderación OpenAI | Infrastructure / Pruebas | `FR-017-10`, `NFR-017-03` | T017-03 | Completada |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
