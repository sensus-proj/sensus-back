# ADR 0011 — Asistente generativo Sensi con recuperación determinista en el backend

| Campo | Valor |
|---|---|
| Estado | Aceptada |
| Fecha | 2026-11-13 |
| Principios afectados | II, IV, VI, VII |
| Specs relacionados | 046, 007, 013, 017, 018, 030, 041 |
| Fuente | Tesina Sensus §1 y §2.3 · Formato de Aceptación · hoja *Sensi IA* del catálogo de componentes móvil (Pantalla `P-25`, Componente `C-39`) |

## Contexto

La Tesina y el Formato de Aceptación comprometen a **Sensi**, un asistente conversacional donde la
persona declara un presupuesto («Tengo $100 para la despensa») o pide ideas de recetas y recibe un
paquete de ofertas cercanas, abiertas y con existencias que no rebasa ese monto. La aplicación móvil ya
tiene la pantalla (`SensiChatScreen`) y el ViewModel, pero sin contraparte en el backend: la feature
`FT-24` del repositorio móvil está en estado `Borrador`, bloqueada por la decisión `D-6`.

Tres restricciones chocan con esa promesa:

1. **`constitution.md` §2.2** prohíbe proveedores fuera de AWS salvo tres excepciones acotadas
   (moderación, despacho push y Cloudinary), y exige una enmienda para cualquier otra dependencia
   externa.
2. **ADR 0009** fija ranking determinista y descarta servicios de ML gestionados «en esta fase», con
   alcance declarado en las specs 041, 042 y 043.
3. **P-VI** trata el costo cero como requisito de producto, y `FR-045-07` fija un presupuesto de
   1.00 USD mensuales en AWS Budgets.

Un modelo de lenguaje no puede, además, garantizar aritmética: si el paquete se construyera con lo que
el modelo responde, nada asegura que la suma respete el presupuesto ni que las ofertas existan.

## Decisión

- Habilitar un **único proveedor generativo**: la API de OpenAI con el modelo de chat fijado por
  configuración por entorno. No se añade una empresa nueva al proyecto: extiende al proveedor ya
  autorizado en §2.1 para moderación, reutilizando su gestión de clave y su adaptador en
  `Sensus.Infrastructure`.
- Separar **recuperación** de **composición**:
  - La recuperación de ofertas candidatas MUST ser determinista y ocurrir en `Sensus.Application`,
    reutilizando los índices `2dsphere` y de texto de MongoDB y las reglas de visibilidad de la spec 007.
  - El proveedor generativo MUST NOT consultar la base de datos, recibir credenciales ni ejecutar
    herramientas. Recibe el mensaje de la persona y el conjunto acotado de candidatos ya recuperado.
  - La composición generativa se limita a redactar la respuesta y a **seleccionar** elementos dentro de
    ese conjunto.
- Validar el resultado en el backend, nunca confiarlo al modelo:
  - Toda oferta cuyo identificador no pertenezca al conjunto de candidatos MUST descartarse.
  - El total del paquete MUST recalcularse en el backend y MUST NOT exceder el presupuesto declarado.
- Moderar **entrada y salida** con el contrato de §2.4, *fail-closed*: el mensaje de la persona antes de
  enviarse al proveedor y el texto generado antes de devolverse. Ante error, timeout, cuota agotada o
  respuesta inválida, no se entrega texto generado.
- Degradar con gracia: si el proveedor generativo falla, la respuesta se construye con el paquete
  determinista y una redacción fija. La capacidad MUST NOT dejar de responder por indisponibilidad del
  proveedor.
- Acotar el costo de forma verificable: límite de tokens por solicitud, límite de mensajes por persona y
  día, control de tasa según spec 013, caché de solicitudes equivalentes y alarma de facturación.
- Gestionar la clave en AWS Secrets Manager, nunca en el repositorio ni en el cliente móvil.
- Desactivar el entrenamiento del proveedor sobre los datos enviados y MUST NOT incluir PII en el prompt
  ni registrar su contenido en logs.

### Relación con ADR 0009

ADR 0009 **no queda sustituida**. El ranking de búsqueda, recomendaciones y alternativas (specs 041, 042
y 043) sigue siendo determinista y explicable. Esta ADR acota la prohibición de ADR 0009 a esas tres
capacidades y habilita un proveedor generativo exclusivamente para el asistente de la spec 046, donde la
recuperación continúa siendo determinista y solo la redacción es generativa.

## Consecuencias

**Positivas**

- Cumple el compromiso de la Tesina sin mover el ranking fuera del backend.
- El presupuesto se respeta por construcción: lo calcula el backend, no el modelo.
- Una alucinación del modelo no puede inventar ofertas, porque el conjunto de candidatos es cerrado.
- El contrato del proveedor queda tras una interfaz en Application: sustituirlo solo afecta a
  Infrastructure.
- La capacidad responde incluso con el proveedor caído.

**Negativas**

- Introduce la primera dependencia externa con costo variable y rompe el costo cero estricto de P-VI.
- Añade latencia al flujo conversacional.
- Dependencia de un servicio externo sin SLA contractual.
- La calidad de la redacción no es determinista ni reproducible entre ejecuciones.

**Mitigación**

- Excepción a P-VI registrada en el Apéndice E de la constitución, con techo de gasto y fecha de
  expiración.
- Límites de tokens, de mensajes por persona y de tasa, más caché y alarma de facturación
  (`NFR-046-02`).
- Degradación determinista ante fallo (`FR-046-14`).
- Las pruebas verifican la aritmética del paquete y el descarte de identificadores ajenos, que sí son
  deterministas, no la redacción.

## Alternativas consideradas

| Alternativa | Motivo del descarte |
|---|---|
| Paquete por presupuesto 100 % determinista, sin texto generado | Cumple la constitución sin enmienda, pero no satisface el asistente conversacional comprometido en la Tesina y el Formato de Aceptación |
| Amazon Bedrock | Se mantiene dentro de AWS, pero queda fuera del nivel gratuito (prohibido en §2.2) y añade superficie de IAM e IaC sin resolver el problema de costo |
| Modelo de lenguaje autoalojado en Lambda | Inviable en memoria y tiempo de ejecución de Lambda; costo superior al del proveedor gestionado |
| Llamar al proveedor desde la aplicación móvil | Exige la clave en el cliente; prohibido por P-IV y por la propia spec `FT-24` del móvil |
| Dar al modelo acceso a la base de datos mediante herramientas | Rompe P-II y P-IV, impide acotar el costo y hace indemostrable la visibilidad de las ofertas |
| Confiar el total del paquete a la respuesta del modelo | Ningún criterio de aceptación sería verificable; el presupuesto es una obligación aritmética |

## Referencias

- `constitution.md` §2.1, §2.2, §2.4, §2.5, P-IV, P-VI y Apéndice E
- `docs/adr/0004-moderacion-multimodal.md` y `docs/adr/0009-ranking-determinista.md`
- `specs/046-asistente-sensi-ia/`
- `specs/007-ofertas-cercanas/`, `specs/013-rate-limiting/`, `specs/017-moderacion-multimodal/`,
  `specs/018-umbrales-y-bandas/`, `specs/030-eliminacion-cuenta/`, `specs/041-busqueda-inteligente/`
- Repositorio móvil: `specs/FT-24-sensi-ia/` (decisión `D-6`)
