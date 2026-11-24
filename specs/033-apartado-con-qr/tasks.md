# Tareas 033 — Apartado de oferta con código QR

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Backend completado |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T033-01 | Actualizar y validar el contrato OpenAPI del spec | Api | `FR-033-01` | — | Completada |
| T033-02 | Implementar: Entidad OfferClaim con estados Active, Redeemed, Expired y reglas de canje | Domain | `FR-033-01` | T033-01 | Completada |
| T033-03 | Implementar: ClaimOfferCommand, RedeemClaimCommand y ExpireClaimsJob | Application | `FR-033-01` | T033-02 | Completada |
| T033-04 | Implementar: Actualización atómica con findOneAndUpdate, firma HMAC y generación de QR | Infrastructure | `FR-033-01` | T033-03 | Completada |
| T033-05 | Implementar: Endpoints de apartado y canje | Api | `FR-033-01` | T033-04 | Completada |
| T033-06 | Modelar OfferClaim y sus transiciones | Domain | `FR-033-04` | T033-05 | Completada |
| T033-07 | Garantizar la reserva atómica de stock | Infrastructure | `FR-033-01` | T033-06 | Completada |
| T033-08 | Implementar el job de expiración y liberación | Application | `FR-033-05` | T033-07 | Completada |
| T033-09 | Firmar y verificar el código del QR con HMAC | Seguridad | `FR-033-06` | T033-08 | Completada |
| T033-10 | Implementar las pruebas de integración del flujo completo | Pruebas | `FR-033-01` | T033-09 | Completada |
| T033-11 | Actualizar `specs/README.md`, `requirements.md` y el checklist del spec | Docs | `FR-033-01` | T033-10 | Completada |
| T033-12 | Registrar evidencias del Constitution Check | Docs | `FR-033-01` | T033-11 | Completada |

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T033-13 | Enviar avisos tras persistir el apartado y reservar cada envío de forma atómica | Application / Infrastructure | `FR-033-09` | — | Completada en backend (entrega FCM y latencia real pendientes) |
| T033-14 | Incluir el vencimiento UTC en el aviso del comprador y probar el contrato push | Application / Pruebas | `FR-033-10` | — | Completada en backend (entrega física pendiente) |
| T033-15 | Proteger `claims/expire-check` con `AdminOnly` y límite de tasa; regresión en `ExpireCheckAuthorizationTests` | Api | `FR-033-12` | — | Hecho |
| T033-16 | Comprobar en MongoDB la reserva concurrente de la última unidad y el índice de apartado activo | Pruebas | `NFR-033-02`, `FR-033-03` | — | Verificada en MongoDB Local |
| T033-17 | Verificar el flujo del POST hasta el mensaje HTTP v1 de FCM, incluyendo preferencias | Pruebas | `FR-033-09`, `FR-033-10` | — | Verificada localmente con doble HTTP |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral (P-V).
- Los identificadores de requisito remiten a `spec.md` §4 y §5.

- [x] T033-14 — Implementar y verificar lectura autorizada desde el aviso (FR-033-11).
