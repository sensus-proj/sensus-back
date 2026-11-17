# Tareas 050 — Red de amigos y solicitudes por QR o invitación

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Parcial (código y pruebas unitarias listos; despliegue AWS pendiente) |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T050-01 | Crear `FriendRequest`, `FriendRequestStatus`, `FriendInviteCodeGenerator` y contratos de repositorio | Domain | FR-050-01, FR-050-02, FR-050-03 | — | Hecha |
| T050-02 | Implementar queries y commands CQRS de amigos con despacho vía `IPushDispatcher` | Application | FR-050-01..FR-050-06 | T050-01 | Hecha |
| T050-03 | Implementar `MongoFriendRequestRepository`, `FriendRequestDocument`, índices y búsqueda por código | Infrastructure | FR-050-01, FR-050-02 | T050-01 | Hecha |
| T050-04 | Exponer endpoints `/api/v1/friends` y `/api/v1/offers/{offerId}/share` en `FriendEndpoints.cs` y registrar en `Program.cs` | Api | NFR-050-01, NFR-050-02 | T050-02, T050-03 | Hecha |
| T050-05 | Añadir `FriendHandlersTests`, `SharedOffer` tests y fakes en `Sensus.TestKit` | Tests | NFR-050-03 | T050-02 | Hecha |
| T050-06 | Agregar entidad `SharedOffer`, `ISharedOfferRepository`, enriquecimiento de `FotoPerfilUrl` y compartición 1..10 amigos | Domain / App / Infra / Api | FR-050-07 | T050-04 | Hecha |
| T050-07 | Desplegar cambios en AWS API Gateway / Lambda y validar con MongoDB real | Infra / Ops | NFR-050-01 | T050-04 | Pendiente |

## Notas de ejecución

La compilación en configuración `Release`, `dotnet format --verify-no-changes`, `Sensus.ArchitectureTests` (4/4), `Sensus.Application.UnitTests` (629/629) y `Sensus.E2ETests` (3/3) pasaron en local. Queda pendiente únicamente el despliegue autorizado a AWS y las pruebas de integración con contenedor MongoDB.
