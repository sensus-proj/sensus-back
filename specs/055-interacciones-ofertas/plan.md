# Plan 055 — Registro de interacciones con ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Propuesto |
| Versión | 1.0.0 |
| Constitution Check | PASS (previo a implementación) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Puerto `IOfferInteractionRecorder` en Application, invocado por el feed (042), el detalle de oferta (032) y el
apartado (033). `MongoOfferInteractionRecorder` calcula el seudónimo con `IUserPseudonymizer` y escribe en
`offer_interactions`. La escritura es de mejor esfuerzo: se captura cualquier excepción, se emite la métrica
`InteractionWriteFailed` y la operación principal continúa (son datos analíticos, no de seguridad ni de
moderación, por lo que no aplica *fail-closed*).

Las impresiones de una página se guardan en **un solo documento por página** para reducir el volumen:

```json
{ "type": "impression", "pseudoUserId": "<hmac>", "keyVersion": 2, "snapshotId": "...",
  "rankingVersion": "feed-v1", "page": 1, "items": [ { "offerId": "...", "position": 1, "slot": "ranked",
  "category": "Bakery", "distanceBucket": "0-1" } ], "occurredAtUtc": "..." }
```

`open` y `claim` son documentos de un solo elemento con la misma forma.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `OfferInteraction` (tipos, rango de distancia, invariantes de tamaño) y `DistanceBucket` |
| Application | Puertos `IOfferInteractionRecorder` e `IUserPseudonymizer`; invocación desde 042, 032 (solo registrados) y 033 |
| Infrastructure | `HmacUserPseudonymizer` (clave y versiones desde SSM `/sensus/<entorno>/interactions/hmac-keys`), `MongoOfferInteractionRecorder`, purga por usuario en 030, agregación diaria de métricas |
| Api | Sin endpoints nuevos |

## 3. Modelo de datos e índices

- Colección `offer_interactions`.
- Índice TTL sobre `occurredAtUtc` con `ExpireAfter` de 180 días (`FR-055-06`).
- Índice `{ pseudoUserId: 1, keyVersion: 1 }` para la purga por usuario (`FR-055-07`).
- Índice `{ rankingVersion: 1, type: 1, occurredAtUtc: 1 }` para las métricas diarias (`FR-055-10`).
- Configuración `Interactions`: `Enabled`, `ImpressionSampleRate` (1.0 por defecto), `RetentionDays` (180).

## 4. Seguridad

- Seudonimización con HMAC-SHA256: sin la clave no se puede revertir ni enlazar con `users`. La clave vive en
  SSM *SecureString*, se rota cada 90 días y las versiones previas se conservan solo 180 días para la purga.
- Sin coordenadas, texto libre, correos ni nombres; la categoría y el rango de distancia no identifican.
- Logs de CloudWatch sin seudónimos: solo conteos, tipo de evento y `correlation-id`.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Reidentificar usuarios desde los eventos | Divulgación de información | HMAC con clave en SSM; sin PII ni coordenadas; acceso solo del backend | Bajo |
| Fuga de la clave HMAC | Divulgación de información | SSM *SecureString*, IAM de mínimo privilegio, rotación ≤ 90 días | Bajo |
| Inyectar eventos falsos para sesgar métricas o un modelo futuro | Manipulación | Sin endpoint público; eventos generados por el servidor desde acciones autenticadas | Bajo |
| Retención indefinida de hábitos de consumo | Divulgación de información | TTL de 180 días y purga en eliminación de cuenta | Bajo |
| Escritura lenta o caída degrada el feed o el apartado | Denegación de servicio | Escritura de mejor esfuerzo con tiempo de espera de 200 ms y métrica de fallos | Bajo |
| Eventos de invitados mezclados con registrados | Suplantación | Se omite el registro si el rol es invitado | Bajo |

## 5. Observabilidad y rendimiento

- Métricas EMF: `InteractionWriteFailed` y `InteractionsWritten` por tipo, sin dimensiones de usuario.
- Métricas diarias de calidad (tasa de apertura y de apartado por `slot` y `rankingVersion`) calculadas con una
  agregación indexada y publicadas como métricas EMF (`FR-055-10`).

## 6. Costo AWS

- Cómputo y métricas: sin costo incremental (Lambda existente y métricas dentro del nivel gratuito revisado en
  la spec 015).
- Almacenamiento en MongoDB: con un documento por página, 1 000 usuarios activos × 3 páginas/día × 180 días ≈
  540 000 documentos de ~1.2 KB ≈ 650 MB, más `open` y `claim`. **El clúster confirmado es Atlas M0 (512 MB
  compartidos con todas las colecciones)**, así que la colección se limita a 150 MB (NFR-055-04). Controles:
  1. `ImpressionSampleRate` para muestrear impresiones sin tocar `open` ni `claim`.
  2. `RetentionDays` configurable; se baja a 90 días si el tamaño supera el 70 % del límite.
  3. Métrica EMF diaria con `dbStats`/`collStats` (tamaño de la colección y del clúster) y alarma en CloudWatch al
     70 % de 150 MB y al 70 % de 512 MB (spec 015).
- Con el volumen actual del proyecto el consumo es muy inferior al límite. Si se requiere más almacenamiento,
  se detiene y se pide aprobación (`AGENTS.md` §10).

## 7. Estrategia de pruebas

- Unitarias: `DistanceBucket`, invariantes de `OfferInteraction` y `HmacUserPseudonymizer` (determinismo por
  versión, distinto por clave).
- Integración con Testcontainers: impresiones del feed (CA-055-01), sin PII (CA-055-02), fallo de escritura que
  no rompe la operación (CA-055-03), purga con dos versiones de clave (CA-055-04), invitado sin eventos
  (CA-055-05) y existencia del índice TTL.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | 10 FR y 3 NFR medibles |
| II | PASS | Puertos en Application; HMAC y MongoDB en Infrastructure |
| III | PASS | Sin contratos públicos; sin documentos expuestos |
| IV | PASS | Sin PII; seudónimo con clave en SSM; logs sanitizados |
| V | PASS | Unitarias e integración con MongoDB real (§7) |
| VI | PASS | Sin costo incremental; riesgo de almacenamiento con controles y punto de aprobación (§6) |
| VII | PASS | Índices TTL, de purga y de métricas declarados (§3) |
| VIII | PASS | STRIDE (§4), rotación de clave ≤ 90 días, retención y purga |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Purga incompleta si se pierde una versión de clave antes de 180 días: las versiones se borran solo cuando su
  último evento posible ya expiró.

## 10. Orden de implementación

1. `T055-01` Domain e invariantes
2. `T055-02` Seudonimizador y claves en SSM
3. `T055-03` Recorder e índices
4. `T055-04` Integración con 042, 032 y 033
5. `T055-05` Purga en eliminación de cuenta
6. `T055-06` Métricas
7. `T055-07` Pruebas de integración
