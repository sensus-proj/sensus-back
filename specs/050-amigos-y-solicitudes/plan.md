# Plan 050 — Red de amigos y solicitudes por QR o invitación

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado (retro-alineación con implementación) |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## 1. Resumen técnico

Se implementa el agregado `FriendRequest` y el generador `FriendInviteCodeGenerator` en `Sensus.Domain`, los casos de uso CQRS en `Sensus.Application.Friends`, la persistencia en MongoDB (`friend_requests`) en `Sensus.Infrastructure` y los endpoints REST bajo `/api/v1/friends` en `Sensus.Api`, junto con dobles en `Sensus.TestKit` para pruebas E2E.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | `FriendRequest`, `FriendRequestStatus`, `FriendInviteCodeGenerator`, `IFriendRequestRepository`, `IUserRepository.GetByInviteCodeAsync` |
| Application | `GetFriendsOverviewQuery`, `SendFriendRequestCommand`, `AcceptFriendRequestCommand`, `RejectFriendRequestCommand`, `RemoveFriendCommand`, `FriendDtos`, `FriendRequestMappingExtensions` |
| Infrastructure | `FriendRequestDocument`, `MongoFriendRequestRepository`, índices de `FriendRequests` en `MongoDbContext`, `FriendRequestsCollectionName` en `MongoDbSettings`, `MongoUserRepository.GetByInviteCodeAsync` |
| Api | `FriendEndpoints.cs` mapeado en `Program.cs` |
| Tests | `FriendHandlersTests`, `InMemoryFriendRequestRepository`, actualización de `InMemoryUserRepository` y `SensusE2EWebApplicationFactory` |

## 3. Modelo de datos e índices

Colección `friend_requests` en MongoDB Atlas: documento `FriendRequestDocument` con `SenderUserId`, `TargetUserId`, códigos de invitación, nombres, medio (`QR`, `Enlace`, `Código`), `Status` y fechas UTC. Índices compuestos por `(SenderUserId, TargetUserId)` y `(TargetUserId, Status)`.

## 4. Seguridad

- Todos los endpoints exigen política `RegisteredUserPolicy`.
- Solo el `TargetUserId` puede aceptar solicitudes pendientes; terceros reciben `403 Forbidden`.
- Sin exposición de correo electrónico ni credenciales en las respuestas de amigos o solicitudes.

## 5. Estrategia de pruebas

- Pruebas unitarias en `Sensus.Application.UnitTests/Friends/FriendHandlersTests.cs` (5 pruebas cubriendo código determinístico, envío + notificación push, aceptación mutua, aceptación/rechazo con control de permisos y resumen/eliminación).
- Reglas de capas y sufijo `*Handler` validadas con `Sensus.ArchitectureTests` (4/4).
- Compatibilidad de arranque E2E verificada con `Sensus.E2ETests` (3/3).

## 6. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | Spec 050 alineada al código y `requirements.md` |
| II. Clean Architecture | PASS | `Sensus.ArchitectureTests` (4/4 en verde) |
| III. Calidad y pruebas | PASS | `dotnet format --verify-no-changes` y `Sensus.Application.UnitTests` (629/629 en verde) |
