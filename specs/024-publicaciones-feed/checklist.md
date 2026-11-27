# Checklist 024 — Publicaciones del feed

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 024 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 017, 022
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado
- [x] Modelo de amenazas (STRIDE) y Principio VIII Fintech incluidos en `plan.md`

## B. Específicos de este spec

- [x] Ninguna publicación se expone sin veredicto (*fail-closed*, `FR-024-02`)
- [x] La moderación de texto e imagen ocurre en la misma solicitud, antes de subir la imagen y persistir el post (`FR-024-02`, `FR-024-04`)
- [x] Solo texto y texto con imagen se moderan con la entrada correspondiente; ambos caminos quedan en cuarentena (`202 Accepted`, `Status=Quarantined`, sin subida de imagen y con auditoría) ante fallos o indisponibilidad del moderador (`CA-024-01`, `CA-024-10`, `FR-024-09`)
- [x] El rechazo explícito por políticas o la falta de almacenamiento/auditoría en publicación aprobada producen 400/503 fail-closed sin publicar ni subir imagen (`CA-024-06`)
- [x] El autor proviene del token JWT (`ICurrentUserService`, `FR-024-03`)
- [x] El borrado solo lo realiza el autor original o un administrador (403 a terceros, `FR-024-05`, `FR-024-06`)
- [x] El borrado excluye inmediatamente el post del feed y de comentarios (`FR-024-07`)
- [x] El borrado purga la imagen con `IMediaStorage.DeleteImageAsync` usando `ImagePublicId`; el fallo se registra sin revertir el borrado (`CA-024-07`)
- [x] `multipart/form-data` acepta `content`/`text` e `image` con límite de 4,5 MB y respuesta 413; solo JPEG/PNG/WebP, otros formatos se rechazan con 400 `FeedPost.UnsupportedImageType` antes de moderar o subir
- [x] La visibilidad usa lista de permitidos: solo `Published` y `!isDeleted` (`FR-024-07`)
- [x] El estado es consultable y los estados del post son consistentes (`PendingModeration`, `Published`, `Quarantined`, `Deleted`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`tests/Sensus.ArchitectureTests` — 4 superadas)
- [x] Pruebas unitarias y de integración añadidas y en verde (`FeedPostsTests.cs` — 12; `FeedPostEndpointsTests.cs` — 10; `MongoFeedPostVisibilityLookupTests.cs` — 7)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado y validado (`specs/024-publicaciones-feed/contracts/openapi.yaml`, `tests/Sensus.IntegrationTests/Contracts/OpenApiContractTests.cs`)
- [x] IaC actualizada y `cdk diff` revisado si aplica (sin cambios de infraestructura requeridos)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ (v2.1.0) |
| Plan | [`plan.md`](plan.md) | ☑ (v2.1.0) |
| Tareas | [`tasks.md`](tasks.md) | ☑ (v2.1.0) |
| Pruebas unitarias | [`tests/Sensus.Application.UnitTests/Feed/FeedPostsTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.Application.UnitTests/Feed/FeedPostsTests.cs) | ☑ (12 superadas) |
| Pruebas de integración | [`tests/Sensus.IntegrationTests/Feed/FeedPostEndpointsTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.IntegrationTests/Feed/FeedPostEndpointsTests.cs) | ☑ (11 superadas) |
| Pruebas de visibilidad | [`tests/Sensus.IntegrationTests/Persistence/MongoFeedPostVisibilityLookupTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.IntegrationTests/Persistence/MongoFeedPostVisibilityLookupTests.cs) | ☑ (7 superadas) |
| Pruebas de arquitectura | [`tests/Sensus.ArchitectureTests/ArchitectureTests.cs`](file:///C:/Users/salin/source/repos/sensus-back/tests/Sensus.ArchitectureTests/ArchitectureTests.cs) | ☑ (4 superadas) |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ (v2.1.0) |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-22 | Antigravity | Aprobado | Implementación completa de Spec 024 con pruebas unitarias, de integración, formato y compilación limpia con `-warnaserror`. |
| 2026-11-23 | Revisión de specs | Aprobado | Ajuste de `FR-024-04` a 201/400 (moderación síncrona), purga de imagen al borrar, visibilidad por lista de permitidos, contrato multipart con lista de permitidos JPEG/PNG/WebP y 413. Build `-warnaserror`, `dotnet format`, 910 unitarias, 4 arquitectura y pruebas de integración afectadas en verde. 9 pruebas Testcontainers no ejecutables: daemon de Docker no disponible en el entorno; el resto de la suite de integración pasa (493/502). |
| 2026-11-26 | Antigravity | T024-10 completada (`FR-024-08`) | JSON ya no acepta `imageUrl` externa arbitraria (400 `FeedPost.ExternalImageUrlNotAllowed`); solo `mediaAssetId` (o alias compatible `mediaId`) verificado en `IMediaAssetRepository` como `Approved` y propiedad del autor (`400 FeedPost.MediaNotApproved`). Móvil `movil_Rodrigo` actualizado (`FeedDtos.kt` + `FeedAvailabilityApiTest.kt`). Pruebas: `FeedPostsTests` **17/17 pasadas**, `FeedPostEndpointsTests` **14/14 pasadas**. |
| 2026-11-26 | Antigravity | T024-09 / T024-11 completadas (`FR-024-02`, `FR-024-09`) | Cuarentena persistida (`FeedPostStatus.Quarantined`, HTTP `202 Accepted`) sin subida a Cloudinary ante indisponibilidad/timeout de moderación de texto o imagen, auditoría `ModerationTargetType.FeedPost` (`RecordAsync` obligatorio antes de publicar y `RecordBestEffortAsync` en cuarentena/rechazo) y purga de cuarentena vencida a los 30 días en `PurgeQuarantinedMediaCommandHandler`. Verificado: `53/53` unitarias (`FeedPostsTests` + `NearbyFeedTests` + `PurgeQuarantinedMediaCommandHandlerTests`) y `25/25` integración (`FeedPostEndpointsTests` + `MongoFeedSocialIntegrationTests`). |
| 2026-11-26 | Ejecutor cierre specs | T024-12 completada (`FR-024-10`) | Retirado alias `mediaId` en `FeedEndpoints.cs`: responde `400 Bad Request` (`Feed.MediaIdRetired`, "Usa mediaAssetId"). Verificado en `FeedPostEndpointsTests.CreatePost_ReturnsBadRequest_WhenRetiredMediaIdAliasSentWithoutMediaAssetId`. |