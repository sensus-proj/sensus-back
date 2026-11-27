# Spec 059 — Filtro de groserías y endurecimiento fail-closed de moderación textual

| Campo | Valor |
|---|---|
| Estado | Implementado (retro-spec) |
| Versión | 1.0.0 |
| Fecha | 2026-11-19 |
| Autor | Equipo Sensus |
| Depende de | [017](../017-moderacion-multimodal/spec.md), [018](../018-umbrales-y-bandas/spec.md), [046](../046-asistente-sensi-ia/spec.md), [056](../056-resenas-comercios/spec.md) |

## 1. Contexto y objetivo

Resolver la evasión de lenguaje vulgar en español e inglés (`"pito"`, `"puta"`, variantes *leetspeak*, repeticiones y frases compuestas) detectada en publicaciones de vendedores y extender la moderación preventiva a todo texto ingresado por usuarios y comercios (nombres de perfil, nombres/direcciones de comercio, ofertas, reseñas y respuestas), manteniendo la moderación IA estrictamente **fail-closed** y externalizando todos los umbrales de decisión en configuración, nunca fijos en código de servicio.

## 2. Alcance y exclusiones

### Incluye
- `ProfanityFilter` determinístico en `Sensus.Application/Common/Moderation` con normalización de acentos, *leetspeak* (`0/1/3/4/5/7/8/@/$`), reducción de caracteres repetidos, separación de prefijos/sufijos pegados y frases prohibidas (`Regex` con timeout explícito anti-ReDoS).
- `ContentModerationHelper.EnsureApprovedAsync` aplicado en registro/edición de usuario, registro/edición de comercio, creación/edición/republicación de ofertas, reseñas y respuestas (`CreateStoreReviewCommand`, `ReplyStoreReviewCommand`).
- Extensión de `ITextModerationService` con `ModerateContentTextAsync(string text, string contentContext, CancellationToken)` e implementación dual en `OpenAiTextModerationService` soportando modelos de chat (`gpt-*`, vía JSON estricto) y `omni-moderation-latest` (`/moderations`) en modo estrictamente **fail-closed**.
- Externalización completa de umbrales (`DefaultLowThreshold`, `DefaultHighThreshold`, `CategoryThresholds` y `Sensi:TextModerationThresholds`) en `appsettings.json` / `OpenAiSettings`, prohibiendo valores numéricos de umbral codificados en `OpenAiTextModerationService`.

### Excluye
- Moderación visual de imágenes (`OpenAiImageModerationService`, cubierta en Spec 017/018).
- Diccionarios remotos editables en tiempo real desde panel administrativo.

## 3. Actores y reglas de negocio

| Regla | Descripción |
|---|---|
| `RN-059-01` | Ningún texto con groserías obvias (español/inglés) o variantes disfrazadas supera el filtro local, independientemente de si OpenAI está configurado o accesible. |
| `RN-059-02` | Si `ProfanityFilter` aprueba el texto, se invoca `ITextModerationService.ModerateContentTextAsync` (registrado en DI) cuyo fallo, timeout, JSON inválido o falta de `ApiKey` bloquea la publicación en modo *fail-closed* (`Quarantine` / rechazo). |
| `RN-059-03` | Los umbrales numéricos `Low` y `High` provienen exclusivamente de configuración (`OpenAiSettings` en `appsettings.json` / variables de entorno); `OpenAiTextModerationService` nunca contiene constantes numéricas de umbral en código. |
| `RN-059-04` | En `ContentModerationHelper`, la evaluación en producción exige un `ITextModerationService` válido o rechaza de forma cerrada (`fail-closed`) si no hay verificador configurado cuando se exige moderación estricta. |

## 4. Requisitos funcionales y no funcionales

| ID | Tipo | Requisito | Prioridad / Métrica | Estado |
|---|---|---|---|---|
| `FR-059-01` | FR | Detectar de forma determinística groserías, frases compuestas, variantes *leetspeak* y repeticiones en `ProfanityFilter` con timeout regex anti-ReDoS | Must | Implementado |
| `FR-059-02` | FR | Aplicar `ContentModerationHelper.EnsureApprovedAsync` en registro/perfil de usuario, registro/perfil de comercio, crear/editar/republicar ofertas, reseñas y respuestas | Must | Implementado |
| `FR-059-03` | FR | Mantener `OpenAiTextModerationService.ModerateTextAsync` y `ModerateContentTextAsync` 100 % *fail-closed* ante ausencia de `ApiKey`, error HTTP, timeout o respuesta malformada | Must | Implementado |
| `FR-059-04` | FR | Resolver todos los umbrales (`Sensi:TextModerationThresholds`, `CategoryThresholds`, `DefaultLowThreshold`, `DefaultHighThreshold`) desde `IOptions<OpenAiSettings>` / `appsettings.json`, eliminando constantes de umbral fijas en `OpenAiTextModerationService` | Must | Implementado |
| `FR-059-05` | FR | Configurar `OpenAiSettings` de publicación (`Model`, `DefaultLowThreshold`, `DefaultHighThreshold`, `CategoryThresholds`) explícitamente en `appsettings.json` | Must | Implementado |
| `NFR-059-01` | NFR | Resiliencia ReDoS: toda expresión regular de `ProfanityFilter` utiliza timeout explícito (`<= 1 s`) | 0 vulnerabilidades S6444 | Implementado |
| `NFR-059-02` | NFR | Fail-closed garantizado: 100 % de fallos del proveedor de moderación textual impiden la publicación o actualización | 0 % fail-open | Implementado |
| `NFR-059-03` | NFR | Cobertura unitaria e integración sobre casos positivos, negativos, *leetspeak*, umbrales configurados y fallos de red | Cobertura >= 80 % | Implementado |

## 5. Criterios de aceptación

- **CA-059-01:** Publicar una oferta o comentario con `"pito"`, `"puta"`, `"p3nd3j0"` o `"pppputaaa"` es rechazado con `400 Bad Request` en `< 5 ms` sin depender de red externa.
- **CA-059-02:** Cuando OpenAI devuelve HTTP 500, timeout o JSON corrupto, o no existe `ApiKey`, `OpenAiTextModerationService` retorna `IsApproved = false` (`Decision = "Quarantine"`).
- **CA-059-03:** Cambiar `OpenAiSettings:DefaultLowThreshold` o `CategoryThresholds` en `IOptions<OpenAiSettings>` altera la banda de decisión en `OpenAiTextModerationService` sin recompilar ni existir `0.35`/`0.75` fijos en `GetThresholdForCategory`.
- **CA-059-04:** Nombres comerciales o productos legítimos (`"Cafetería Central"`, `"Tamales de pollo"`, `"Computadora portátil"`, `"Cono de helado artesanal"`, `"Conos de pizza al 2x1"`) son aprobados sin falsos positivos, preservando la detección de `"coño"` al conservar `ñ` durante la normalización (`NormalizeBasic`).

## 6. Dependencias y riesgos

- Riesgo de falsos positivos por coincidencia de subcadenas: mitigado verificando límites de palabra (`tokens`) y whitelist implícita de prefijos/sufijos solo cuando el resto no corresponde a palabras neutras largas.
- Riesgo de falsos negativos por nuevas jergas: mitigado con la segunda capa IA en `OpenAiTextModerationService`.
