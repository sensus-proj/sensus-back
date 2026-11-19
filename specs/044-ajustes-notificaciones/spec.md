# Spec 044 — Ajustes de notificaciones

| Campo | Valor |
|---|---|
| ID | 044 |
| Slug | `ajustes-notificaciones` |
| Estado | Parcial |
| Ola | 4 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 027, 003 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

---

## 1. Contexto y problema

Permitir que el usuario configure qué notificaciones recibe, con lectura y actualización de sus preferencias y respeto estricto en el despacho.

## 2. Alcance

**Incluye**

- Lectura de preferencias
- Actualización de push activo, avisos de ofertas y avisos sociales
- Radio de interés
- Aplicación de las preferencias en el despacho

**No incluye**

- Programación de horarios silenciosos
- Plantillas de notificación
- Campañas

## 3. Actores

- Usuario autenticado
- Servicio de push

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-044-01 | Devolver las preferencias de notificación del usuario | Must |
| FR-044-02 | Actualizar pushEnabled, offerAlerts, socialAlerts y radiusKm | Must |
| FR-044-03 | Validar radiusKm entre 1 y 50 | Must |
| FR-044-04 | Respetar pushEnabled y las categorías al despachar | Must |
| FR-044-05 | Aplicar valores por defecto cuando el usuario no tiene preferencias | Must |
| FR-044-06 | Devolver 200 con el estado resultante tras la actualización | Must |
| FR-044-07 | Guardar la última ubicación compartida con consentimiento para alertas y permitir que el usuario la retire | Must |
| FR-044-08 | Al crear una oferta disponible, avisar "Nuevo {título} cerca de ti" únicamente a usuarios con ubicación vigente dentro de su radio y alertas activadas, después de aprobar por moderación su título y descripción | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-044-01 | Latencia de lectura | p95 < 250 ms |
| NFR-044-02 | Respeto de preferencias | 100 % de envíos filtrados según configuración |

## 6. Criterios de aceptación

### CA-044-01

- **Dado** usuario sin preferencias
- **Cuando** GET /api/v1/users/settings
- **Entonces** 200 con valores por defecto

### CA-044-02

- **Dado** pushEnabled=false
- **Cuando** se actualiza y se despacha una oferta
- **Entonces** no se envía notificación

### CA-044-03

- **Dado** radiusKm=0
- **Cuando** PUT /api/v1/users/settings
- **Entonces** 400

### CA-044-04

- **Dado** offerAlerts=false
- **Cuando** se despacha un aviso de oferta
- **Entonces** no se envía

### CA-044-05

- Solo la cuenta autenticada puede guardar o retirar su ubicación de alertas. Se guarda únicamente la última ubicación y su hora de recepción en UTC. La vigencia predeterminada es de 24 horas y puede reducirse con la configuración `NotificationLocation:LifetimeHours`. La ubicación retirada, vencida o eliminada con la cuenta queda excluida del despacho.

### CA-044-06

- Al crear una oferta con `POST /api/v1/offers` que queda disponible en ese momento (publicada, vigente y con stock), el backend modera juntos su título y descripción. Solo el veredicto `Approved` produce avisos. `Rejected`, `Quarantine`, error o tiempo agotado producen cero avisos (*fail-closed*). La oferta se crea igual: el aviso nunca bloquea ni revierte la creación.
- Un destinatario con `pushEnabled` y `offerAlerts` activos, dentro de su `radiusKm` (límite inclusivo) y con ubicación vigente, recibe un aviso por dispositivo activo. Sin preferencias guardadas se usan los valores por defecto. El titular del comercio no recibe aviso de su propia oferta.
- El aviso contiene solo el título `Nuevo {título de la oferta} cerca de ti`, sin cuerpo, y los datos `type=nearby_offer` y `offerId`. Al tocarlo, la app abre `GET /api/v1/offers/{offerId}`.
- Sin difusión retroactiva ni avisos por ofertas programadas a futuro, republicadas o editadas. El máximo de destinatarios por oferta, el radio máximo de búsqueda y el tiempo total del envío se fijan en la configuración `NearbyOfferAlerts`.

> **Decisión del responsable del proyecto (2026-11-19).** El aviso se habilita moderando el texto que muestra la notificación (título y descripción). La moderación de la imagen de la oferta sigue pendiente en la spec 034, donde la brecha IV queda registrada. Esta decisión sustituye, solo para el aviso, la condición previa de aprobación conjunta con la imagen.

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| GET | `/api/v1/users/settings` | JWT |
| PUT | `/api/v1/users/settings` | JWT |

La ubicación de alertas se gestionará mediante `PUT` y `DELETE /api/v1/users/notification-location`. Ambas operaciones requieren JWT de usuario registrado. `PUT` recibe `latitude` y `longitude`; implica la acción de compartir ubicación para alertas desde la app. Ambas devuelven 204. `DELETE` es idempotente. Las coordenadas fuera de rango se rechazan con 400.

## 8. Dependencias y supuestos

- Depende de: 027, 003
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Ignorar las preferencias en el despacho genera reportes de abuso y desinstalaciones

## 10. Preguntas abiertas

- ¿El catálogo necesita también un GET? Se incluye por obligación de lectura.

## 11. Trazabilidad

- Requisitos de este spec: `FR-044-01`, `FR-044-02`, `FR-044-03`, `FR-044-04`, `FR-044-05`, `FR-044-06`, `FR-044-07`, `FR-044-08`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
