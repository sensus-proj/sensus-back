# Spec 009 — Errores uniformes con ProblemDetails

| Campo | Valor |
|---|---|
| ID | 009 |
| Slug | `errores-problemdetails` |
| Estado | Completo |
| Ola | 0 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | Ninguna |
| Versión | 1.1.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Garantizar que toda respuesta de error use ProblemDetails (RFC 7807) con un identificador de correlación y sin filtrar detalles internos (stack traces, nombres de colecciones o tipos de base de datos).

## 2. Alcance

**Incluye**

- Formato único de errores basado en RFC 7807 (`type`, `title`, `status`, `detail`, `instance`, `extensions.correlationId`).
- Header HTTP `X-Correlation-Id` sincronizado con `extensions.correlationId`.
- Catálogo de códigos de error mapeados a estados HTTP.
- Eliminación de instanciaciones directas de `new ProblemDetails` en capas de endpoints mediante extensiones centralizadas (`ResultExtensions.ToHttpResult()`).

**No incluye**

- Internacionalización dinámica de mensajes (los mensajes se estandarizan en español sin filtrar PII).
- Documentación interactiva de errores en swagger más allá de los esquemas OpenAPI estándar.

## 3. Actores

- Cliente de la API (App móvil y frontends)
- Equipo de soporte e ingeniería (rastreo vía correlationId)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-009-01 | Toda respuesta de error usa ProblemDetails | Must |
| FR-009-02 | Incluir traceId o correlationId en cada error | Must |
| FR-009-03 | No exponer stack traces ni nombres de colecciones | Must |
| FR-009-04 | Mantener un catálogo de códigos de error documentado | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-009-01 | Estabilidad del formato | Sin cambios incompatibles dentro de v1 (RFC 7807 estricto) |

## 6. Criterios de aceptación

### CA-009-01

- **Dado** error de validación o parámetro inválido
- **Cuando** se envía una petición inválida
- **Entonces** 400 Bad Request con ProblemDetails, `instance`, `correlationId` y errores por campo si aplica.

### CA-009-02

- **Dado** excepción no controlada
- **Cuando** se produce un fallo interno
- **Entonces** 500 Internal Server Error con correlationId, `application/problem+json` y sin stack trace ni detalles de colecciones internas.

### CA-009-03

- **Dado** una solicitud rechazada por autenticación, autorización, conflicto o rate limit (401, 403, 409, 429)
- **Cuando** el cliente recibe la respuesta
- **Entonces** contiene la estructura uniforme RFC 7807 y `extensions.correlationId` igual al header `X-Correlation-Id`.

## 7. Catálogo de códigos y tipos de error (FR-009-04)

| Tipo de error (`ErrorType`) | HTTP Status | RFC Reference Type | Title | Ejemplos de Error.Code en el código |
|---|---|---|---|---|
| `Validation` | 400 Bad Request | `https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1` | Bad Request / Error de validación | `Validation.Failed`, `OfferFeed.CoordinatesRequired`, `OfferFeed.InvalidSnapshotId`, `Media.EmptyFile`, `Media.InvalidContentType`, `Media.TooManyFiles`, `User.InvalidProfile`, `Offer.InvalidStock` |
| `Unauthorized` | 401 Unauthorized | `https://datatracker.ietf.org/doc/html/rfc7235#section-3.1` | Unauthorized | `Auth.InvalidCredentials`, `Auth.Unauthorized`, `Offer.Unauthorized` |
| `Forbidden` | 403 Forbidden | `https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3` | Forbidden / Prohibido | `Auth.Forbidden`, `Offer.Forbidden`, `Offer.NoActiveMerchant` |
| `NotFound` | 404 Not Found | `https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4` | Not Found / Recurso no encontrado | `Offer.NotFound`, `Merchant.NotFound`, `User.NotFound` |
| `Conflict` | 409 Conflict | `https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8` | Conflict | `User.EmailAlreadyExists`, `Offer.VersionConflict`, `OfferClaim.AlreadyClaimed` |
| `PayloadTooLarge` | 413 Payload Too Large | `https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.11` | Payload Too Large | `Media.FileTooLarge`, `Media.TotalPayloadTooLarge` |
| Rate Limit (Middleware) | 429 Too Many Requests | `https://datatracker.ietf.org/doc/html/rfc6585#section-4` | Too Many Requests | Exceso de cuota en políticas `AuthStrict`, `Recover`, `ReadHeavy`, `SensitiveWrite`, `AiChat` |
| Excepción interna | 500 Internal Server Error | `https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1` | Error interno del servidor | Excepciones no controladas capturadas por `GlobalExceptionHandler` |

## 8. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| — | Middleware transversal (`GlobalExceptionHandler`, `ResultExtensions`, `SecurityExtensions`) | Aplica a todos los endpoints |

## 8. Dependencias y supuestos

- Depende de: Ninguna
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Cambiar el formato rompe clientes que parsean el cuerpo actual

## 10. Preguntas abiertas

- ¿Se mantiene compatibilidad con el formato actual durante una versión?

## 11. Trazabilidad

- Requisitos de este spec: `FR-009-01`, `FR-009-02`, `FR-009-03`, `FR-009-04`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
