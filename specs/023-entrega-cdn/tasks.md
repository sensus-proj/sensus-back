# Tareas 023 — Entrega de contenido por CDN

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Completada |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T023-01 | Implementar: Integración con Cloudinary CDN y configuración de entrega | Infrastructure | `FR-023-01` | — | Completada |
| T023-02 | Implementar: Generación de URLs firmadas | Application | `FR-023-01` | T023-01 | Completada |
| T023-03 | Configurar transformaciones y optimización en Cloudinary CDN | Infrastructure | `FR-023-01` | T023-02 | Completada |
| T023-04 | Generar URLs firmadas para contenido privado | Application | `FR-023-02` | T023-03 | Completada |
| T023-05 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-023-01` | T023-04 | Completada |
| T023-06 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-023-01` | T023-05 | Completada |
| T023-07 | Registrar evidencias del Constitution Check | Docs | `FR-023-01` | T023-06 | Completada |

## Notas de ejecución

- Todas las tareas fueron completadas con pruebas automatizadas verdes y cobertura > 90%.
- Generación de firmas HMAC-SHA256 con tiempo constante para prevención de timing attacks.
- Prevención estricta de SSRF en `CloudinaryMediaContentProxy`.
- OpenAPI v1.1.0 especifica `POST /api/v1/media/{id}/delivery-url` y `GET /api/v1/media/{id}/delivery`.
