# Plan 044 — Ajustes de notificaciones

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## 1. Resumen técnico

Permitir que el usuario configure qué notificaciones recibe, con lectura y actualización de sus preferencias y respeto estricto en el despacho.

## 2. Cambios por capa

Para `FR-044-07` y `FR-044-08`, la ubicación de alertas se recibe por consentimiento explícito y se almacena por cuenta, sin historial. Infrastructure usa un índice `2dsphere` para seleccionar candidatos dentro del máximo permitido y un TTL para purgar ubicaciones vencidas; el filtro final usa el radio individual, vigencia y preferencias actuales. La baja de cuenta y el retiro deben eliminar la ubicación. El payload incluye el ID de la oferta y un tipo de evento, sin coordenadas.

**Brecha constitucional registrada.** El flujo existente de creación de la spec 034 persiste una oferta sin enlazar un veredicto de moderación para el contenido actual. Un estado `Published` o un URL de imagen no prueban aprobación. El disparador de alertas requiere vincular y verificar primero una decisión aprobada de la moderación conjunta de imagen y texto, conforme a §2.4, y volver a invalidarla si cambia el contenido. Diseñar y validar ese control en la spec 034 antes de activar el envío. Conservar las rutas existentes y documentar el contrato de ubicación antes de implementar.

| Capa | Cambio |
|---|---|
| Application | GetNotificationSettingsQuery y UpdateNotificationSettingsCommand |
| Infrastructure | Persistencia de preferencias y filtro en el despacho |
| Api | Endpoints GET y PUT /api/v1/users/settings |

## 3. Modelo de datos e índices

- notification_settings con índice único userId

## 4. Seguridad

- Preferencias del propio usuario
- Sin datos sensibles en la respuesta

## 5. Observabilidad y rendimiento

- Métricas de actualizaciones y de envíos filtrados

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de validación y valores por defecto
- Integración de filtrado en el despacho

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-044-01` y 5 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Ignorar las preferencias en el despacho genera reportes de abuso y desinstalaciones

## 10. Orden de implementación

1. `T044-01` Actualizar y validar el contrato OpenAPI del spec
2. `T044-02` Implementar: GetNotificationSettingsQuery y UpdateNotificationSettingsCommand
3. `T044-03` Implementar: Persistencia de preferencias y filtro en el despacho
4. `T044-04` Implementar: Endpoints GET y PUT /api/v1/users/settings
5. `T044-05` Implementar valores por defecto
6. `T044-06` Aplicar el filtro de preferencias en el despacho
7. `T044-07` Probar el límite de radio y la actualización
8. `T044-08` Implementar las pruebas de integración del flujo completo
9. `T044-09` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
10. `T044-10` Registrar evidencias del Constitution Check

### Diseño implementado para FR-044-08 (2026-11-19)

Por decisión del responsable del proyecto (ver CA-044-06), se implementa un disparador síncrono y acotado en lugar del trabajo persistente descrito más abajo:

- `CreateOfferCommandHandler` llama a `INearbyOfferNotifier` después de persistir la oferta. El notificador nunca lanza excepciones, así que la creación no se bloquea ni se revierte.
- `NearbyOfferNotifier` (Application) solo actúa si la oferta está disponible (`IsActive`). Modera juntos el título y la descripción con `ITextModerationService` y continúa solo con `Approved` (*fail-closed*). La decisión queda en el log con veredicto, categorías y acción, sin el texto.
- Candidatos: `INotificationLocationRepository.GetActiveNearAsync` con `$geoNear` sobre el índice `2dsphere` existente, filtrando la vigencia y limitando por `MaxRecipients`. Preferencias y dispositivos se leen por lote (`GetByUserIdsAsync` y `GetActiveByUsersAsync`) con los índices `userId` existentes.
- Filtros: `pushEnabled`, `offerAlerts`, `radiusKm` inclusivo (valores por defecto si no hay preferencias guardadas) y exclusión del titular del comercio.
- Aviso: título `Nuevo {título} cerca de ti`, cuerpo vacío y datos `type=nearby_offer` y `offerId`. Un token rechazado por FCM se desactiva.
- Configuración `NearbyOfferAlerts`: `Enabled`, `TimeoutSeconds` (3), `MaxRadiusKm` (50), `MaxRecipients` (500), `MaxParallelSends` (8) y `MaxTitleLength` (60), validados al arrancar.
- Costo: una llamada de moderación de texto por oferta creada (proveedor existente bajo EX-002) y envíos FCM sin costo. Sin recursos AWS nuevos.
- Limitaciones aceptadas: la latencia de `POST /offers` aumenta como máximo `TimeoutSeconds`. No se avisa de ofertas programadas, republicadas ni editadas, y no hay reintento si se agota el tiempo.

### Diseño original (no implementado) para FR-044-08

Usar un trabajo persistente por primera activación de oferta con evidencia conjunta, con vencimiento a la fecha de fin de la oferta y elegibilidad a partir de StartsAt. El trabajador debe volver a leer la oferta y la evidencia para descartar contenido retirado, vencido, agotado o modificado. Seleccionar ubicaciones vigentes mediante el índice 2dsphere y el máximo de radio permitido en ajustes. Aplicar el radio inclusivo individual y comprobar preferencias y dispositivo activo antes de reservar el intento en MongoDB por oferta y dispositivo. El evento para móvil deberá acordarse como parte del contrato del disparador y documentarse con su navegación al detalle de oferta. Revisar la ejecución recurrente en Lambda, su costo y la latencia antes de activar el despacho.
