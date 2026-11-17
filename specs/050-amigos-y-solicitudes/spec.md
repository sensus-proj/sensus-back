# Spec 050 — Red de amigos y solicitudes por QR o invitación

| Campo | Valor |
|---|---|
| ID | 050 |
| Slug | `amigos-y-solicitudes` |
| Estado | Parcial |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 001, 002, 027 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

---

## 1. Contexto y problema

La aplicación móvil (`FT-25-amigos-qr`) necesita conectar usuarios reales mediante códigos de invitación y QR escaneables sin usar listas simuladas. El almacenamiento únicamente local en el teléfono que escanea el QR impide que el destinatario reciba notificación y decida si acepta o rechaza la solicitud antes de quedar vinculados ambos perfiles.

## 2. Alcance

**Incluye**

- Generación determinística del código de invitación por usuario (`FriendInviteCodeGenerator`, formato `SNS-XXXXXX` derivado de SHA-256 sobre el correo normalizado).
- Entidad de dominio `FriendRequest` con estados `Pending`, `Accepted` y `Rejected`.
- Repositorio `IFriendRequestRepository` y su implementación en MongoDB Atlas (`friend_requests` en `MongoDbContext`), más consulta `GetByInviteCodeAsync` en `IUserRepository`.
- Consultas y comandos MediatR: `GetFriendsOverviewQuery`, `SendFriendRequestCommand`, `AcceptFriendRequestCommand`, `RejectFriendRequestCommand` y `RemoveFriendCommand`.
- Endpoints REST protegidos con `RegisteredUserPolicy` bajo `/api/v1/friends`:
  - `GET /api/v1/friends`
  - `POST /api/v1/friends/requests`
  - `POST /api/v1/friends/requests/{requestId:guid}/accept`
  - `POST /api/v1/friends/requests/{requestId:guid}/reject`
  - `DELETE /api/v1/friends/{friendUserId:guid}`
- Despacho de notificación push vía `IPushDispatcher` hacia los dispositivos activos del destinatario/remitente.

**No incluye**

- Búsqueda pública global por nombre o teléfono.
- Envío directo de ofertas entre amigos (`POST /api/v1/offers/{id}/share`), diferido para iteración posterior.

## 3. Actores

- Usuario autenticado solicitante (escanea QR o abre enlace de invitación)
- Usuario autenticado destinatario (recibe solicitud pendiente, acepta o cancela)
- Sistema (`IPushDispatcher`, `FriendInviteCodeGenerator`)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-050-01 | Generar el código determinístico `SNS-XXXXXX` del usuario autenticado con `FriendInviteCodeGenerator` y exponer la vista general en `GET /api/v1/friends` (`FriendsOverviewDto`: amigos conectados, pendientes recibidas y pendientes enviadas) | Must |
| FR-050-02 | Registrar una solicitud pendiente vía `POST /api/v1/friends/requests` resolviendo al usuario activo destino por su código de invitación | Must |
| FR-050-03 | Rechazar auto-invitaciones con error de validación (`400`) y manejar de forma idempotente vínculos existentes (`AlreadyConnected`, `AlreadyPending`) y solicitudes mutuas (`AcceptedMutual`) | Must |
| FR-050-04 | Notificar a los dispositivos activos del destinatario mediante `IDeviceRepository` e `IPushDispatcher` al crearse la solicitud pendiente y al ser aceptada | Must |
| FR-050-05 | Permitir únicamente al destinatario aceptar con `POST /api/v1/friends/requests/{requestId}/accept` (`403` a terceros) y permitir a los participantes cancelar/rechazar con `POST /api/v1/friends/requests/{requestId}/reject` | Must |
| FR-050-06 | Permitir eliminar un vínculo de amistad existente mediante `DELETE /api/v1/friends/{friendUserId}` | Should |

## 5. Requisitos no funcionales

| ID | Categoría | Métrica | Estado |
|---|---|---|---|
| NFR-050-01 | Seguridad | 100 % de los endpoints bajo `RegisteredUserPolicy` (excluye invitados y anónimos) | Implementado |
| NFR-050-02 | Privacidad | Los DTOs de amigos y solicitudes no exponen correo, hash ni coordenadas exactas | Implementado |
| NFR-050-03 | Arquitectura | Cumplimiento estricto de capas (`Sensus.ArchitectureTests` en verde) | Implementado |

## 6. Criterios de aceptación

1. Dado un código válido `SNS-XXXXXX` de otro usuario activo, `POST /api/v1/friends/requests` crea una solicitud `Pending`, notifica sus dispositivos registrados y devuelve `Outcome = "Sent"`.
2. Si el usuario intenta enviarse solicitud a su propio código, el sistema responde validación negativa (`Friend.SelfInvite`).
3. Cuando existe una solicitud pendiente en sentido inverso, `SendFriendRequestCommandHandler` la acepta automáticamente (`AcceptedMutual`).
4. Cuando el destinatario invoca `accept`, el estado pasa a `Accepted`, notifica al remitente y ambos usuarios aparecen conectados en `GET /api/v1/friends`.
5. Cuando el destinatario o remitente invoca `reject`, el estado pasa a `Rejected` y deja de listarse como pendiente.

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| `FcmPushDispatcher` sigue simulado en desarrollo hasta configurar credenciales FCM en AWS | La app cliente realiza sincronización periódica y muestra notificaciones locales al recibir pendientes vía `GET /api/v1/friends` |
| Búsqueda de usuario por código calculado sobre usuarios activos | Se prioriza consulta limpia en `MongoUserRepository.GetByInviteCodeAsync` sin requerir migración previa de documentos existentes |
