# Checklist 033 — Apartado de oferta con código QR

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 033 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 007, 032, 037
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El stock nunca queda negativo
- [x] Un código solo se canjea una vez
- [x] El canje valida la propiedad del comercio
- [x] Los apartados vencidos liberan stock

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica (sin cambios de infraestructura: Lambda y API Gateway ya declarados)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/` (OfferClaimTests, ClaimOffer, RedeemClaim, ExpireClaims) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/OfferClaimEndpointsTests.cs`, `Security/HmacClaimCodeServiceTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

- [x] En pruebas locales, un apartado persistido genera avisos sin QR y respeta preferencias (`FR-033-09`, `FR-033-10`).
- [x] En pruebas locales, los reintentos no duplican el despacho y las fallas no revierten el apartado.

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-13 | Equipo Sensus | PASS | `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, 337 unitarias y 78 de integración en verde. Firma HMAC con `FixedTimeEquals`; secreto por configuración. |
| 2026-11-18 | Codex | Parcial | Contrato HTTP sin cambios. Unitarias cubren destinatarios y vencimiento. MongoDB real verifica unicidad concurrente del recibo y el índice TTL. Falta entrega FCM física y medición de latencia del apartado. |
| 2026-11-19 | Claude | Parcial | FR-033-11 cubierto en integración: firma reemplazada sin QR, apartado inexistente y comercio inactivo con 404, además de los casos previos. Unitaria del notificador: una excepción del adaptador no se propaga y un token rechazado se desactiva. 4 unitarias y 13 de integración en verde. Entrega FCM física y latencia siguen pendientes. |
| 2026-11-22 | Codex | Parcial | La suite completa en .NET 10.0.401 volvió a comprobar apartado, preferencias, deduplicación con MongoDB y lectura protegida del QR. Quedan pendientes la entrega real por FCM y la medición de latencia. |
| 2026-11-22 | Codex | T033-15 verificada | El POST devuelve 200, código y QR. La prueba ejercita el adaptador FCM HTTP v1 con doble HTTP y verifica el vencimiento del comprador, el aviso al comercio y la preferencia desactivada. |
| 2026-11-22 | Codex | T033-16 verificada | MongoDB real admite solo una reserva concurrente de la última unidad, deja stock cero y rechaza el segundo apartado activo del mismo usuario y oferta. |

### Validación de entrega al habilitar FCM real

El backend está completado. El registro y la apertura de los avisos en Android, la entrega real de FCM y la medición del p95 quedan como verificaciones pendientes de integración. Los escaneos de CI forman parte del cierre formal.

- [ ] En el entorno de pruebas con FCM habilitado, registrar un dispositivo de comprador y otro de comerciante. Con el JWT de comprador llamar a `POST /api/v1/offers/{id}/claim` sobre una oferta activa con stock. Comprobar HTTP 200 y la presencia del QR y su vencimiento en la respuesta.
- [ ] Comprobar en ambos extremos que el evento y los identificadores coinciden con el apartado. Abrir los avisos en primer plano, en segundo plano y con la app cerrada durante la integración Android.
- [ ] Repetir con push deshabilitado para un destinatario y comprobar cero avisos para esa cuenta.
- [x] Forzar indisponibilidad del adaptador en pruebas y comprobar que el apartado permanece persistido. Verificar que la reserva evita segundos intentos sobre el mismo dispositivo.
- [ ] Ejecutar una carga controlada con ofertas y compradores de prueba que produzcan apartados válidos. Registrar el tamaño de la muestra y el p95 de las respuestas HTTP 200, con objetivo < 700 ms (NFR-033-01). Registrar la entrega de FCM por separado. La evidencia omite tokens de dispositivos y códigos QR.

- [x] FR-033-11 — Comprador vigente recibe QR; comercio, comprador vencido o firma reemplazada reciben metadatos; tercero recibe 404, invitado 403 y anónimo 401. Verificar no-store.
