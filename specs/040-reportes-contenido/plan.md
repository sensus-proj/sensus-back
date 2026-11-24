# Plan 040 — Reportes de contenido

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Implementar el flujo seguro de reportes (`POST /api/v1/reports`) sobre ofertas, publicaciones y comentarios mediante la entidad `ContentReport`, validando motivos tipificados con lista de permitidos, límite de 500 caracteres en detalles, bloqueo inmediato **sin persistencia** de material de explotación infantil (`FR-040-06`), deduplicación de 24 horas por usuario/recurso (`409 Conflict`) y marcado automático para revisión al alcanzar el umbral configurable (`FlagThresholdForReview = 3` en `ContentReportSettings`).

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `ContentReport`, `ReportTargetType`, `ReportReason`, `LegalExclusionPolicy`, `IContentReportRepository` |
| Application | `CreateContentReportCommand` (+ validador), `IReportTargetService`, `ContentReportSettings`, `ContentReportDto` |
| Infrastructure | `ContentReportDocument`, `MongoContentReportRepository` (índice único `(ReporterUserId, TargetType, TargetId, DayKey)` e índice `(TargetType, TargetId, CreatedAtUtc desc)`), `MongoReportTargetService` |
| Api | `ReportEndpoints.cs` mapeado en `Program.cs` |
| Tests | `ContentReportHandlersTests`, `ReportsEndpointsTests`, `MongoFeedAndReportRepositoriesTests` |

## 3. Modelo de datos e índices

- Colección `content_reports` (`ContentReportDocument`): `Id`, `TargetType`, `TargetId`, `ReporterUserId`, `TargetAuthorUserId`, `Reason`, `Details`, `DayKey`, `Status`, `CreatedAtUtc`.
- Índices en `MongoDbContext`:
  1. Único compuesto `(ReporterUserId, TargetType, TargetId, DayKey)` para deduplicación atómica.
  2. Compuesto `(TargetType, TargetId, CreatedAtUtc desc)` para conteo en ventana de 24 h.

## 4. Seguridad

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Persistencia accidental de material de explotación infantil (CSAM) | Cumplimiento legal / Seguridad | `LegalExclusionPolicy` rechaza antes de crear/guardar (`400 Bad Request`) con log legal mínimo sin detalle ni PII (`FR-040-06`) | Bajo |
| Spam de múltiples reportes del mismo usuario para ocultar contenido legítimo | Manipulación / DoS | Límite 1 reporte / 24 h (`409 Conflict`, índice único) + `SensitiveWritePolicy` | Bajo |
| Exposición de identidad del reportante al usuario reportado | Divulgación de información | `ContentReportDto` omite `ReporterUserId` (`FR-040-08`); logs sanitizados | Bajo |
| Auto-reporte para manipular estado del recurso | Manipulación | Verificación `targetAuthorUserId == reporterUserId` -> `400` (`FR-040-07`) | Bajo |
| Acceso de invitado o enumeración de contenido | Elevación de privilegios | `RegisteredUserPolicy` (`403` para invitados) y validación estricta de `targetType`/`reason` por whitelist | Bajo |

## 5. Observabilidad y rendimiento

- Conteo reciente en ventana de 24 h respaldado por índice `(TargetType, TargetId, CreatedAtUtc desc)`.
- Umbrales (`FlagThresholdForReview = 3`, `DeduplicationWindowHours = 24`) externalizados en `ContentReportSettings` (`appsettings.json`).

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias (`Sensus.Application.UnitTests`): motivos permitidos, rechazo CSAM sin llamar al repositorio, auto-reporte `400`, duplicado `409`, marcado para revisión al 3.º reporte y cero PII en respuesta.
- Integración (`Sensus.IntegrationTests`): `POST /api/v1/reports` (201, 400 CSAM/inválido/auto-reporte, 401, 403 invitado, 404, 409) y persistencia/índices en MongoDB.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | `FR-040-01`..`08` y `NFR-040-01`..`02` |
| II. Clean Architecture | PASS | Capas Domain, Application, Infrastructure y Api desacopladas |
| III. CQRS y contratos | PASS | `CreateContentReportCommand` + `ContentReportDto` |
| IV. Seguridad y moderación | PASS | Exclusión legal CSAM, whitelist de motivos, `SensitiveWritePolicy`, cero PII |
| V. Verificación automatizada | PASS | Cobertura ≥ 80 % + integración HTTP y MongoDB |
| VI. Costo AWS | PASS | 0.00 USD incrementales |
| VII. Paginación e índices | PASS | Índice único de deduplicación + índice de ventana 24 h |
| VIII. DevSecOps / STRIDE | PASS | STRIDE completo y parámetros en configuración |
