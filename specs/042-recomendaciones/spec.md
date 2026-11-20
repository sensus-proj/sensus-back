# Spec 042 — Feed de promociones por intereses

| Campo | Valor |
|---|---|
| ID | 042 |
| Slug | `recomendaciones` |
| Estado | Pendiente |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 007, 010, 013, 032, 033, 034, 048, 052, 054, 055 (038 y 039 como señales opcionales) |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

> **v2.0.0 (2026-11-20).** Sustituye a «Recomendaciones personalizadas» (v1.0.0, máximo 20 ofertas en
> `GET /api/v1/ml/recommendations`). Por decisión del responsable del producto, la capacidad pasa a ser un
> **feed de promociones propio**, separado del feed social (specs 024 y 026), paginado y ordenado por los
> intereses del usuario. El slug de la carpeta se conserva porque el ID y la carpeta son estables.

---

## 1. Contexto y problema

El usuario registrado necesita una pestaña donde ver, en *scroll* continuo, las promociones cercanas que más le
interesan. El feed social (026) es cronológico y muestra publicaciones, no ofertas. Este spec entrega un feed
de **ofertas** ordenado por una función de puntaje determinista y explicable (ADR 0009), que combina los
intereses elegidos por el usuario (054), su afinidad inferida, la cercanía, la urgencia y la popularidad, con
reglas de diversidad y exploración para que el feed no se encierre en una sola categoría.

## 2. Alcance

**Incluye**

- Feed paginado de ofertas visibles cercanas para usuarios registrados
- Puntaje con pesos configurables y versionados: interés explícito, afinidad inferida, cercanía, urgencia y
  popularidad
- Regla de diversidad (tope de ofertas consecutivas de una misma categoría)
- Cuota de exploración determinista (ofertas nuevas o de categorías fuera de los intereses)
- Orden estable entre páginas mediante una instantánea del ranking en caché distribuida (048)
- Fallback por cercanía y urgencia cuando el usuario no tiene intereses ni señales
- Registro de impresiones para métricas y entrenamiento futuro (055)

**No incluye**

- Acceso de invitados (reciben `403`)
- Mezclar publicaciones del feed social con ofertas
- Modelos entrenados (Random Forest u otros): requieren datos de 055 y un ADR nuevo
- Explicación al usuario final de por qué ve cada oferta
- Notificaciones push del feed

## 3. Actores

- Usuario registrado (Consumidor o Comerciante)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-042-01 | Exponer `GET /api/v1/feed/offers` paginado con el estándar de la spec 010 (20 por defecto, 100 máximo) | Must |
| FR-042-02 | Calcular el puntaje de cada oferta con pesos configurables de interés explícito, afinidad inferida, cercanía, urgencia y popularidad | Must |
| FR-042-03 | Tomar el interés explícito de las categorías elegidas por el usuario (spec 054) | Must |
| FR-042-04 | Derivar la afinidad inferida de vistas (032) y apartados (033), y de favoritos (038) cuando existan | Should |
| FR-042-05 | Excluir ofertas vencidas, pausadas, sin stock, de comercios inactivos y de comercios del propio usuario | Must |
| FR-042-06 | Aplicar diversidad: como máximo `MaxConsecutiveSameCategory` ofertas seguidas de la misma categoría (3 por defecto) | Must |
| FR-042-07 | Reservar una cuota de exploración configurable (1 de cada 5 posiciones por defecto) para ofertas publicadas hace menos de 48 h o de categorías fuera de los intereses, con selección determinista | Must |
| FR-042-08 | Mantener el orden estable entre páginas con una instantánea del ranking identificada por `snapshotId` y TTL configurable (10 min por defecto) | Must |
| FR-042-09 | Aplicar un fallback por cercanía y urgencia cuando el usuario no tenga intereses ni señales | Must |
| FR-042-10 | Devolver `403` a invitados y `401` sin token | Must |
| FR-042-11 | Registrar en la respuesta la versión del ranking (`rankingVersion`) y emitir las impresiones servidas a la spec 055 | Must |
| FR-042-12 | No usar datos personales sensibles en el cálculo: solo categorías, identificadores de oferta y ubicación de la consulta | Must |
| FR-042-13 | Limitar el radio a 1–50 km (10 km por defecto) y validar coordenadas con lista de permitidos | Must |
| FR-042-14 | Aplicar la política de límite de tasa `read-heavy` (spec 013) | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-042-01 | Latencia primera página (sin instantánea) | p95 < 800 ms |
| NFR-042-02 | Latencia páginas siguientes (con instantánea) | p95 < 300 ms |
| NFR-042-03 | Cobertura | ≥ 90 % de usuarios con al menos 20 ofertas en la primera página dentro de 10 km, si existen |
| NFR-042-04 | Relevancia | ≥ 60 % de las 10 primeras posiciones pertenecen a categorías de interés cuando el usuario declaró intereses y hay oferta suficiente |
| NFR-042-05 | Diversidad | 100 % de las páginas cumplen FR-042-06 cuando hay al menos dos categorías disponibles |
| NFR-042-06 | Determinismo | Mismo usuario, insumos, fecha y versión de pesos producen el mismo orden |
| NFR-042-07 | Explicabilidad | 100 % de los pesos documentados, configurados por entorno y versionados |

## 6. Criterios de aceptación

### CA-042-01 — Intereses primero

- **Dado** un usuario con interés en `Bakery` y ofertas cercanas de varias categorías
- **Cuando** `GET /api/v1/feed/offers?lat=..&lng=..`
- **Entonces** `200` y al menos 6 de las 10 primeras posiciones son de `Bakery`, respetando la diversidad

### CA-042-02 — Diversidad

- **Dado** que el 90 % de las ofertas cercanas son de una sola categoría y hay al menos otra categoría
- **Cuando** se consulta la primera página
- **Entonces** no aparecen más de 3 ofertas seguidas de esa categoría

### CA-042-03 — Exploración

- **Dado** un usuario con intereses y ofertas nuevas de otras categorías
- **Cuando** se consulta la primera página
- **Entonces** las posiciones 5, 10, 15 y 20 son ofertas de exploración

### CA-042-04 — Sin intereses ni señales

- **Dado** un usuario recién registrado que omitió elegir intereses
- **Cuando** se consulta
- **Entonces** `200` con ofertas ordenadas por cercanía y urgencia

### CA-042-05 — Paginación estable

- **Dado** la página 1 con `snapshotId` S
- **Cuando** se pide la página 2 con `snapshotId=S` dentro del TTL
- **Entonces** no se repite ninguna oferta de la página 1

### CA-042-06 — Instantánea vencida

- **Dado** un `snapshotId` vencido o inexistente
- **Cuando** se pide cualquier página
- **Entonces** `200` con una instantánea nueva y su `snapshotId`, empezando en la página 1

### CA-042-07 — Exclusiones

- **Dado** una oferta vencida, pausada, sin stock o de un comercio inactivo
- **Cuando** se consulta
- **Entonces** no aparece

### CA-042-08 — Invitado y sin token

- **Dado** un token de invitado o ningún token
- **Cuando** se consulta
- **Entonces** `403` o `401` con `ProblemDetails`

### CA-042-09 — Entrada inválida

- **Dado** `lat` fuera de [-90, 90], `radiusKm` fuera de [1, 50] o `pageSize` > 100
- **Cuando** se consulta
- **Entonces** `400` con `ProblemDetails` sin detalles internos

### CA-042-10 — Determinismo

- **Dado** los mismos insumos, fecha y versión de pesos
- **Cuando** se generan dos instantáneas
- **Entonces** el orden es idéntico

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| GET | `/api/v1/feed/offers` | JWT, usuario registrado (`RegisteredUser`) |

Sustituye a `GET /api/v1/ml/recommendations`, que nunca se implementó. Contrato en
[`contracts/openapi.yaml`](contracts/openapi.yaml).

## 8. Dependencias y supuestos

- 007 (visibilidad de ofertas cercanas), 010 (paginación), 013 (límite de tasa), 032 (vistas), 033 (apartados),
  048 (Redis), 052 (rol invitado), 054 (intereses) y 055 (interacciones).
- 038 (favoritos) y 039 (me gusta) son señales opcionales: el feed funciona sin ellas y las incorpora cuando
  existan, con su peso en configuración.
- La categoría de una oferta pertenece al catálogo de producto `OfferCategory` (spec 034); una oferta sin categoría no recibe puntaje de interés.
- Privacidad: el aviso de privacidad informa la personalización del feed y el registro de interacciones (decisión del 2026-11-20: solo aviso, sin ajuste para desactivarla).
- El comportamiento descrito aplica a la versión v1 de la API.

## 9. Riesgos

- Con pocas ofertas cercanas, la diversidad y la exploración no se pueden cumplir: se relajan y se registra.
- La instantánea puede mostrar una oferta pausada después de calcularse: se filtra de nuevo al servir cada página.
- Pesos mal calibrados producen un feed monótono: se miden NFR-042-04 y NFR-042-05 con datos de 055.

## 10. Preguntas abiertas

- Ninguna bloqueante. Valores por defecto (radio, cuota de exploración, tope de diversidad y TTL) confirmables
  en la revisión del plan.

## 11. Trazabilidad

- Requisitos de este spec: `FR-042-01` a `FR-042-14`, `NFR-042-01` a `NFR-042-07`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
- Decisión: [ADR 0009](../../docs/adr/0009-ranking-determinista.md)
