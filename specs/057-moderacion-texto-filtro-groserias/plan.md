# Plan 057 — Filtro de groserías y moderación textual fail-closed

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec + remediación de umbrales en configuración |
| Versión | 1.0.0 |
| Constitution Check | PASS tras remediación `T057-03` |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

La moderación de texto combina dos barreras secuenciales:
1. **Filtro determinístico (`ProfanityFilter`):** ejecuta normalización Unicode/ASCII, decodificación *leetspeak* y validación contra vocabulario y frases prohibidas antes de cualquier llamada HTTP.
2. **Moderación semántica IA (`OpenAiTextModerationService`):** invoca `ModerateContentTextAsync` (con `gpt-4o-mini` estructurado en JSON o `omni-moderation-latest` vía `/moderations`) siempre bajo comportamiento *fail-closed* y leyendo los umbrales (`DefaultLowThreshold`, `DefaultHighThreshold`, `CategoryThresholds`, `Sensi:TextModerationThresholds`) exclusivamente desde configuración (`appsettings.json` / `IOptions<OpenAiSettings>`).

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Sin dependencias externas |
| Application | `ProfanityFilter.cs`, `ContentModerationHelper.cs`, `ITextModerationService.ModerateContentTextAsync`, integración en handlers de Usuarios, Comercios, Ofertas y Reseñas |
| Infrastructure | `OpenAiTextModerationService.cs` (modo chat JSON + omni-moderation), `OpenAiSettings.cs` y `appsettings.json` con umbrales explícitos en configuración |
| Api | Respuestas `400 Bad Request` (`ProblemDetails`) ante rechazo |
| Tests | `ProfanityFilterTests`, `OpenAiTextModerationServiceTests`, `SocialHandlersTests` |

## 3. Modelo de datos e índices

No crea colecciones propias; protege escrituras sobre `usuarios`, `comercios`, `offers` y `store_reviews`.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Evasión de groserías con números/acentos/repeticiones | Repudio / Manipulación | Normalización diacrítica, *leetspeak* y compresión en `ProfanityFilter` + `gpt-4o-mini` | Bajo |
| Degradación a fail-open si OpenAI falla o no hay clave | Elevación / Bypass | Todo error, timeout, JSON inválido o falta de `ApiKey` retorna `IsApproved = false` (`Quarantine`) | Bajo |
| Ataque ReDoS sobre expresiones regulares del filtro | Denegación de servicio | Regex con `RegexMatchTimeout` acotado y fail-safe (`TimeSpan.FromMilliseconds(500)`) | Bajo |
| Inyección de prompt en el texto evaluado | Manipulación | `systemPrompt` fijo y entrada encapsulada como datos con `temperature = 0.0` | Bajo |

## 5. Observabilidad y rendimiento

- `ProfanityFilter` evalúa en memoria (< 1 ms) y evita consumo de tokens de OpenAI en rechazos evidentes.
- Registro en `_logger` de eventos `Quarantine`/`Rejected` sin incluir el texto crudo ofensivo ni PII.

## 6. Costo AWS

- 0.00 USD incrementales de AWS; reduce el gasto de tokens OpenAI al filtrar primero en memoria.

## 7. Estrategia de pruebas

- Unitarias (`Sensus.Application.UnitTests`): groserías explícitas (`pito`, `puta`), *leetspeak*, repeticiones, palabras benignas sin falsos positivos y rechazo en handlers.
- Integración (`Sensus.IntegrationTests/Services/OpenAiTextModerationServiceTests`): `HttpMessageHandler` simulado verificando fail-closed (sin API key, 500, timeout, JSON inválido), modo chat JSON (`gpt-4o-mini`) y umbrales tomados exclusivamente de `OpenAiSettings`.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec `057-moderacion-texto-filtro-groserias` con requisitos `FR-057-*` |
| II. Clean Architecture | PASS | Reglas en `Application`, cliente OpenAI en `Infrastructure` |
| III. Calidad y pruebas | PASS | `ProfanityFilter` y `OpenAiTextModerationServiceTests` cubren ramas |
| IV. Seguridad y moderación | PASS | *Fail-closed* integral; umbrales en `appsettings.json` tras `T057-03` |
| V. Cobertura y pruebas reales | PASS | Cobertura en `Application` >= 80 % |
| VI. Costo AWS | PASS | 0 USD AWS; pre-filtro local ahorra cuota |
| VII. Paginación e índices | PASS | No aplica (servicio de validación) |
| VIII. DevSecOps / STRIDE | PASS | Anti-ReDoS, STRIDE y sin secretos en código |

## 9. Riesgos de implementación

- Garantizar compatibilidad con pruebas existentes que instancian `OpenAiSettings` sin especificar manualmente `CategoryThresholds`: definir valores iniciales en `OpenAiSettings` y `appsettings.json`, y en `OpenAiTextModerationService.GetThresholdForCategory` leer directamente las propiedades configuradas sin literales numéricos embebidos en el servicio (y tratar umbrales inválidos `<= 0` de forma *fail-closed* o validada).

## 10. Orden de implementación

1. `T057-01` Documentar `ProfanityFilter`, `ContentModerationHelper` y handlers protegidos
2. `T057-02` Publicar contrato de errores en `contracts/openapi.yaml`
3. `T057-03` Externalizar `DefaultLowThreshold`, `DefaultHighThreshold` y `CategoryThresholds` a `appsettings.json` y eliminar literales `0.35`/`0.75` de `OpenAiTextModerationService.cs`, agregando regex timeout explícito en `ProfanityFilter.cs` si falta
4. `T057-04` Ampliar pruebas unitarias e integración verificando umbrales configurados y *fail-closed*
