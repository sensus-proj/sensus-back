# Spec 046 — Asistente conversacional Sensi

| Campo | Valor |
|---|---|
| ID | 046 |
| Slug | `asistente-sensi-ia` |
| Estado | Pendiente |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 002, 007, 013, 017, 018, 030, 041 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v1.4.0 · `AGENTS.md` v1.2.0 |

---

## 1. Contexto y problema

La persona declara un presupuesto en lenguaje natural («Tengo $100 para la despensa») o pide ideas de
recetas, y espera un paquete de ofertas cercanas, vigentes y con existencias cuya suma no rebase ese
monto. El backend no expone hoy ninguna capacidad conversacional y la aplicación móvil tiene la pantalla
sin contraparte (feature `FT-24`, bloqueada por la decisión `D-6`).

La recuperación de ofertas MUST permanecer determinista: un modelo de lenguaje no garantiza aritmética ni
existencia de las ofertas que menciona. El asistente se construye por tanto en dos mitades: recuperación
determinista en el backend y redacción generativa acotada al conjunto recuperado (ADR 0011).

## 2. Alcance

**Incluye**

- Creación y recuperación de conversaciones por persona autenticada
- Envío de mensajes con presupuesto y ubicación aproximada
- Recuperación determinista de ofertas candidatas por cercanía, vigencia y existencias
- Selección de un paquete cuya suma no exceda el presupuesto, calculada y validada en el backend
- Redacción generativa de la respuesta, acotada al conjunto de candidatos
- Moderación de entrada y salida, *fail-closed*
- Historial de conversación paginado y purgable
- Límites de tokens, de mensajes por persona y día, y control de tasa

**No incluye**

- Acceso del proveedor generativo a la base de datos, a credenciales o a herramientas
- Cálculo de importes o totales por parte del proveedor generativo
- Modificación del ranking determinista de las specs 041, 042 y 043
- Apartado o reserva de las ofertas sugeridas, que sigue el flujo de la spec 033
- Modo invitado
- Conversaciones entre personas o soporte humano
- Recetas con información nutricional o sanitaria

## 3. Actores

- Usuario autenticado con rol Consumidor

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-046-01 | Crear una conversación o devolver la activa de la persona autenticada | Must |
| FR-046-02 | Rechazar con 401 las solicitudes sin JWT válido y con 403 las de sesión invitada | Must |
| FR-046-03 | Moderar el mensaje de la persona antes de enviarlo al proveedor generativo, *fail-closed* | Must |
| FR-046-04 | Recuperar las ofertas candidatas de forma determinista por cercanía, vigencia y existencias, sin intervención del proveedor generativo | Must |
| FR-046-05 | Interpretar el presupuesto declarado en el mensaje y rechazar montos no positivos o no interpretables con un mensaje accionable | Must |
| FR-046-06 | Calcular en el backend el paquete de ofertas y recalcular su total, garantizando que no exceda el presupuesto | Must |
| FR-046-07 | Descartar toda oferta cuyo identificador no pertenezca al conjunto de candidatos entregado al proveedor | Must |
| FR-046-08 | Moderar el texto generado antes de devolverlo, *fail-closed* | Must |
| FR-046-09 | Excluir ofertas vencidas, pausadas, sin existencias o de comercios inactivos | Must |
| FR-046-10 | Persistir los mensajes de la conversación y devolverlos paginados | Must |
| FR-046-11 | Limitar los tokens por solicitud y el número de mensajes por persona y día, con valores configurables | Must |
| FR-046-12 | Resolver la clave del proveedor generativo desde AWS Secrets Manager | Must |
| FR-046-13 | No registrar en logs el contenido del mensaje, el texto generado ni PII | Must |
| FR-046-14 | Degradar a paquete determinista con redacción fija cuando el proveedor generativo falle, agote su cuota o exceda el tiempo de espera | Must |
| FR-046-15 | Purgar las conversaciones y sus mensajes al eliminar la cuenta | Must |
| FR-046-16 | Desactivar el entrenamiento del proveedor sobre los datos enviados | Must |
| FR-046-17 | Reutilizar la respuesta en caché ante solicitudes equivalentes de la misma persona dentro del TTL configurado | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-046-01 | Latencia de respuesta del asistente | p95 < 8 s; tiempo de espera máximo 30 s |
| NFR-046-02 | Costo mensual del proveedor generativo | ≤ 1.00 USD con alarma de facturación al 80 % |
| NFR-046-03 | Exactitud del paquete | 100 % de paquetes con suma ≤ presupuesto, verificado en el backend |
| NFR-046-04 | Ofertas inexistentes en la respuesta | 0 |
| NFR-046-05 | Respuestas entregadas sin moderar | 0 |
| NFR-046-06 | Disponibilidad ante caída del proveedor generativo | 100 % con degradación determinista |
| NFR-046-07 | PII o contenido de prompts en logs | 0 |

## 6. Criterios de aceptación

### CA-046-01

- **Dado** una persona autenticada con ofertas cercanas vigentes
- **Cuando** POST /api/v1/ai/messages con un mensaje que declara un presupuesto de 100
- **Entonces** 200 con texto de respuesta y un paquete cuya suma recalculada es ≤ 100

### CA-046-02

- **Dado** que el proveedor generativo devuelve un identificador de oferta ajeno al conjunto de candidatos
- **Cuando** se compone la respuesta
- **Entonces** ese elemento se descarta y no aparece en el paquete

### CA-046-03

- **Dado** que el proveedor generativo responde con un total inferior al real
- **Cuando** se compone la respuesta
- **Entonces** prevalece el total recalculado por el backend

### CA-046-04

- **Dado** que el proveedor de moderación marca el mensaje de la persona
- **Cuando** se envía
- **Entonces** no se invoca al proveedor generativo y se responde con el rechazo correspondiente

### CA-046-05

- **Dado** que el proveedor generativo agota el tiempo de espera
- **Cuando** se envía un mensaje
- **Entonces** 200 con el paquete determinista y redacción fija, sin texto generado

### CA-046-06

- **Dado** una sesión invitada
- **Cuando** se invoca cualquier ruta del asistente
- **Entonces** 403 sin consumir cuota del proveedor

### CA-046-07

- **Dado** una persona que supera el límite diario de mensajes
- **Cuando** envía otro mensaje
- **Entonces** 429 sin invocar al proveedor generativo

### CA-046-08

- **Dado** una conversación con mensajes
- **Cuando** se elimina la cuenta
- **Entonces** la conversación y sus mensajes dejan de existir

### CA-046-09

- **Dado** una oferta sin existencias
- **Cuando** se solicita un paquete
- **Entonces** no aparece entre los candidatos ni en el paquete

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| POST | `/api/v1/ai/conversations` | JWT |
| POST | `/api/v1/ai/messages` | JWT |
| GET | `/api/v1/ai/conversations/{id}` | JWT |

## 8. Dependencias y supuestos

- Depende de: 002, 007, 013, 017, 018, 030, 041
- Habilitado por ADR 0011 y por la enmienda `constitution.md` v1.4.0 (§2.1, §2.2, §2.4, §2.5).
- La excepción a P-VI por el costo del proveedor está registrada en el Apéndice E de la constitución
  (`EX-001`).
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.
- La ubicación que envía el cliente es aproximada; el radio máximo es el de la spec 007.

## 9. Riesgos

- El costo del proveedor crece con el uso y puede rebasar el techo mensual
- La interpretación del presupuesto en lenguaje natural puede fallar con montos ambiguos
- La latencia del proveedor degrada la experiencia conversacional
- Un prompt malicioso puede intentar desviar la redacción; el conjunto cerrado de candidatos y la
  moderación de salida acotan el daño
- La calidad de la redacción no es reproducible entre ejecuciones

## 10. Preguntas abiertas

- ¿Debe conservarse el historial más allá de un número fijo de mensajes por conversación?
- ¿Se habilita el asistente para el rol Comerciante en una fase posterior?

## 11. Trazabilidad

- Requisitos de este spec: `FR-046-01` … `FR-046-17`, `NFR-046-01` … `NFR-046-07`
- Decisión: [`../../docs/adr/0011-asistente-generativo-sensi.md`](../../docs/adr/0011-asistente-generativo-sensi.md)
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
- Contraparte móvil: `specs/FT-24-sensi-ia/` del repositorio móvil
