# Plan 021 — Auditoría de moderación

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Implementado |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Conservar evidencia inmutable de cada decisión de moderación (tanto automática como manual) para revisión, trazabilidad y defensa legal.
Se implementa como un almacén estrictamente append-only (solo inserción y consulta paginada, sin operaciones `Update` ni `Delete`), fail-closed y protegido mediante RBAC exclusivo para el rol `Admin`.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `ModerationAuditRecord`, enums `ModerationVerdict`, `ModerationTargetType`, `ModerationActorType`, contrato `IModerationAuditRepository`. |
| Application | Consultas CQRS `GetModerationAuditQuery`, handler `GetModerationAuditQueryHandler`, DTO inmutable `ModerationAuditRecordDto`, mapper `ModerationAuditMapping`, servicio `IModerationAuditRecorder`/`ModerationAuditRecorder`, integración fail-closed en creación/edición de ofertas, comentarios, imágenes y liberación de cuarentena. |
| Infrastructure | Documento `ModerationAuditDocument`, repositorio `MongoModerationAuditRepository`, índices de MongoDB en `MongoDbContext` (`ix_moderation_audit_created_at`, `ix_moderation_audit_target`, `ttl_moderation_audit_retention`). |
| Api | Endpoint administrativo `GET /api/v1/admin/moderation/audit` con autorización de rol `Admin`, rate limiting `ReadHeavy` y respuestas estándar `ProblemDetails`. |

## 3. Modelo de datos e índices

- Colección MongoDB `moderation_audit` strictly append-only.
- Retención explícita legal/reglamentaria: **2555 días** (~7 años, `retainUntilUtc = createdAtUtc.AddDays(2555)`).
- Índices configurados en `MongoDbContext`:
  - `ix_moderation_audit_created_at`: `{ createdAtUtc: -1 }` (orden cronológico inverso eficiente).
  - `ix_moderation_audit_target`: `{ targetType: 1, targetId: 1, createdAtUtc: -1 }` (búsqueda por recurso moderado).
  - `ttl_moderation_audit_retention`: `{ retainUntilUtc: 1 }` con `expireAfterSeconds: 0` (purga automática tras retención legal/reglamentaria).

## 4. Seguridad y Modelo de Amenazas (Fintech / OWASP ASVS)

- **Spoofing**: Actor validado mediante JWT del servidor; actor tipo `System` para decisiones automatizadas de OpenAI.
- **Tampering / Inmutabilidad (NFR-021-01)**: Almacén inmutable append-only.
  - *Control compensatorio en BD*: La identidad de MongoDB de la aplicación (`api_app_role` en Atlas / usuario de la API) tiene permisos exclusivos de `insert` y `find` sobre `moderation_audit`, con revocación explícita de privilegios `update` y `delete` a nivel motor.
  - *Control en arquitectura*: La entidad de dominio `ModerationAuditRecord` y la interfaz `IModerationAuditRepository` no exponen ningún método de actualización o eliminación física/lógica.
- **Repudiation**: Toda decisión de publicación o rechazo queda registrada con veredicto, puntajes normalizados, regla aplicada, `correlation-id` y actor.
- **Information Disclosure**: Endpoint restringido a rol `Admin`. La auditoría trunca detalles a 2,000 caracteres y no almacena credenciales ni tokens.
- **Denial of Service**: Paginación obligatoria (20 por defecto, 100 máximo), índices cubrientes y rate limiting en el endpoint.
- **Elevation of Privilege**: Verificación estricta en servidor de roles (`RequireRole("Admin")`), no delegada al cliente.

## 5. Observabilidad y rendimiento

- Métricas operacionales y registros de logs estructurados con `correlation-id` y sin PII.
- Consultas paginadas asíncronas con `CancellationToken` propagado a MongoDB.
- Latencia p95 esperada inferior a 800 ms (`NFR-021-02`).

## 6. Costo AWS

- Almacenamiento en MongoDB Atlas / Local dentro de los límites del Free Tier (512 MB).
- Purga automática mediante índice TTL sin requerir trabajos cron costosos.

## 7. Estrategia de pruebas

- Unitarias para invariantes de dominio de `ModerationAuditRecord`.
- Unitarias para validación y manejo de consulta `GetModerationAuditQuery`.
- Unitarias para el grabador `ModerationAuditRecorder` y su comportamiento ante fallos.
- Integración en `ModerationAuditEndpointsTests` verificando acceso exitoso, rechazo por falta de token (401), rol no autorizado (403), validación de parámetros (400) y paginación.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | `FR-021-01` a `FR-021-04`, `NFR-021-01`, `NFR-021-02` implementados y probados. |
| II. Clean Architecture | PASS | Flujo unidireccional Domain <- Application <- Infrastructure / Api; dependencias externas aisladas tras interfaces. |
| III. CQRS y contratos | PASS | Consulta aislada `GetModerationAuditQuery`, DTO record inmutable, OpenAPI v1.1.0 actualizado. |
| IV. Seguridad y privacidad | PASS | RBAC Admin en servidor, sin PII ni tokens en logs ni registros, fail-closed en moderación. |
| V. Verificación automatizada | PASS | Pruebas unitarias e integración en verde con cobertura > 90% en Domain y Application. |
| VI. Costo cero e IaC | PASS | Colección en MongoDB con TTL automático sin costos adicionales en AWS. |
| VII. Rendimiento y observabilidad | PASS | Índices compuestos, paginación estricta y logs estructurados con correlation-id. |
| VIII. Nivel Fintech | PASS | Append-only, inmutabilidad, fail-closed y modelo de amenazas STRIDE documentado. |

## 9. Riesgos de implementación

- Mitigado: Crecimiento de colección gestionado mediante índice TTL en `retainUntilUtc`.

## 10. Orden de implementación

1. `T021-01` Actualizar y validar el contrato OpenAPI del spec (Completada)
2. `T021-02` Implementar: Modelo de registro de auditoría (Completada)
3. `T021-03` Implementar: Consulta paginada (Completada)
4. `T021-04` Implementar: Colección append-only (Completada)
5. `T021-05` Implementar: Endpoint administrativo (Completada)
6. `T021-06` Crear colección append-only con índices (Completada)
7. `T021-07` Restringir el endpoint al rol Admin (Completada)
8. `T021-08` Implementar las pruebas de integración del flujo completo (Completada)
9. `T021-09` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec (Completada)
10. `T021-10` Registrar evidencias del Constitution Check (Completada)
