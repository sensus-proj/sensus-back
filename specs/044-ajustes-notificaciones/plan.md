# Plan 044 — Ajustes de notificaciones

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (Cerrado) |
| Versión | 2.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Resumen técnico

Preferencias de notificación por usuario con lectura y actualización en `GET`/`PUT /api/v1/users/settings`, ubicación consentida en `PUT`/`DELETE /api/v1/users/notification-location` y respeto estricto de `pushEnabled`, `offerAlerts` y `socialAlerts` en cada despacho. El proveedor real es FCM HTTP v1 con cuenta de servicio en SSM SecureString; Development y Testing usan un despachador simulado. Las alertas de ofertas cercanas se activan solo cuando el texto moderado tiene veredicto `Approved` (*fail-closed*).

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `NotificationSettings` con valores por defecto y `Update`; invariantes de radio 1–50 y vigencia de ubicación |
| Application | `GetNotificationSettingsQuery`, `UpdateNotificationSettingsCommand`, `ShareNotificationLocationCommand`, `DeleteNotificationLocationCommand`, `NearbyOfferNotifier` y filtros de preferencias en todos los notificadores |
| Infrastructure | Persistencia Mongo con índice único `userId`, índice `2dsphere` y TTL para ubicación; `FcmPushDispatcher` y `FcmCredentials` con SSM; `SimulatedPushDispatcher` para Development/Testing |
| Api | Endpoints GET/PUT de ajustes y PUT/DELETE de ubicación con `RegisteredUserPolicy`; composición del proveedor en `Program.cs` |
| IaC | Sin recursos nuevos; el parámetro SSM `/sensus/<entorno>/push/fcm-service-account` se aprovisiona fuera del repositorio |

## 3. Modelo de datos e índices

- `notification_settings`: índice único por `userId`.
- `notification_locations`: índice `2dsphere` para `$geoNear` e índice TTL sobre la fecha de expiración; solo la última ubicación por cuenta, sin historial.
- La baja de cuenta y el retiro explícito eliminan la ubicación. El payload de push incluye `type` y `offerId`, nunca coordenadas.

## 4. Seguridad y modelo de amenazas (STRIDE)

| Amenaza | Control |
|---|---|
| Spoofing | `RegisteredUserPolicy` en los cuatro endpoints; el `userId` se toma del token, nunca del cuerpo |
| Tampering | Validación de borde con lista de permitidos para coordenadas y radio; update solo sobre la cuenta autenticada |
| Repudiation | Auditoría redactada con `correlation-id` en los despachos; sin PII en logs |
| Information disclosure | La respuesta expone solo preferencias propias; el payload push no incluye coordenadas ni correo; la credencial FCM se lee de SSM con decrypt |
| Denial of service | `SensitiveWritePolicy` en `PUT /notification-location`; `NearbyOfferAlerts` acota `MaxRecipients` (500), `MaxRadiusKm` (50) y `TimeoutSeconds` (3) |
| Elevation of privilege | Los dispositivos y preferencias se consultan por lote con `userId` del servidor; FCM con ámbito `firebase.messaging` |

> **Brecha dependiente**: la spec 034 mantiene abierta la moderación conjunta de imagen y texto. Para el aviso de esta spec se moderan título y descripción (*fail-closed*) por decisión del responsable del 2026-11-19; sin veredicto `Approved` no se envía nada.

## 5. Observabilidad y rendimiento

- NFR-044-01 medido en `NotificationSettingsEndpointTests`: 20 solicitudes HTTP con p95 < 250 ms.
- NFR-044-02 cubierto por pruebas de `NearbyOfferNotifier`, `ClaimPushNotifier` y notificadores de amistad: 100 % de los envíos filtrados por preferencias.
- `FcmPushDispatcher` registra solo el tipo de error y el código de estado, nunca tokens ni cuerpo del mensaje.

## 6. Costo AWS

Sin costo incremental: Lambda, API Gateway y SSM Parameter Store dentro del Free Tier. FCM no cobra por entrega. EX-002 cubre el proveedor de moderación de texto ya existente. No se crearon recursos AWS nuevos ni se ejecutó `cdk deploy`.

## 7. Estrategia de pruebas

- Unitarias: dominio de `NotificationSettings`, validadores y handlers de consulta/actualización, `NearbyOfferNotifier` y `FcmPushDispatcher` con `HttpMessageHandler` falso.
- Integración HTTP: `NotificationSettingsEndpointTests` (401, 403, valores por defecto, 400 por radio y p95) y `NotificationLocationEndpointTests` (consentimiento, idempotencia y coordenadas inválidas).
- Persistencia: `NotificationMongoRepositoryTests` contra MongoDB real con Testcontainers (índices 2dsphere y TTL, reemplazo y purga).
- Externa: entrega física FCM pendiente de la credencial en SSM; documentada como brecha operativa, no como prueba omitida.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Cuatro artefactos v2.0.0 con `FR-044-01` a `FR-044-08` y `NFR-044-01/02` trazados en `requirements.md` |
| II. Clean Architecture | PASS | Dominio, Application, Infrastructure y Api separados; test de arquitectura 4/4 |
| III. CQRS y contratos | PASS | Consultas y comandos distintos, DTOs explícitos, OpenAPI actualizado |
| IV. Seguridad y privacidad | PASS | STRIDE en §4, sin PII en respuestas o logs, SSM SecureString, autorización en servidor |
| V. Verificación automatizada | PASS | 920 unitarias + 4 arquitectura + 3 E2E + 503/514 integración; cobertura Domain 89,35 % y Application 96,18 %; 11 pruebas bloqueadas por Docker ajeno al código |
| VI. Costo cero e IaC | PASS | Sin recursos nuevos; EX-002 para moderación; sin `cdk deploy` |
| VII. Rendimiento y observabilidad | PASS | p95 < 250 ms medido; filtrado 100 %; latencia y errores registrados sin PII |
| VIII. DevSecOps de nivel fintech | PASS | Sin secretos ni PII, lista de permitidos, auditoría con `correlation-id`, escaneos bloqueantes del pipeline; credencial FCM rotada en SSM según política |

## 9. Riesgos de implementación

- La entrega real depende de la credencial FCM en SSM; hasta entonces solo se prueba con dobles HTTP.
- La app móvil debe completar el registro de token y el consentimiento de ubicación; fuera de este repositorio.
- La brecha IV de la spec 034 (moderación de imagen) permanece abierta; el aviso usa únicamente texto aprobado.

## 10. Orden de implementación

1. Contrato OpenAPI y preferencias (T044-01 a T044-10) — completado.
2. Ubicación consentida y purga (T044-11 a T044-12) — completado en backend.
3. Despacho de ofertas cercanas con texto moderado (T044-13) — completado en backend.
4. Pruebas HTTP de ajustes y latencia (T044-14) — completado.
5. Cierre documental y evidencia (T044-15) — completado.

---