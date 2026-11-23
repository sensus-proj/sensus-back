# Spec 024 — Publicaciones del feed

| Campo | Valor |
|---|---|
| ID | 024 |
| Slug | `publicaciones-feed` |
| Estado | Implementado |
| Ola | 3 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 017, 022 |
| Versión | 2.1.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Permitir crear y eliminar publicaciones con texto e imagen opcional, sujetas a moderación previa **síncrona** y *fail-closed*, con borrado por el autor y purga de la imagen almacenada.

## 2. Alcance

**Incluye**

- Alta de publicación con texto o con texto e imagen en la misma solicitud
- Moderación previa obligatoria y síncrona de texto e imagen (*fail-closed*)
- Carga de imagen por `multipart/form-data` (campo `image`, formatos permitidos JPEG/PNG/WebP, límite 4,5 MB) o referencia ya moderada por JSON (`imageUrl`)
- Borrado por el autor o un administrador, con purga de la imagen mediante `IMediaStorage`
- Estado de la publicación y visibilidad mediante lista de permitidos

**No incluye**

- Edición de publicaciones
- Menciones y etiquetas
- Publicaciones programadas

## 3. Actores

- Usuario autenticado
- Administrador

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-024-01 | Crear publicación con texto de 1 a 500 caracteres e imagen opcional | Must |
| FR-024-02 | No publicar hasta obtener veredicto favorable | Must |
| FR-024-03 | Registrar autor, fechas y estado | Must |
| FR-024-04 | Resolver la moderación de forma síncrona: 201 con la publicación aprobada, 400 ante rechazo o indisponibilidad del moderador | Must |
| FR-024-05 | Permitir al autor eliminar su propia publicación | Must |
| FR-024-06 | Permitir a un administrador eliminar cualquier publicación | Should |
| FR-024-07 | Excluir del feed las publicaciones eliminadas de inmediato | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-024-01 | Latencia percibida | p95 < 1 s hasta aceptar la solicitud |
| NFR-024-02 | Consistencia | Sin publicaciones sin veredicto |

## 6. Criterios de aceptación

### CA-024-01

- **Dado** contenido apto sin imagen, con moderación de texto disponible
- **Cuando** POST /api/v1/feed/posts (JSON o `multipart/form-data`)
- **Entonces** 201 con `status: Published` y publicación visible de inmediato

### CA-024-02

- **Dado** contenido rechazado
- **Cuando** se crea la publicación
- **Entonces** 400 y la publicación no se hace visible ni se almacena imagen

### CA-024-03

- **Dado** sin autenticación
- **Cuando** se intenta publicar
- **Entonces** 401

### CA-024-04

- **Dado** autor elimina su publicación
- **Cuando** DELETE /api/v1/feed/posts/{id}
- **Entonces** 204 y desaparece del feed

### CA-024-05

- **Dado** otro usuario intenta eliminar
- **Cuando** DELETE /api/v1/feed/posts/{id}
- **Entonces** 403

### CA-024-06 — Fail-closed

- **Dado** texto rechazado, imagen rechazada o servicio de moderación no registrado
- **Cuando** POST /api/v1/feed/posts
- **Entonces** 400, ningún registro persistido y ninguna imagen almacenada

### CA-024-07 — Imagen moderada y purga

- **Dado** una imagen adjunta aprobada en `multipart/form-data`
- **Cuando** POST /api/v1/feed/posts y después DELETE /api/v1/feed/posts/{id}
- **Entonces** 201 con `imageUrl` de Cloudinary y `ImagePublicId` persistido; el borrado invoca `IMediaStorage.DeleteImageAsync` con ese identificador

## 7. Contratos expuestos

| Método | Ruta | Autorización | Contenido | Respuestas |
|---|---|---|---|---|
| POST | `/api/v1/feed/posts` | JWT (`RegisteredUserPolicy`) | `application/json` o `multipart/form-data` (`content`/`text`, `image` en JPEG/PNG/WebP) | 201, 400, 401, 403, 413, 429 |
| DELETE | `/api/v1/feed/posts/{id}` | JWT (`RegisteredUserPolicy`) | — | 204, 401, 403, 404 |

## 8. Dependencias y supuestos

- Depende de: 017, 022
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.
- La spec 025 consume `IFeedPostVisibilityLookup`: solo se consideran visibles los posts `Published` y no eliminados.

## 9. Riesgos y mitigaciones

- Publicación visible antes del veredicto si se invierte el orden: mitigado moderando y subiendo la imagen antes de persistir el estado `Published`.
- El borrado lógico podía dejar imágenes accesibles por URL: mitigado con purga inmediata mediante `IMediaStorage.DeleteImageAsync`. Si la purga falla se registra una advertencia y el borrado lógico no se revierte (el contenido ya está fuera del feed).
- Carga de imágenes muy grandes: mitigado con límite de 4,5 MB en el borde y respuesta 413. Formatos no admitidos (distintos de JPEG/PNG/WebP) se rechazan con 400 `FeedPost.UnsupportedImageType` antes de moderar, subir o persistir.
- Visibilidad restringida con lista de permitidos: solo `Published` y `!isDeleted` (spec 025); cualquier otro estado se bloquea por defecto.

## 10. Decisiones tomadas

- **Moderación síncrona** (2026-11-23): se descarta el flujo `202 PendingModeration` porque CA-024-01 exige una publicación visible en la misma petición. `FR-024-04` pasa a 201/400 y toda indisponibilidad del moderador se resuelve *fail-closed*.
- **Borrado lógico con purga inmediata** (2026-11-23): `ImagePublicId` se persiste en el documento y se usa para eliminar la imagen al borrar. Un fallo de purga se registra, no revierte el borrado.
- **Visibilidad por lista de permitidos** (2026-11-23): solo `Published` y `!isDeleted` son visibles; cualquier otro estado (incluidos `PendingModeration` y `Quarantined`) se bloquea por defecto.

## 11. Trazabilidad

- Requisitos de este spec: `FR-024-01`, `FR-024-02`, `FR-024-03`, `FR-024-04`, `FR-024-05`, `FR-024-06`, `FR-024-07`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
