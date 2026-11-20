# Plan 042 — Feed de promociones por intereses

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Propuesto |
| Versión | 2.0.0 |
| Constitution Check | PASS (previo a implementación) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

`GetOfferFeedQuery` obtiene las ofertas visibles dentro del radio (índice `2dsphere`, spec 007), calcula el
puntaje de cada una con `OfferFeedRanker` (función pura de Domain), aplica diversidad y exploración, guarda el
orden resultante en Redis como instantánea y sirve la página pedida. Las páginas siguientes leen la instantánea
y vuelven a filtrar visibilidad. Cada página servida emite sus impresiones a la spec 055.

Puntaje por oferta `o` y usuario `u`:

```
score(o,u) = wInterest  · interest(o,u)     // 1 si la categoría está en los intereses (054), si no 0
           + wAffinity  · affinity(o,u)     // [0,1] normalizada desde vistas, apartados y favoritos por categoría
           + wProximity · proximity(o)      // 1 − distancia/radio
           + wUrgency   · urgency(o)        // 1 si vence en ≤ 24 h, decae linealmente hasta 0 a los 7 días
           + wPopularity· popularity(o)     // vistas + apartados de la oferta, normalizado por percentil del radio
```

Desempate: `expiresAtUtc` ascendente y después `offerId`. Exploración: las posiciones múltiplo de
`ExplorationEvery` toman la mejor oferta de exploración no usada; su orden se fija con un hash estable de
`userId + fecha UTC + offerId`, sin aleatoriedad, para cumplir NFR-042-06.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `OfferFeedRanker` (puntaje, diversidad y exploración, puro y probado), `FeedRankingWeights` (value object validado) |
| Application | `GetOfferFeedQuery` + validador (coordenadas, radio 1–50 km, `page`, `pageSize` ≤ 100, `snapshotId` formato GUID), `OfferFeedItemDto`, `OfferFeedPageDto`, puertos `IUserAffinityReader`, `IFeedSnapshotStore`, `IOfferInteractionRecorder` (055) |
| Infrastructure | Agregación de afinidad por categoría (`offer_views`, `offer_claims`, `favorites` cuando exista), `RedisFeedSnapshotStore` con TTL, lectura de pesos versionados desde configuración |
| Api | `GET /api/v1/feed/offers` con `RegisteredUser` y `read-heavy`; `ProblemDetails` en errores |

## 3. Modelo de datos e índices

- `offers`: índice `2dsphere` existente sobre la ubicación, filtrado por estado, stock y vigencia (spec 007).
- `offer_views`: índice existente por `userId` (spec 032). Se agrega por categoría con ventana de 90 días.
- `offer_claims`: índice por `userId`; el TTL de 30 días acota la señal a ese periodo.
- `user_interests` (spec 054): lectura por `userId`, índice único.
- Redis: clave `feed:offers:{userId}:{snapshotId}` con la lista ordenada de `offerId` (máximo 500) y TTL
  `SnapshotTtlMinutes`. La afinidad por usuario se cachea en `feed:affinity:{userId}` con TTL de 10 min.
- Configuración `FeedRanking` por entorno: `Version`, `Weights`, `MaxConsecutiveSameCategory`,
  `ExplorationEvery`, `NewOfferHours`, `SnapshotTtlMinutes`, `DefaultRadiusKm`, `MaxRadiusKm`.

## 4. Seguridad

- Autorización en el servidor con `RegisteredUser`; el invitado recibe `403` sin revelar el motivo interno.
- Validación con lista de permitidos de `lat`, `lng`, `radiusKm`, `page`, `pageSize` y `snapshotId`.
- La instantánea se indexa por `userId` del token: un `snapshotId` de otro usuario no existe para este (sin
  enumeración ni fuga).
- Logs sin PII: solo `correlation-id`, `rankingVersion`, número de ofertas y latencia; nunca coordenadas exactas.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Leer la instantánea de otro usuario adivinando `snapshotId` | Divulgación de información | Clave compuesta con `userId` del token; GUID no secuencial | Bajo |
| Inferir intereses o hábitos de otro usuario desde el feed | Divulgación de información | El feed solo se sirve al dueño del token; sin parámetro `userId` | Bajo |
| Extracción masiva del catálogo de ofertas | Divulgación de información | `read-heavy`, `pageSize` ≤ 100, instantánea ≤ 500 ofertas | Bajo |
| Radio o página extremos para agotar recursos | Denegación de servicio | Radio 1–50 km, tope de instantánea, límite de tasa | Bajo |
| Inflar la popularidad de una oferta con vistas falsas | Manipulación | Vistas deduplicadas por usuario y día (032); popularidad normalizada por percentil | Medio |
| Invitado suplanta a registrado | Suplantación | Rol leído del JWT validado; política `RegisteredUser` | Bajo |
| Cambio de pesos no auditado altera el orden | Repudio | Pesos versionados en configuración desplegada por CI; `rankingVersion` en respuesta e impresiones | Bajo |
| Elevación a datos de comercio ajenos | Elevación de privilegios | Solo se exponen campos públicos de `OfferFeedItemDto` | Bajo |

## 5. Observabilidad y rendimiento

- Métricas EMF: latencia por tipo de página (nueva o instantánea), tamaño de instantánea, aciertos de caché y
  proporción de páginas que relajan diversidad o exploración.
- Si Redis no está disponible, se calcula la página sin instantánea y se responde con `snapshotId` nulo; la
  paginación puede repetir ofertas y se registra como degradación (no afecta seguridad).
- `CancellationToken` propagado hasta MongoDB y Redis.

## 6. Costo AWS

Sin costo incremental: corre en la Lambda existente, usa MongoDB y el Redis de la spec 048. Estimación de
memoria Redis: 500 GUID × 36 B ≈ 18 KB por instantánea activa; con 1 000 usuarios activos simultáneos ≈ 18 MB.

## 7. Estrategia de pruebas

- Unitarias de `OfferFeedRanker`: puntaje, desempate, diversidad, exploración, relajación con pocas ofertas y
  determinismo (cobertura ≥ 80 %).
- Unitarias del validador y de `FeedRankingWeights`.
- Integración con Testcontainers (MongoDB y Redis): éxito, `400`, `401`, `403` invitado, paginación estable
  con `snapshotId`, instantánea vencida, exclusiones y degradación sin Redis.
- Prueba de mapeo de entidad a `OfferFeedItemDto`.

### 7.1 Conjunto de datos fijo para relevancia y diversidad

Las métricas NFR-042-03 a NFR-042-06 se verifican en integración con un *fixture* determinista, versionado en
`tests/Sensus.IntegrationTests/Fixtures/OfferFeed/` y cargado en MongoDB con Testcontainers:

| Elemento | Contenido |
|---|---|
| Ubicación de referencia | Un punto fijo; ofertas repartidas en anillos de 0–1, 1–3, 3–10 y 10–15 km |
| Ofertas | 300 visibles en las 9 categorías del catálogo, con un sesgo deliberado (60 % de una categoría) para probar diversidad; 40 publicadas hace menos de 48 h para la exploración |
| Ofertas excluidas | 30 vencidas, pausadas, sin stock, de comercios inactivos o del propio usuario |
| Usuarios | (a) con 2 intereses; (b) sin intereses pero con vistas y apartados en una categoría; (c) sin intereses ni señales; (d) invitado; (e) comerciante con ofertas propias |
| Reloj | `IDateTimeProvider` fijo para que urgencia, vigencia y exploración sean reproducibles |

Aserciones mínimas: ≥ 60 % de categorías de interés en el top 10 del usuario (a); máximo 3 consecutivas de la
misma categoría en todas las páginas; exploración en las posiciones 5, 10, 15 y 20; 0 ofertas excluidas; orden
idéntico en dos ejecuciones; usuario (c) ordenado por cercanía y urgencia; `403` para (d); (e) sin sus ofertas.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | 14 FR y 7 NFR medibles con criterios Dado/Cuando/Entonces |
| II | PASS | Ranker puro en Domain; agregaciones y Redis en Infrastructure; DI solo en `Program.cs` |
| III | PASS | Una consulta CQRS; DTOs propios; sin entidades ni documentos expuestos |
| IV | PASS | JWT, `RegisteredUser`, validación de borde e invariantes de pesos en Domain |
| V | PASS | Unitarias, integración con Testcontainers y mapeo explícito (§7) |
| VI | PASS | Sin costo incremental (§6) |
| VII | PASS | Paginación 20/100, índices declarados, caché con TTL e invalidación por expiración (§3 y §5) |
| VIII | PASS | STRIDE (§4), lista de permitidos, errores uniformes, logs sin PII y límite de tasa |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- La agregación de afinidad sobre 90 días puede crecer: se cachea por usuario y se acota con índices.
- Calibrar pesos sin datos reales: se arranca con pesos iniciales documentados y se revisan con métricas de 055.

## 10. Orden de implementación

1. `T042-01` Contrato OpenAPI
2. `T042-02` `FeedRankingWeights` y `OfferFeedRanker`
3. `T042-03` Consulta, validador y DTOs
4. `T042-04` Afinidad e intereses en Infrastructure
5. `T042-05` Instantánea en Redis
6. `T042-06` Endpoint
7. `T042-07` Emisión de impresiones (055)
8. `T042-08` Pruebas de integración
9. `T042-09` Métricas y alarmas
