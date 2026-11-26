# Plan 005 — Activación del rol comercial

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (Completado) |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Permitir que un usuario con cuenta unificada active el rol comercial creando el perfil de su local con categoría, dirección y coordenadas validadas. Este plan documenta la implementación existente en la rama actual y las brechas detectadas frente a la constitución.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad Merchant con invariantes de categoría, ubicación y estado |
| Application | ActivateMerchantCommand, validador y handler |
| Infrastructure | Repositorio Mongo, índices único y 2dsphere |
| Api | Endpoint POST /api/v1/merchants/activate |

## 3. Modelo de datos e índices

- merchants con índice único en userId e índice 2dsphere en location

## 4. Seguridad

- Autorización por token
- El userId proviene del token
- Sin datos de contacto internos en la respuesta

### Modelo de amenazas (STRIDE — Principio VIII)

| Amenaza | Mitigación implementada |
|---|---|
| *Spoofing* | `UsuarioId` tomado exclusivamente del claim JWT validado en el servidor (`ICurrentUserService`) |
| *Tampering* | Validación de catálogo, CP y coordenadas (`RegisterComercioCommandValidator`) más invariantes de `Comercio` |
| *Repudiation* | Auditoría append-only de activación/moderación en `IModerationAuditRecorder` + logs estructurados con `correlation-id` |
| *Information Disclosure* | DTOs `RegisterComercioResponseDto`/`ComercioDto` sin campos internos y respuestas `ProblemDetails` uniformes |
| *Denial of Service* | Límite de tasa por usuario/ruta (`SensitiveWritePolicy`) y restricción única por `usuarioId` (`Conflict 409`) |
| *Elevation of Privilege* | Exclusivo para usuarios registrados válidos; transición de rol controlada por entidad de dominio y caso de uso único |

## 5. Observabilidad y rendimiento

- Métricas de activaciones y conflictos
- Log sin PII del local

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de handler y validador
- Integración de alta, duplicado y coordenadas inválidas

## 8. Constitution Check

Evaluación del estado actual. Las brechas marcadas como FAIL se convierten en tareas de remediación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-005-01` y 6 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Unitarias (`RegisterComercioCommandHandlerTests`, `RegisterComercioCommandValidatorTests`), integración (`MerchantProfileEndpointsTests` 15/15) y Testcontainers `2dsphere` + `explain` (`MongoUserAndComercioRepositoryTests` 2/2) |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Índice único en `usuarioId`, índice `2dsphere` en `ubicacion` y correlación HTTP |
| VIII | PASS | Matriz STRIDE en §4, mínimo privilegio y escaneos CI bloqueantes verdes (`ci.yml`, `dast-zap.yml`, corrida `4a00d24`) |

> Todas las brechas previas de `005` quedaron cerradas con código, pruebas contra MongoDB real y evidencia trazable.

## 9. Riesgos de implementación

- Coordenadas enviadas por el cliente pueden ser imprecisas
- El índice geoespacial no está probado contra MongoDB real

## 10. Orden de implementación

1. `T005-01` Actualizar y validar el contrato OpenAPI del spec
2. `T005-02` Documentar invariantes de Merchant y ubicación
3. `T005-03` Retirar el endpoint legacy /api/v1/comercio/registro
4. `T005-04` Probar el conflicto de comercio duplicado
5. `T005-05` Verificar el índice 2dsphere con explain
6. `T005-06` Implementar las pruebas de integración del flujo completo
7. `T005-07` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
8. `T005-08` Registrar evidencias del Constitution Check
