# Plan 061 — Anonimización de contenido y cierre comercial

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Cerrado |
| Versión | 2.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Resumen técnico

Al producirse la baja de cuenta (spec 060), el mismo handler delega en un caso de uso de cierre que: (1) anonimiza publicaciones y comentarios del feed mediante actualizaciones en lote por autor, (2) expira los apartados activos del comercio antes de archivar sus ofertas, y (3) copia la evidencia de moderación de las sanciones del usuario a una colección `moderation_audit` sin PII con TTL de 90 días. Se mantiene el comportamiento existente de borrado de comercio, tokens, fotos, trazas, intereses e interacciones.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `FeedPost.Anonymize`, `FeedComment.Anonymize`, entidad `ModerationAudit` con `SubjectKey` hash; nuevos contratos de repositorio |
| Application | `IUserContentAnonymizer`, `CloseAccountContentCommand`/handler, ampliación de `DeleteAccountCommandHandler` |
| Infrastructure | Implementaciones Mongo de anonimización por lote, expiración por comercio y auditoría; documentos, TTL e índices |
| Api | Sin endpoints nuevos; configuración `AccountDeletion:ModerationAuditRetentionDays` |

## 3. Modelo de datos e índices

- `feed_posts`: actualización en lote por `authorUserId` usando el índice existente `ix_feed_posts_author_created_at`; campos tocados: `authorUserId`, `authorDisplayName`, `authorPhotoUrl`, `updatedAtUtc`.
- `feed_comments`: índice nuevo `ix_feed_comments_author` para la actualización en lote; campos tocados análogos.
- `offer_claims`: índice nuevo `ix_offer_claims_merchant_status`; transición atómica `Active → Expired` con `expiredAtUtc` antes del archivado.
- `moderation_audit`: campos `subjectKey` (SHA-256 del `userId`), `decision`, `categories`, `scores`, `thresholdApplied`, `action`, `recordedAtUtc`, `expiresAtUtc`; TTL con `ExpireAfter = TimeSpan.Zero` sobre `expiresAtUtc`; índice por `subjectKey`.
- La colección `sanctions` no se modifica: sigue siendo de solo adición.

## 4. Seguridad y modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Escenario | Control existente (con archivo/prueba) | Riesgo residual |
|---|---|---|---|
| **S — Spoofing** | Disparo directo del cierre comercial con token de tercero | `CloseAccountContentCommandHandler` invocado únicamente vía `ISender` desde `DeleteAccountCommandHandler` autenticado (`DeleteAccountEndpointTests.cs`) | Bajo |
| **T — Tampering** | Repetición de la baja o escritura parcial en colecciones | Actualizaciones idempotentes (`Guid.Empty` ya anonimizado) y auditoría con clave determinista por sanción (`CloseAccountContentCommandHandler.cs`) | Bajo |
| **R — Repudiation** | Auditoría eliminada antes de 90 días o sin trazabilidad | TTL configurable a 90 días en `moderation_audit`, colección `sanctions` append-only y evento `SecurityAudit AccountContentAnonymized` con `CorrelationId` (`MongoDbContext.cs`) | Bajo |
| **I — Information Disclosure** | Contenido público o auditoría filtrando PII tras la baja | Anonimización en lote en publicaciones/comentarios y copia con `subjectKey` SHA-256 irreversible sin correo, nombre ni foto (`CloseAccountContentCommandHandler.cs`) | Bajo |
| **D — Denial of Service** | Carga excesiva por recorrido completo de colecciones al cerrar cuenta | Índices por autor y `ix_offer_claims_merchant_status`; sin `GetAll` en la ruta de baja (`MongoDbContext.cs`) | Bajo |
| **E — Elevation of Privilege** | Canje de apartados `Active` tras cierre de comercio | Expiración explícita de apartados por comercio antes de archivar ofertas y validación de comercio activo al canjear (`RedeemClaimCommandHandler.cs`) | Bajo |

## 5. Observabilidad y rendimiento

- El cierre registra `SecurityAudit AccountContentAnonymized Posts={Count} Comments={Count} ClaimsExpired={Count} AuditEntries={Count}` con `correlation-id`, sin PII.
- `NFR-061-01` y `NFR-061-02` se verifican con pruebas que consultan el estado resultante de las cuatro colecciones.
- `NFR-061-03` se verifica comprobando `expiresAtUtc` y la ausencia de campos personales en el documento de auditoría.

## 6. Costo AWS

Sin costo incremental: solo operaciones MongoDB sobre la instancia existente e índices adicionales. No se crean recursos AWS ni se ejecuta `cdk deploy`.

## 7. Estrategia de pruebas

- Unitarias: `FeedPost.Anonymize`, `FeedComment.Anonymize`, `ModerationAudit.CreateFromSanction`, handler de cierre con repositorios simulados (con apartados activos, idempotencia y PII).
- Integración HTTP: baja de cuenta con publicaciones, comentarios, apartados y sanciones; verificación de estados finales.
- Persistencia: índices y TTL declarados en `MongoDbContext`; pruebas con MongoDB real (Testcontainers) cuando el daemon esté disponible, sin dobles que simulen el motor.
- Regresión: una prueba que fallaba antes del arreglo (apartado `Active` tras archivar oferta) se añade y pasa.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Cuatro artefactos con `FR-061-01` a `FR-061-06` y `NFR-061-01` a `NFR-061-04` |
| II. Clean Architecture | PASS | Reglas en Domain, orquestación en Application, Mongo en Infrastructure, composición en Api |
| III. CQRS y contratos | PASS | Sin endpoints nuevos, DTOs sin entidades de dominio, comando de cierre separado |
| IV. Seguridad y privacidad | PASS | STRIDE en §4, anonimización verificable, sin PII en auditoría ni logs |
| V. Verificación automatizada | PASS | Pruebas unitarias, de integración y de regresión con cobertura ≥ 80 % |
| VI. Costo cero e IaC | PASS | Sin recursos nuevos |
| VII. Rendimiento y observabilidad | PASS | Actualizaciones indexadas y auditoría con métricas |
| VIII. DevSecOps de nivel fintech | PASS | Auditoría de solo adición con retención declarada, mínimo privilegio y sin secretos |

## 9. Riesgos de implementación

- `Sanction.Evidence` puede no contener decisiones para sanciones manuales; esas entradas se omiten de la copia anonimizada.
- El hash del `userId` es seudónimo, no anonimato pleno; se documenta como dato Interno con retención de 90 días.
- Los apartados expirados siguen sujetos al TTL general de 30 días sobre `ExpiresAtUtc`.

## 10. Orden de implementación

1. Anonimización de `FeedPost` y `FeedComment` en Domain y repositorios Mongo.
2. Expiración de apartados activos por comercio.
3. Entidad, documento, repositorio y TTL de `moderation_audit`.
4. Orquestación en el handler de baja de cuenta.
5. Pruebas, gates y cierre documental.
