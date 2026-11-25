# Plan 024 — Publicaciones del feed

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 2.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Permitir crear y eliminar publicaciones en el feed social con texto (1 a 500 caracteres) e imagen opcional, sujetas a moderación preventiva obligatoria (*fail-closed*) **síncrona** y autorización estricta en borrado (autor original o rol de administrador), con purga de la imagen almacenada en Cloudinary.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `FeedPost` con estados (`PendingModeration`, `Published`, `Quarantined`, `Deleted`), `ImagePublicId`, invariantes de texto (1..500) y regla de autoría/borrado |
| Application | `CreateFeedPostCommand` (modera texto e imagen en la misma solicitud, exige `ITextModerationService`/`IImageModerationService`/`IMediaStorage`, sube la imagen aprobada) y `DeleteFeedPostCommand` (purga con `IMediaStorage.DeleteImageAsync`), DTOs (`FeedPostDto`), validadores FluentValidation y puerto `IFeedPostRepository` |
| Infrastructure | `FeedPostDocument` (persiste `imagePublicId`), `MongoFeedPostRepository`, `MongoFeedPostVisibilityLookup` con lista de permitidos, índices en `MongoDbContext` (`authorUserId` + `createdAtUtc`, `status` + `createdAtUtc`) y registro en inyección de dependencias |
| Api | Endpoints `POST /api/v1/feed/posts` (JSON o `multipart/form-data`, lista de permitidos JPEG/PNG/WebP → 400, límite 4,5 MB → 413) y `DELETE /api/v1/feed/posts/{id:guid}` en `FeedEndpoints.cs` con `SensitiveWritePolicy` y `RegisteredUserPolicy` |

## 3. Modelo de datos e índices

- Colección: `feed_posts` en MongoDB Atlas.
- Índices declarados y obligatorios:
  - `ix_feed_posts_author_created_at`: `{ authorUserId: 1, createdAtUtc: -1 }` (consulta de publicaciones por autor)
  - `ix_feed_posts_status_created_at`: `{ status: 1, isDeleted: 1, createdAtUtc: -1 }` (consulta del feed público y lookup de visibilidad)

## 4. Modelo de amenazas (STRIDE) y Seguridad

| Amenaza (STRIDE) | Mitigación implementada |
|---|---|
| **S**poofing | El autor de la publicación se extrae exclusivamente del JWT (`sub`/`UserId` verificado en servidor), nunca del cuerpo del payload. |
| **T**ampering | Moderación previa *fail-closed* y síncrona con `gpt-4o-mini` y la política de Sensus antes de persistir o subir a Cloudinary: enviar solo texto si no hay imagen y ambas modalidades juntas en la misma solicitud cuando haya imagen. Ante rechazo, fallo, timeout, cuota agotada o respuesta inválida, queda en cuarentena / se rechaza sin subir la imagen ni publicar el post. La visibilidad posterior usa lista de permitidos (`Published` y `!isDeleted`). |
| **R**epudiation | Auditoría inmutable de autor, fecha de creación (`createdAtUtc`) y estado de moderación. |
| **I**nformation Disclosure | ProblemDetails estándar RFC 7807/7231 sin filtrar nombres de colecciones ni trazas internas. Los posts eliminados o en cuarentena devuelven 404 o no aparecen en consultas. |
| **D**enial of Service | Rate limiting aplicado en `POST` (`SensitiveWritePolicy`), tamaño máximo de texto fijado en 500 caracteres y límite de 4,5 MB para la imagen (`413`). |
| **E**levation of Privilege | Verificación estricta en servidor de que quien invoca `DELETE /api/v1/feed/posts/{id}` sea el autor de la publicación o un usuario con rol `Administrador`. |

## 5. Observabilidad y rendimiento

- Paginación estándar (20 por defecto, 100 máximo) para consultas derivadas (Spec 026/042).
- Logs estructurados con `correlation-id` y sin PII ni contenido no moderado.
- El fallo de purga de imagen se registra como advertencia sin PII; no revierte el borrado lógico.
- Métricas operativas sin dimensiones de riesgo.

## 6. Costo AWS

Lambda .NET 10 y API Gateway HTTP API v2 dentro del Free Tier; almacenamiento multimedia en Cloudinary Free Tier. Las llamadas de moderación consumen el techo agregado de 1 USD/mes de `EX-002`; se medirá el costo unitario de solo texto antes de habilitar el flujo.

## 7. Estrategia de pruebas

- **Unitarias (`Sensus.Application.UnitTests`)**:
  - Invariantes de la entidad `FeedPost` (longitud de texto 1..500, `ImagePublicId`, transiciones de estado, autorización de borrado).
  - Handlers `CreateFeedPostCommandHandler` y `DeleteFeedPostCommandHandler` con dobles de prueba: moderación de texto no registrada (*fail-closed*), moderación e upload de imagen en la misma solicitud, purga de imagen al borrar, 403 usuario no autor, 404 no encontrado (`FeedPostsTests.cs`, 12 pruebas).
- **Integración (`Sensus.IntegrationTests`)**:
  - Endpoint `POST /api/v1/feed/posts`: 201 en JSON, 201 en `multipart/form-data` con moderación y subida, 400 por texto inválido, moderación rechazada o formato de imagen no permitido, 401 sin auth, 403 invitado, 413 por imagen > 4,5 MB (`FeedPostEndpointsTests.cs`, 11 pruebas).
  - Endpoint `DELETE /api/v1/feed/posts/{id}`: 204 por autor (con purga), 204 por admin, 403 por usuario ajeno, 404 inexistente.
  - Visibilidad: `MongoFeedPostVisibilityLookup.IsVisible` solo permite `Published` y `!isDeleted` (`MongoFeedPostVisibilityLookupTests.cs`, 7 pruebas).
  - Contrato OpenAPI validado en `OpenApiContractTests.cs`.

## 8. Constitution Check

Evaluación alineada a `constitution.md` v2.4.0.

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | `FR-024-01` a `FR-024-07` cubiertos con criterios de aceptación medibles y tareas atómicas. |
| II. Clean Architecture | PASS | Domain (entidad e invariantes) sin dependencias externas; Application orquesta casos de uso y puertos; Infrastructure implementa persistencia y Api expone endpoints. |
| III. CQRS y contratos | PASS | Comandos separados (`CreateFeedPostCommand`, `DeleteFeedPostCommand`), DTOs explícitos y contrato OpenAPI 3.1. |
| IV. Seguridad y privacidad | PASS | Moderación *fail-closed* multimodal síncrona previa a publicación o subida; autor obtenido de JWT; borrado autorizado solo por autor o admin; purga de imagen; sin PII en logs. |
| V. Verificación automatizada | PASS | Pruebas unitarias y de integración que cubren flujos de éxito, rechazo de moderación, subida/purga y control de acceso. Cobertura objetivo ≥ 80 %. |
| VI. Costo cero e IaC | PASS | Sin recursos con costo; almacenamiento en Cloudinary y cómputo en Lambda Free Tier. |
| VII. Rendimiento y observabilidad | PASS | Índices MongoDB declarados y justificados; `correlation-id` propagado. |
| VIII. Nivel fintech | PASS | ASVS nivel 2/3 en autorización y control de acceso; modelo de amenazas STRIDE documentado; validación estricta de borde con lista de permitidos y límite de tamaño; errores uniformes sin enumeración. |

## 9. Riesgos de implementación

- Riesgo de publicar contenido antes de recibir el veredicto de moderación: mitigado orquestando la moderación antes de registrar el estado `Published` y antes de subir la imagen.
- Borrado lógico con imágenes huérfanas: mitigado con `IMediaStorage.DeleteImageAsync` usando el `ImagePublicId` persistido; el fallo se registra y se reporta, nunca se omite silenciosamente.
- Estado ausente o distinto de `Published` en el documento: mitigado con lista de permitidos en `MongoFeedPostVisibilityLookup` (valor por defecto `PendingModeration`, no visible).

## 10. Orden de implementación

1. `T024-01` Actualizar y validar el contrato OpenAPI del spec
2. `T024-02` Implementar: Entidad `FeedPost` con estados, `ImagePublicId` e invariantes de autoría en Domain
3. `T024-03` Implementar: DTOs, `CreateFeedPostCommand`, `DeleteFeedPostCommand` y handlers en Application
4. `T024-04` Implementar: Documento, índices, `MongoFeedPostRepository` y lookup de visibilidad en Infrastructure
5. `T024-05` Implementar: Endpoints `POST` (JSON y multipart) y `DELETE` en `FeedEndpoints.cs` en Api
6. `T024-06` Implementar: Pruebas unitarias de dominio y casos de uso en Application.UnitTests
7. `T024-07` Implementar: Pruebas de integración de endpoints y persistencia en IntegrationTests
8. `T024-08` Actualizar `checklist.md`, `tasks.md`, `specs/README.md` y `memory.md`