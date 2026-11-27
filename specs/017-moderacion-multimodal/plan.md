# Plan 017 — Moderación multimodal de contenido

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Validar imagen y texto antes de publicar ofertas, publicaciones o comentarios, usando `gpt-4o-mini` en modo visión con la política de Sensus (armas, drogas, simbología de odio, contenido sexual y violencia) y comportamiento fail-closed. Sustituye a `omni-moderation-latest`, que dejaba pasar imágenes con armas (decisión D28); `omni-moderation-latest` queda para el texto de Sensi IA (Spec 046).

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Contrato IModerationService y modelo de veredicto |
| Application | Caso de uso de moderación previa a la publicación |
| Infrastructure | Cliente de OpenAI con timeout, reintentos y mapeo de respuesta |
| Api | Integración en los flujos de oferta, publicación y comentario |

`T017-12` añade un puerto de Application para moderar el texto de una publicación. El adaptador de OpenAI reutiliza la política de Sensus y el parser de moderación multimodal. Para `FR-017-08` utiliza el modelo configurado de la familia `gpt-4o-mini` y envía únicamente un bloque de texto. El caso de uso de la Spec 024 consume el veredicto para publicar o poner en cuarentena. El puerto no expone una ruta HTTP.

## 3. Modelo de datos e índices

- moderation_decisions con veredicto, puntajes, umbral y acción
- Para `FR-017-05`, registrar por solo adición la decisión automática, el identificador de contenido, la fecha y la correlación de la solicitud. El registro excluye el contenido, los motivos en texto libre, identificadores de personas y URLs. Registrar antes de devolver una aprobación al flujo de reseñas. La consulta administrativa y la política de retención del historial corresponden a la Spec 021.

## 4. Seguridad

- Clave del proveedor en Secrets Manager
- No registrar el contenido moderado
- Timeout obligatorio
- Sin endpoint público que permita saltarse el flujo

Para `FR-017-08` se considera el texto como datos sin autoridad (suplantación y manipulación de instrucciones). Solo se acepta un veredicto conocido con categorías estructuradas y puntajes válidos (manipulación). Fallos, demoras y respuestas inválidas producen cuarentena (denegación de servicio). Los logs omiten el texto y las respuestas del proveedor (divulgación). El adaptador carece de permisos para publicar (elevación de privilegios). El flujo llamador conserva la evidencia de decisión (repudio). Para `FR-017-09`, `ContentModerationHelper` falla cerrado (`Quarantine`, `Error.Failure("<errorCode>.ModerationUnavailable")`) cuando `ITextModerationService` es nulo, lanza excepción o devuelve nulo (costo 0 USD adicional).

## 5. Observabilidad y rendimiento

- Métricas de latencia y de fallos del proveedor
- Log de decisión sin contenido

## 6. Costo AWS

Para el adaptador de publicaciones aplica EX-002, con techo compartido de 1.00 USD/mes. La modalidad de solo texto no añade recursos AWS ni tokens de imagen.

## 7. Estrategia de pruebas

- Dobles de fallo, timeout y respuesta malformada
- Integración con stub del proveedor

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-017-01` y 6 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |
| VIII | PASS | Matriz STRIDE en §4, presupuesto acotado (`OpenAiSettingsValidator`, ≤ 20 s) y escaneos CI bloqueantes verificados (`ci.yml`, run `4a00d24`) |

> Aplica EX-002 (Apéndice E de la constitución).

### 2.1 Reintento transitorio y presupuesto de latencia (`FR-017-10`, `NFR-017-03`, `T017-03`)

- **Implementación sin dependencias externas:** `ModerationHttpRetryHelper` ejecuta el bucle de envío en `OpenAiTextModerationService` y `OpenAiImageModerationService`, recreando `HttpRequestMessage` por intento con timeout individual y cancelación ligada al cliente. Reintenta únicamente ante `HttpRequestException`, 429, 500, 502, 503 y 504 (máximo `MaxRetries`, defecto 1, límite 2), respetando `Retry-After` ≤ 2 s o `RetryDelayMilliseconds` (defecto 500 ms, límite 2000 ms). No reintenta JSON malformado ni cancelación/timeout.
- **Validación al arrancar (`OpenAiSettingsValidator`):** verifica `TimeoutSeconds` en [1, 20], `MaxRetries` en [0, 2], `RetryDelayMilliseconds` en [0, 2000] y `TimeoutSeconds * (1 + MaxRetries) + MaxRetries * (RetryDelayMilliseconds / 1000.0) <= 20 s` (techo debajo del timeout de 29 s de Lambda). `appsettings.json` ajusta `OpenAiSettings:TimeoutSeconds` a `8` (`8 * 2 + 0.5 = 16.5 s`). Costo adicional: 0 USD.

## 9. Riesgos de implementación

- Dependencia externa sin SLA
- Coste oculto si el proveedor cambia de política

## 10. Orden de implementación

1. `T017-01` Implementar: Contrato IModerationService y modelo de veredicto
2. `T017-02` Implementar: Caso de uso de moderación previa a la publicación
3. `T017-03` Implementar: Cliente de OpenAI con timeout, reintentos y mapeo de respuesta
4. `T017-04` Implementar: Integración en los flujos de oferta, publicación y comentario
5. `T017-05` Definir IModerationService y el modelo de veredicto
6. `T017-06` Implementar el cliente con timeout y fail-closed
7. `T017-07` Registrar la decisión completa para auditoría
8. `T017-08` Encadenar la moderación a la creación de ofertas
9. `T017-09` Implementar las pruebas de integración del flujo completo
10. `T017-10` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
11. `T017-11` Registrar evidencias del Constitution Check
12. `T017-12` Incorporar la entrada de solo texto y probar sus veredictos y fallos conforme a `FR-017-08` y ADR 0015
13. `T017-13` Retirar la ruta independiente `POST /api/v1/media/moderate` conforme a `FR-017-07`. El flujo de carga conserva su moderación interna.
14. `T017-14` Validar también las respuestas del adaptador de compatibilidad de imágenes. Una respuesta sin veredicto booleano, categorías o puntajes válidos se envía a cuarentena conforme a `FR-017-04`.
15. `T017-15` Hacer fail-closed `ContentModerationHelper.EvaluateAsync` y `EnsureApprovedAsync` ante `ITextModerationService` nulo, excepción o retorno nulo conforme a `FR-017-09`.
