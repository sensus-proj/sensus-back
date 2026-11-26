# Checklist 017 — Moderación multimodal de contenido

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 017 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 009, 022
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El flujo de publicación de la 024 persiste la cuarentena ante fallos (`202 Accepted`, `Quarantined` sin subida a Cloudinary y purga a 30 días). El adaptador ya valida el veredicto y devuelve cuarentena con respuestas malformadas o timeout (`CA-017-06`).

- [x] Imagen y texto viajan en la misma solicitud
- [x] Ningún camino permite publicar sin veredicto (moderación y auditoría *fail-closed* antes de publicar)
- [x] El timeout, reintento único acotado (`FR-017-10`) y presupuesto ≤ 20 s (`NFR-017-03`) están probados
- [x] La clave no está en el repositorio
- [x] No existe endpoint público de moderación

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application (validada en suite de cierre §6)
- [x] Contrato OpenAPI actualizado (no aplica: `FR-017-07` sin endpoint público)
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica en Spec 017)
- [x] Logs con correlation-id y sin PII ni contenido moderado
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — La ruta independiente responde 404 (FR-017-07)
- [x] **IV. Seguridad y privacidad** — PASS (`UploadMediaCommandHandler` modera antes de Cloudinary, `CreateOfferCommandHandler`/`UpdateProfileCommandHandler` exigen `MediaAsset` aprobado del propietario, `CreateFeedPostCommandHandler` conecta auditoría y cuarentena sin subida)
- [x] **V. Verificación automatizada** — PASS (`OpenAiTextModerationServiceTests`, `OpenAiImageModerationServiceTests` y `OpenAiSettingsValidatorTests`: 61/61 en verde)
- [x] **VI. Costo cero e IaC** — PASS (`EX-002`)
- [x] **VII. Rendimiento y observabilidad** — PASS (`OpenAiSettingsValidator`, presupuesto ≤ 20 s validado al arrancar)
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (matriz STRIDE en `plan.md` §4, validación estricta y CI verde `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` (`ContentModerationHelperTests`, `CreateOfferCommandHandlerTests`, `UploadMediaCommandHandlerTests`) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Services/` (`OpenAiTextModerationServiceTests`, `OpenAiImageModerationServiceTests`, `OpenAiSettingsValidatorTests`: 61/61) | ☑ |
| Contrato OpenAPI | No aplica (`FR-017-07`, sin endpoint público) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `OpenAiImageModerationService` con omni-moderation y modo visión, timeout configurable y fail-closed. La moderación ocurre en la carga (`UploadMediaCommandHandler`). Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-15 | Equipo Sensus | Spec v1.2.0 | FR-017-02 y FR-017-03 alineados al modelo real (`gpt-4o-mini` en visión con política de Sensus, decisión D28); costo registrado en `EX-002` (`constitution.md` v2.1.0). Corregido el *fail-open* sin clave de `OpenAiImageModerationService.cs`: sin clave, errores y timeouts envían a cuarentena (fail-closed, `constitution.md` §2.4) con pruebas de regresión verificadas. |
| 2026-11-22 | Codex | Parcial | `OpenAiImageModerationServiceTests` verifica la solicitud de solo texto a `gpt-4o-mini`, el resultado estructurado y el comportamiento fail-closed. `MediaEndpointsTests` verifica que la ruta independiente no existe. Pendientes la integración de publicación, la cuarentena de imágenes de ofertas y la auditoría completa. |
| 2026-11-22 | Codex | T017-14 verificada | El adaptador de compatibilidad envía a cuarentena veredictos sin campos requeridos, puntajes fuera de rango y resultados contradictorios. Cinco casos de regresión. |
| 2026-11-22 | Codex | Parcial | El registro append-only de multimedia y reseñas conserva veredicto, categorías, puntajes, política o umbral, acción y correlación. La consulta de administración queda en 021. Falta conectar la publicación de la Spec 024. |
| 2026-11-26 | Antigravity | T017-15 completada (`FR-017-09`) | `ContentModerationHelper.EvaluateAsync` y `EnsureApprovedAsync` convertidos a *fail-closed* (`Quarantine`, `Error.Failure("<errorCode>.ModerationUnavailable")`) ante `ITextModerationService` nulo, excepción o retorno nulo. `ContentModerationHelperTests` (5 casos nuevos en rojo → verde) + ajuste de 8 archivos de pruebas unitarias para inyectar doble aprobador explícito; `dotnet test tests/Sensus.Application.UnitTests` → **1,180/1,180 pasadas (0 fallos)**. |
| 2026-11-26 | Antigravity | Integración Spec 024 completada (`FR-017-08`) | `CreateFeedPostCommandHandler` conectado con `IModerationAuditRecorder` (`ModerationTargetType.FeedPost`) y cuarentena persistida sin subida a Cloudinary (`202 Accepted`) para texto solo y texto con imagen; purga automática a 30 días en `PurgeQuarantinedMediaCommandHandler`. |
| 2026-11-26 | Antigravity (Bloque A4) | Completado (`T017-03`, `T017-04`, `T017-08`, `T017-16`) | Reintento acotado ante fallos transitorios (`ModerationHttpRetryHelper`) y validador al arranque (`OpenAiSettingsValidator` con techo 20 s y `appsettings.json` `TimeoutSeconds=8`). Rojo→verde confirmado (5 fallos→61/61 en verde en `OpenAiTextModerationServiceTests` + `OpenAiImageModerationServiceTests` + `OpenAiSettingsValidatorTests`). Verificado por grep el encadenamiento en ofertas, feed, reseñas y carga previa a Cloudinary (`UploadMediaCommandHandler`). |
