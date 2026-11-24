# Plan 034 — Creación de oferta

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | Abierto. Falta resolver la cuarentena previa a publicación. |
| Alineado a | `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0 |

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

La verificación de la imagen en la creación puede entregarse antes de resolver la cuarentena. Para `T034-14`, la carga conserva una huella del `textContext` efectivamente evaluado cuando el proveedor otorga una aprobación automática. Al publicar, el backend exige que `imageUrl` coincida con un activo aprobado del dueño del comercio y que la huella coincida con el título y descripción recortados, separados por un salto de línea. El índice `ownerId + secureUrl` acota la búsqueda. Las aprobaciones históricas o manuales sin esa huella requieren una nueva evaluación automatizada. El borrador conserva su flujo privado. Este control no cambia el almacenamiento de cuarentena ni añade servicios.

Usar el flujo de carga de medios como origen de una decisión conjunta. Moderar los bytes y el contexto de título y descripción antes de enviar a Cloudinary. Persistir el veredicto, categorías, puntajes, umbral, acción y fecha, vinculados internamente al propietario, al activo y a una huella del contexto que recibió el proveedor. Las decisiones automáticas históricas sin huella y las liberaciones manuales sin evidencia conjunta quedan excluidas de las alertas. La creación y edición deben validar que el activo aprobado pertenece al comerciante y que la huella coincide con el contenido de la versión a publicar. Un cambio de texto o imagen exige otra decisión. La versión de oferta debe conservar la referencia a la evidencia para que el despacho pueda reevaluarla.

La cuarentena por fallo del proveedor debe resolverse antes de activar este diseño. El flujo actual sube a Cloudinary antes de moderar y no conserva el contexto de la decisión. El Constitution Check del cambio permanece abierto. Utilizar el proveedor existente bajo EX-002 y cuantificar el consumo al incorporar moderación de versiones.

### Propuesta de enmienda conforme a §4.3 — Cuarentena previa a Cloudinary

- Enmienda no aprobada por el responsable el 2026-11-22. Continúa vigente la regla de almacenamiento de la constitución v3.0.0. `FR-034-03` queda abierto hasta disponer de un diseño de cuarentena compatible con los principios IV y VI y §2.4.
- Motivación. Ante timeout o fallo se debe conservar el contenido en cuarentena. El plan 019 vigente usa Cloudinary para retenerlo y el Principio IV impide escribir allí antes de moderar. El Principio VI reserva el almacenamiento multimedia a Cloudinary.
- Redacción propuesta. Permitir retención temporal de imágenes de ofertas pendientes de veredicto en una colección privada de MongoDB, sin URL ni acceso de clientes, con expiración por TTL y máximo efectivo conforme a la spec 019. Mantener Cloudinary como destino de las imágenes aprobadas. Excluir expresamente el contenido prohibido de persistir según §2.3. Mantener cerrado el acceso a contenido inconcluso, rechazado o expirado.
- Impacto. Actualizar 017, 019, 034 y 044 antes de implementar. El flujo de ofertas puede recibir la imagen en la propia solicitud de publicación y las ediciones de contenido quedan sujetas a una nueva evidencia. Las rutas ajenas al flujo de ofertas quedan fuera de este cambio. Verificar capacidad de MongoDB Free Tier, tamaño máximo por documento, acceso interno, TTL, costo y eliminación antes de aprobar el plan de implementación.
- Criterio de verificación. Con timeout o respuesta inválida, cero escrituras en Cloudinary, oferta no visible, imagen inaccesible a clientes y registro de cuarentena con vencimiento. Con aprobación conjunta válida, se habilita la imagen y se puede programar la alerta de la versión aprobada.
