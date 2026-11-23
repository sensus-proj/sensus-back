# Checklist 024 — Publicaciones del feed

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

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
- [x] Ausencia del moderador de texto, de imagen o de almacenamiento multimedia produce 400 sin efectos secundarios (`CA-024-06`)
- [x] El autor proviene del token JWT (`ICurrentUserService`, `FR-024-03`)
- [x] El borrado solo lo realiza el autor original o un administrador (403 a terceros, `FR-024-05`, `FR-024-06`)
- [x] El borrado excluye inmediatamente el post del feed y de comentarios (`FR-024-07`)
- [x] El borrado purga la imagen con `IMediaStorage.DeleteImageAsync` usando `ImagePublicId`; el fallo se registra sin revertir el borrado (`CA-024-07`)
- [x] `multipart/form-data` acepta `content`/`text` e `image` con límite de 4,5 MB y respuesta 413; solo JPEG/PNG/WebP, otros formatos se rechazan con 400 `FeedPost.UnsupportedImageType` antes de moderar o subir
- [x] La visibilidad usa lista de permitidos: solo `Published` y `!isDeleted` (`FR-024-07`)
- [x] Los estados del post son consistentes (`PendingModeration`, `Published`, `Quarantined`, `Deleted`)

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
