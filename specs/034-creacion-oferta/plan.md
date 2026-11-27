# Plan 034 — Creación de oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.1.0 |
| Constitution Check | Abierto. Falta resolver la cuarentena previa a publicación. |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Permitir que un comerciante publique una oferta con precio, descuento, stock, vigencia e imágenes, sujeta a moderación previa.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad Offer con invariantes de precio, vigencia y stock |
| Application | CreateOfferCommand, validador y orquestación de moderación |
| Infrastructure | Persistencia, referencias a media y encolado de moderación |
| Api | Endpoint POST /api/v1/offers |

## 3. Modelo de datos e índices

- offers con índice por merchantId + status y por status + endsAt
- `category` con valores del enum de Domain `OfferCategory` y su `OfferCategoryCatalog` (lista de permitidos), guardado como código en inglés
- Bandera de transición `OfferCategory:RequireStrictValidation` (`false` por defecto en `appsettings.json`): solo debe activarse en producción cuando la versión de la app móvil con `T15.15` (`StoreCategory.apiCode`) esté publicada y adoptada; mientras sea `false`, conserva compatibilidad con clientes previos
- Migración única idempotente (`OfferCategoryMigrationService`, marcador `offer_category_v1` en `schema_migrations`) que convierte los nombres en español al código y registra cuántos documentos quedaron nulos (FR-034-09)
- Ejecución (T034-15): `OfferCategoryMigrationHostedService` la invoca una vez por arranque de la Lambda; con el marcador presente solo hace una lectura. Un fallo se registra en logs sin PII y no impide que la API arranque (se reintenta en el siguiente arranque). El marcador se escribe con `InsertOne`: si dos instancias arrancan a la vez, la clave duplicada se trata como "ya ejecutada" y se conserva la evidencia de la primera ejecución (antes un *upsert* podía sobrescribirla con conteos en cero)

## 4. Seguridad

- Dependencia para `FR-044-08`: registrar el vínculo entre la oferta vigente y la decisión de moderación conjunta de su título, descripción e imagen. La decisión debe proceder de un registro interno con propietario y contenido verificables. El estado `Published` por sí solo y la aprobación aislada de un activo multimedia no acreditan `FR-034-03`.
- Para conservar clientes existentes, evaluar un vínculo opcional en el contrato de oferta y exigirlo para entrar al flujo de alertas. Las ediciones de contenido invalidan el vínculo hasta nueva moderación. Las ofertas programadas deben evaluarse al iniciar su vigencia. Falta diseñar el enlace y el mecanismo de programación antes de activar alertas, con contratos y pruebas trazados a las specs 034, 035 y 044.

- El comercio proviene del token
- Moderación previa obligatoria
- Sin publicar imágenes antes del veredicto

### Modelo de amenazas (STRIDE — Principio VIII)

| Amenaza | Escenario | Control existente (con archivo/prueba) | Riesgo residual |
|---|---|---|---|
| **S — Spoofing** | Creación de ofertas suplantando a otro comercio | El comercio se resuelve desde `ICurrentUserService.UserId` del JWT verificado (`CreateOfferCommandHandler.cs`, `CreateOfferEndpointsTests.cs`) | Bajo |
| **T — Tampering** | Manipulación de precio final, descuento > 90 % o enlace de imagen no moderada | Precio final calculado en `Offer.Create` (`OfferTests.cs`); `HasApprovedImageAsync` valida dueño y huella `ModeratedTextFingerprint`; texto re-moderado en `ContentModerationHelper.cs` | Medio (cuarentena de imagen sigue abierta por `FR-034-03`) |
| **R — Repudiation** | Publicación, rechazo o cuarentena sin evidencia de decisión | Registro `ModerationAuditRecorder` con veredicto, acción y `CorrelationId` (`CreateOfferCommandHandler.cs`) | Bajo |
| **I — Information Disclosure** | Exposición pública de borradores (`saveAsDraft=true`) o texto sensible en logs | Filtro `Status != Draft` en detalle/cercanas (`CreateOfferEndpointsTests.cs`) y `CreateOfferCommand : ISensitiveRequest` en logs (`ArchitectureTests.cs`) | Bajo |
| **D — Denial of Service** | Creación masiva de ofertas activas por un comercio | Límite configurable `Offers:MaxActivePerMerchant=20` (`OfferLimitSettings.cs`, `ix_offers_merchant_status_ends_at`, `MongoOfferActiveCountIntegrationTests.cs`) con respuesta `409 Conflict` | Bajo (+1 transitorio posible ante ráfaga concurrente exacta) |
| **E — Elevation of Privilege** | Uso del endpoint por usuario consumidor o invitado, o bypass de moderación en borrador | Política `MerchantOnlyPolicy` en `OfferEndpoints.cs` (`401`/`403`) y aislamiento de transición desde `Draft` en `ChangeOfferStatusCommandHandler.cs` | Bajo |

## 5. Observabilidad y rendimiento

- Métricas de ofertas creadas, rechazadas y en revisión

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de validación de precio y vigencia
- Integración de Draft, Published y pendiente de moderación

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-034-01` y 6 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Publicar antes del veredicto si se invierte el orden de las operaciones
- Un límite de ofertas mal calibrado bloquea comercios legítimos

## 10. Orden de implementación

1. `T034-01` Actualizar y validar el contrato OpenAPI del spec
2. `T034-02` Implementar: Entidad Offer con invariantes de precio, vigencia y stock
3. `T034-03` Implementar: CreateOfferCommand, validador y orquestación de moderación
4. `T034-04` Implementar: Persistencia, referencias a media y encolado de moderación
5. `T034-05` Implementar: Endpoint POST /api/v1/offers
6. `T034-06` Modelar Offer y sus invariantes
7. `T034-07` Encadenar moderación y publicación
8. `T034-08` Implementar el límite de ofertas activas
9. `T034-09` Probar el 202 de moderación pendiente
10. `T034-10` Implementar las pruebas de integración del flujo completo
11. `T034-11` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
12. `T034-12` Registrar evidencias del Constitution Check

`T034-13` implementa el borrador independiente de `FR-034-04`. El parámetro opcional `saveAsDraft` conserva el contrato existente al omitirse. El borrador es visible en el inventario del propietario, se filtra en el detalle y otros consumidores públicos, y permanece fuera de los cambios de estado que podrían habilitar su publicación sin veredicto. No genera avisos, no invoca al proveedor y no usa almacenamiento multimedia adicional. El costo incremental es cero.

STRIDE del borrador. El token y el comercio activo protegen contra suplantación y elevación. El servidor fija el estado y excluye el borrador de lecturas públicas y notificaciones para impedir manipulación y divulgación. Se mantienen los límites de entrada y las políticas de autorización. El identificador de correlación enlaza el resultado con la solicitud.

### Diseño pendiente de implementación para FR-034-03

La verificación de la imagen en la creación puede entregarse antes de resolver la cuarentena. Para `T034-14` y `T034-17`, la carga conserva una huella del `textContext` efectivamente evaluado cuando el proveedor otorga una aprobación automática. Al publicar y en la re-moderación diferida, `OfferMediaApprovalHelper` + `ModeratedTextFingerprint.CreateOfferCandidates` exigen que `imageUrl` coincida con un activo aprobado del dueño del comercio y que la huella coincida con el título y descripción recortados, aceptando tanto el separador canónico (`"\n"`) como el separador `" - "` enviado por clientes móviles existentes. El índice `ownerId + secureUrl` acota la búsqueda. Las aprobaciones históricas o manuales sin esa huella requieren una nueva evaluación automatizada. El borrador conserva su flujo privado. Este control no cambia el almacenamiento de cuarentena ni añade servicios.

Usar el flujo de carga de medios como origen de una decisión conjunta. Moderar los bytes y el contexto de título y descripción antes de enviar a Cloudinary. Persistir el veredicto, categorías, puntajes, umbral, acción y fecha, vinculados internamente al propietario, al activo y a una huella del contexto que recibió el proveedor. Las decisiones automáticas históricas sin huella y las liberaciones manuales sin evidencia conjunta quedan excluidas de las alertas. La creación y edición deben validar que el activo aprobado pertenece al comerciante y que la huella coincide con el contenido de la versión a publicar. Un cambio de texto o imagen exige otra decisión. La versión de oferta debe conservar la referencia a la evidencia para que el despacho pueda reevaluarla.

La cuarentena por fallo del proveedor debe resolverse antes de activar este diseño. El flujo actual sube a Cloudinary antes de moderar y no conserva el contexto de la decisión. El Constitution Check del cambio permanece abierto. Utilizar el proveedor existente bajo EX-002 y cuantificar el consumo al incorporar moderación de versiones.

### Propuesta de enmienda conforme a §4.3 — Cuarentena previa a Cloudinary

- Enmienda no aprobada por el responsable el 2026-11-22. Continúa vigente la regla de almacenamiento de la constitución v3.0.0. `FR-034-03` queda abierto hasta disponer de un diseño de cuarentena compatible con los principios IV y VI y §2.4.
- Motivación. Ante timeout o fallo se debe conservar el contenido en cuarentena. El plan 019 vigente usa Cloudinary para retenerlo y el Principio IV impide escribir allí antes de moderar. El Principio VI reserva el almacenamiento multimedia a Cloudinary.
- Redacción propuesta. Permitir retención temporal de imágenes de ofertas pendientes de veredicto en una colección privada de MongoDB, sin URL ni acceso de clientes, con expiración por TTL y máximo efectivo conforme a la spec 019. Mantener Cloudinary como destino de las imágenes aprobadas. Excluir expresamente el contenido prohibido de persistir según §2.3. Mantener cerrado el acceso a contenido inconcluso, rechazado o expirado.
- Impacto. Actualizar 017, 019, 034 y 044 antes de implementar. El flujo de ofertas puede recibir la imagen en la propia solicitud de publicación y las ediciones de contenido quedan sujetas a una nueva evidencia. Las rutas ajenas al flujo de ofertas quedan fuera de este cambio. Verificar capacidad de MongoDB Free Tier, tamaño máximo por documento, acceso interno, TTL, costo y eliminación antes de aprobar el plan de implementación.
- Criterio de verificación. Con timeout o respuesta inválida, cero escrituras en Cloudinary, oferta no visible, imagen inaccesible a clientes y registro de cuarentena con vencimiento. Con aprobación conjunta válida, se habilita la imagen y se puede programar la alerta de la versión aprobada.

### Diseño de T034-18 — Límite configurable de ofertas activas por comercio (`FR-034-05`)

- **Configuración (`OfferLimitSettings`)**: sección `"Offers"` (`MaxActivePerMerchant`, por defecto `20`, rango `[1, 1000]` validado con `ValidateDataAnnotations().ValidateOnStart()`).
- **Índice compuesto e implementación**: `ix_offers_merchant_status_ends_at` (`{ MerchantId: 1, Status: 1, EndsAt: 1 }`) en `offers` permite ejecutar `CountDocumentsAsync` sobre ofertas con `Status == "Published"` y `EndsAt > nowUtc` mediante `IXSCAN` acotado por comercio (`MongoOfferRepository.CountActiveByMerchantAsync`). Los estados `Draft`, `Paused`, `Expired`, `Archived` y `Published` con `EndsAt <= nowUtc` quedan excluidos del conteo.
- **Puntos de control**: `CreateOfferCommandHandler` (solo cuando `!SaveAsDraft`, después de `Offer.Create` y antes de moderar/persistir), `ChangeOfferStatusCommandHandler` (al reanudar de no-`Published` a `Published`) y `RepublishOfferCommandHandler` (antes de moderar/persistir). Si `activeCount >= maxActive`, devuelven `Error.Conflict("Offer.ActiveLimitReached", ...)` -> `409 Conflict`.
- **Decisión provisional del ejecutor — revisar con Rodrigo**: Ante dos publicaciones simultáneas del mismo comercio en el límite (p. ej. 19 -> 20), la verificación previa por conteo sin transacción distribuida acepta un sobrepaso transitorio mínimo (máximo +1 por ventana de milisegundos). Alternativa descartada: contador atómico en colección aparte o transacciones multi-documento en M0 (overhead innecesario en Atlas Free Tier M0 y riesgo de deriva si expiran ofertas por tiempo sin decrementar el contador). Si se requiere límite estricto bajo concurrencia extrema, migrar en spec posterior a documento de cuota con lease temporal.
- **Costo AWS/Atlas**: Cero adicional (índice pequeño sobre colección existente dentro de M0 Free Tier).

### Diseño de T034-19 — Moderación pendiente `202 Accepted` y re-moderación diferida (`FR-034-07`)

- **Flujo `CreateOfferCommandHandler` + `OfferEndpoints`**: Si `!SaveAsDraft` y `ContentModerationHelper.EvaluateAsync` devuelve `Quarantined` (indisponibilidad/timeout), la oferta pasa a `OfferStatus.PendingModeration` con `NextModerationRetryAtUtc = now + RetryDelaySeconds`, se registra auditoría `best-effort` (`Quarantined`), se omite caché pública y `INearbyOfferNotifier`, y el endpoint responde `202 Accepted` con cabecera `Location`.
- **Ocultamiento público (`GetOfferDetailQueryHandler` y consumidores)**: `PendingModeration` y `Rejected` quedan ocultos (`404 NotFound`) en el detalle público y excluidos de cercanía, búsqueda, feed, favoritos, likes, vistas, compartir y citas IA. Únicamente el propietario autenticado del comercio puede consultar su propia oferta `PendingModeration` (sin escribirla en caché pública Redis) o verla en `/api/v1/merchants/my-offers`.
- **Re-moderación (`ProcessPendingOfferModerationCommand` + `OfferModerationRetryBackgroundService`)**: Consulta lotes mediante el índice `ix_offers_pending_moderation_retry` (`Status`, `NextModerationRetryAtUtc`). Si se aprueba, registra auditoría `System` *fail-closed*, cambia a `Published` (o `Expired`), invalida caché y dispara `INearbyOfferNotifier`; si se rechaza o agota `Moderation:OfferRetry:MaxAttempts` (5), pasa a `Rejected`.
- **Decisión provisional del ejecutor — revisar con Rodrigo**: Al agotar `MaxAttempts` (5 intentos) la oferta pasa *fail-closed* a `OfferStatus.Rejected` en lugar de permanecer en `PendingModeration` indefinidamente o auto-publicarse, y el detalle `PendingModeration` es visible para el propietario sin caché además del inventario privado. Alternativa descartada: ocultarlo también al propietario en `GET /offers/{id}` o mantener cola infinita.
