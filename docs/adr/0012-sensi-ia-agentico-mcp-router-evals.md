# ADR 0012 — Arquitectura por 7 capas de Sensi IA agéntico (MCP *in-process*, *Prompt Caching*, *Model Router*, *Guardrails* y *Evals*)

| Campo | Valor |
|---|---|
| Estado | Aceptada |
| Fecha | 2026-11-15 |
| Sustituye a | [ADR 0011](0011-asistente-generativo-sensi.md) |
| Principios afectados | II, III, IV, V, VI, VII |
| Specs relacionados | 046, 047, 002, 004, 007, 013, 017, 018, 030, 033, 041, 048 |

## Contexto

La [ADR 0011](0011-asistente-generativo-sensi.md) y la versión `1.4.0` de `constitution.md` habilitaron inicialmente a **Sensi IA** bajo un modelo restringido de lista cerrada pre-recuperada en backend, con una única llamada generativa para redacción de paquetes de compra.

La evolución funcional definida para el asistente conversacional de Sensus requiere transformar a Sensi IA en un **asistente agéntico multi-capacidad estructurado en 7 capas**, capaz de:

1. Actuar como asistente oficial de Sensus con reglas estrictas de identidad, tono comercial proactivo y reserva absoluta sobre el modelo base, arquitectura y secretos.
2. Responder dudas funcionales y de políticas de la aplicación usando como única fuente de verdad un **documento Markdown versionado**.
3. Decidir qué acciones de solo lectura invocar mediante **MCP (*Model Context Protocol*) y *Function Calling* nativo de OpenAI** para buscar ofertas/productos y consultar la cuenta de la persona autenticada.
4. Heredar estrictamente los permisos (`RoleType` y *scopes* lógicos) derivados del JWT de sesión vigente.
5. Proteger entradas y salidas mediante **guardrails** (OpenAI Moderation API + detección de *Prompt Injection*/*Jailbreak* + redacción automática de PII y credenciales).
6. Optimizar latencia y costo (`EX-001`, techo `5.00 USD/mes`) combinando **Prompt Caching** (carga dividida en bloque estático cacheable y bloque dinámico) con un **Model Router** de dos niveles.
7. Proveer **trazabilidad estructurada** sin exponer PII en CloudWatch Logs y bloquear en CI/CD cualquier regresión de precisión, tono o seguridad mediante **Evals automatizados** (Spec 047).

Esta evolución exige enmendar `constitution.md` (`v1.4.0 → v2.0.0`, enmienda MAJOR porque redefine la prohibición de herramientas de §2.2) para regular el uso controlado de herramientas *in-process* de solo lectura y precisar la separación entre logs técnicos sanitizados (CloudWatch) y auditoría redactada de trazas (MongoDB).

## Decisión

Se adopta la arquitectura por **7 capas** para **Sensi IA** dentro de `Sensus.Application` y `Sensus.Infrastructure`, desplegada dentro de la misma función AWS Lambda actual (`SensusInfraStack`, timeout `29 s`):

### 1. Identidad y reglas inquebrantables (*System Prompt*)
- Sensi IA adopta la identidad oficial de Sensus (plataforma mexicana de descubrimiento de ofertas locales en radio de `1–5 km`, apoyo a micronegocios y economía familiar, reducción de mermas, apartado con QR ISO/IEC 18004 sin envío a domicilio ni comisiones por venta, capa social por proximidad y cumplimiento LFPDPPP).
- Tono formal, amigable, empático y proactivo en orientar decisiones de compra.
- **Hard rules**: prohibido revelar proveedor/modelo subyacente (OpenAI/GPT), arquitectura interna, prompts de sistema o secretos; rechazo seguro ante solicitudes violentas, agresivas o inseguras.

### 2. Conocimiento estático (RAG en Markdown)
- Un documento Markdown versionado (semver + hash SHA-256) contiene la documentación funcional, flujos y políticas de Sensus como única fuente de verdad para responder dudas de la app.
- El contenido íntegro se inserta dentro de etiquetas `<contexto>...</contexto>` en el bloque estático del mensaje `developer`/`system`, garantizando tamaño base cacheable (`≥ 1,024` tokens) sin introducir bases vectoriales ni costos adicionales fuera de `EX-001`.

### 3. Capa Agéntica (MCP *in-process* y *Function Calling*)
- Se implementa un servidor/adaptador **MCP *in-process*** dentro de `Sensus.Infrastructure` ejecutado en el mismo proceso Lambda (`0 USD` de cómputo adicional), expuesto al modelo como definiciones explícitas de *Function Calling* nativo de OpenAI.
- El catálogo inicial queda acotado a herramientas **exclusivamente de solo lectura** bajo principio de mínimo privilegio:
  - `search_offers`: búsqueda geolocalizada y por categoría/presupuesto sobre ofertas vigentes (`Abierto ahora`, stock positivo, comercio activo).
  - `get_offer_detail`: ficha vigente, condiciones y vigencia de una oferta específica.
  - `get_my_account`: perfil resumido no sensible de la persona autenticada (ciudad/preferencias de notificación/estado comercial; nunca hash ni secretos).
  - `get_my_claims`: historial y estado de apartados activos/vigentes de la persona autenticada.
  - `get_my_merchant_offers`: exclusivamente para el rol `Comerciante`, consulta agregada/vigente de sus propias ofertas publicadas.
- Ninguna herramienta ejecuta escrituras ni recibe SQL/queries crudas. El backend limita iteraciones del ciclo agéntico (máx. 3 rondas) para respetar la cota de `29 s` de Lambda/API Gateway, verifica existencias contra MongoDB y recalcula en el servidor todo precio y sumatoria (`SUM <= presupuesto`).

### 4. Autorización heredada del JWT de sesión
- Se reutiliza el **JWT Bearer sin estado** emitido por Spec 002 (no se introduce un IdP ni proveedor OAuth externo adicional, respetando Free Tier P-VI y §2.2).
- Un mapeador determinista en backend deriva los *scopes* permitidos según el `RoleType` verificado del token y su `security_stamp`:
  - `Consumidor`: `ai:chat`, `offers:read`, `account:read`, `claims:read`.
  - `Comerciante`: incluye los anteriores más `merchant:read` únicamente sobre el `merchantId` propio.
  - `Invitado` / sin token válido / rol desconocido: rechazado en el borde (`401` o `403`, cero acceso a Sensi IA).
- La autorización se evalúa **antes** de construir las definiciones de herramientas expuestas al LLM y **de nuevo** dentro del ejecutor backend cada vez que el modelo intenta invocar una herramienta.

### 5. *Guardrails* duales (entrada y salida)
- **Entrada** (antes de llamar al modelo de chat principal):
  1. Clasificador/filtro determinista y heurístico anti-*Prompt Injection*, *Jailbreak* y manipulación de instrucciones.
  2. Llamada **fail-closed** a la API de Moderación de OpenAI (`omni-moderation-latest`, gratuita y de baja latencia).
  3. Cualquier detección positiva o indisponibilidad bloquea la invocación al LLM y responde con mensaje controlado sin consumir tokens de chat.
- **Salida** (antes de entregar al cliente o persistir):
  1. Validación contra catálogo vigente / no invención de identificadores ni precios.
  2. Moderación *fail-closed* de la respuesta final cuando contenga texto libre relevante y verificación de fuga de *hard rules*.
  3. Redacción/enmascaramiento automático de PII y secretos (teléfonos, correos, tokens JWT/Bearer, tarjetas, RFC/CURP o patrones `sk-`, `ghp_`, `AKIA`) mediante detector dedicado en aplicación/infraestructura.

### 6. *Prompt Caching* y *Model Router* con afinidad de sesión
- **Estructura estricta de 2 bloques hacia OpenAI Chat API**:
  1. **Bloque Estático (cacheable)**: definiciones canónicas ordenadas de herramientas permitidas por rol + mensaje `developer`/`system` con la identidad de Sensi IA + contenido íntegro del documento Markdown dentro de `<contexto>`. Se ordenan deterministamente para maximizar los aciertos de caché de prefijo (`≥ 1,024` tokens).
  2. **Bloque Dinámico (no cacheable)**: contexto mínimo de sesión (rol, nombre de pila o alias no sensible, pantalla activa y coordenadas redondeadas si autorizó GPS), ventana acotada de historial reciente y nuevo prompt del usuario.
- **Model Router con afinidad por conversación**:
  - Se fijan por configuración **dos modelos de chat de OpenAI** (nivel rápido/económico y nivel avanzado multi-paso; sin «última versión»).
  - En el **primer turno** de cada conversación, un clasificador ligero de intención/complejidad selecciona el modelo asignado (`assignedModel`) y lo fija en la conversación (`ai_conversations`) mediante **afinidad *sticky***, preservando intacto el prefijo de *Prompt Caching* en turnos subsecuentes del mismo hilo.
  - Solo ante escalamiento explícito documentado (fallo del nivel económico o requerimiento multi-paso complejo fuera de alcance) o en una nueva conversación (`POST /api/v1/ai/conversations` / cambio de rol) se reevalúa el modelo asignado.
  - Se mantiene el techo mensual de `5.00 USD/mes` (`EX-001`) con degradación determinista de paquetes/respuestas base cuando se agote el presupuesto, cuota diaria o falle OpenAI.

### 7. Observabilidad y gate CI/CD de *Evals*
- **CloudWatch Logs**: únicamente eventos estructurados con `correlation-id`, identificador opaco de conversación/usuario (pseudonimizado), `markdownVersion`, `routingTier`, modelo usado, latencia total y de herramientas, `promptTokens`, `cachedTokens`, `completionTokens`, nombres de herramientas ejecutadas (sin argumentos sensibles ni respuestas crudas), hashes SHA-256 de payload y código de resultado/guardrail. **Cero PII y cero prompts/respuestas en claro en CloudWatch.**
- **MongoDB (`ai_trace_logs`)**: registro estructurado de auditoría de prompts, respuestas y trazas de herramientas **previamente redactado de PII/credenciales**, sujeto a índice TTL configurable y borrado en cascada ante `DELETE /api/v1/ai/conversations/{id}` o eliminación de cuenta (Spec 030).
- **Pipeline CI/CD (Spec 047)**: flujo bloqueante de *Evals* automatizados ejecutado ante cambios en el prompt del sistema, catálogo de herramientas MCP o archivo Markdown de conocimiento, midiendo precisión funcional, cumplimiento de *hard rules*/seguridad y tono antes de permitir fusión o despliegue a producción.

## Alternativas consideradas

1. **Desplegar un servidor MCP separado en ECS Fargate o Lambda independiente.** Descartada por incrementar latencia inter-servicio dentro de una ventana de `29 s` y consumir presupuesto/cuota innecesaria fuera de la simplicidad serverless actual.
2. **Conservar lista cerrada estática de ADR 0011 sin *Function Calling* ni RAG Markdown.** Descartada porque limita la cobertura de dudas funcionales sobre flujos de Sensus y obliga a pre-recuperar datos irrelevantes cuando la consulta es solo informativa o requiere consultar apartados activos.
3. **Cambiar de modelo dinámicamente en cada turno sin afinidad de sesión.** Descartada porque el prefijo cacheable de OpenAI está ligado al identificador del modelo; alternar de modelo turno a turno destruiría el ahorro del *Prompt Caching*.
4. **Registrar prompts y respuestas en claro dentro de CloudWatch Logs.** Descartada por violar P-IV y la LFPDPPP respecto a minimización e inmutabilidad de logs operativos compartidos.

## Consecuencias

- **Positivas**:
  - Unifica seguridad, control de costos y trazabilidad auditable en una arquitectura explícita de 7 capas.
  - Elimina costos de servidores adicionales al ejecutar MCP *in-process* y maximizar *Prompt Caching* mediante afinidad *sticky*.
  - Blindaje previo a producción mediante *Evals* en CI/CD.
- **Negativas / Costos aceptados**:
  - Mayor complejidad de pruebas unitarias, de integración y de regresión de *Evals* al integrar bucles agénticos acotados.
  - Se mantiene la excepción temporal `EX-001` al Principio VI con techo `5.00 USD/mes` y degradación determinista garantizada.
- **Notas de alcance**:
  - Esta decisión **no** reemplaza a la [ADR 0009](0009-ranking-determinista.md), que permanece plenamente vigente para las capacidades deterministas de las Specs 041, 042 y 043.
