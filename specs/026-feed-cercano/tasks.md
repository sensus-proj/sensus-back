# Tareas 026 — Feed geolocalizado

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completado |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T026-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-026-01` | Contrato de lectura de la Spec 024 | Completada. `contracts/openapi.yaml` v1.1.0 con la proyección pública de la respuesta 200 y la ubicación opcional en `POST /feed/posts`. |
| T026-02 | Implementar: GetNearbyFeedQuery con proyección | Application | `FR-026-01` | T026-01 | Completada. `GetNearbyFeedQuery` y `NearbyFeedPostDto` sin coordenadas ni estado. |
| T026-03 | Implementar: Índice geoespacial y caché de corta duración | Infrastructure | `FR-026-01` | T026-02 | Completada. `GetPublishedNearbyPagedAsync` con `$centerSphere` y `hint`; ubicación GeoJSON en `FeedPostDocument`. |
| T026-04 | Implementar: Endpoint GET /api/v1/feed/nearby | Api | `FR-026-01` | T026-03 | Completada |
| T026-05 | Crear índice geoespacial y de estado | Infrastructure | `FR-026-01` | T026-04 | Completada. `ix_feed_posts_status_location_created_at`. |
| T026-06 | Implementar caché con TTL e invalidación | Application | `FR-026-06` | T026-05 | Completada. TTL `Redis:TtlByEntityMinutes:Feed`; invalidación por prefijo al crear y eliminar. |
| T026-07 | Validar radio y paginación | Api | `FR-026-04` | T026-06 | Completada. Radio 0 < r ≤ 5 km, coordenadas finitas y en rango, página ≥ 1, tamaño 1–100. |
| T026-08 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-026-01` | T026-07 | Completada. `NearbyFeedEndpointsTests` y `MongoFeedSocialIntegrationTests` (filtro, radio, desempate, plan `2dsphere`) en verde contra MongoDB 7.0 real. |
| T026-09 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-026-01` | T026-08 | Completada |
| T026-10 | Registrar evidencias del Constitution Check | Docs | `FR-026-01` | T026-09 | Completada (`plan.md` §4 STRIDE; `ci.yml` gitleaks/Snyk/SonarCloud/cdk-nag/SBOM CycloneDX y `dast-zap.yml`, corrida verde `4a00d24`) |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
