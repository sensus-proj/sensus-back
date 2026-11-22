# Checklist 050 — Red de amigos y solicitudes por QR o invitación

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

## A. Antes de empezar (Definition of Ready)

- [x] Spec 050 con requisitos medibles (`FR-050-01..06`, `NFR-050-01..03`) y criterios de aceptación
- [x] Contrato alineado con `FT-25-amigos-qr` en `sensus-app-mobile`

## B. Específicos de este spec

- [x] Generador determinístico `SNS-XXXXXX` alineado con `FriendInviteCodec` móvil
- [x] Validación de auto-invitación, duplicados y aceptación automática mutua
- [x] Solo el destinatario puede aceptar (`403 Forbidden` a terceros)
- [x] Despacho hacia `IPushDispatcher` usando dispositivos activos registrados

## C. Antes de entregar (Definition of Done)

- [x] `dotnet build sensus-back.sln --configuration Release` sin errores ni advertencias
- [x] `dotnet format sensus-back.sln --verify-no-changes` limpio
- [x] `Sensus.ArchitectureTests`, `Sensus.Application.UnitTests`, `FriendEndpointsIntegrationTests` y `MongoSocialRepositoriesTests` añadidas
- [x] `tasks.md` refleja el estado real (pendiente despliegue AWS)
- [ ] Ejecución en vivo contra clúster MongoDB real (`MONGO_CONNECTION_STRING`) y verificación tras despliegue en AWS

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | [`FriendHandlersTests.cs`](../../tests/Sensus.Application.UnitTests/Friends/FriendHandlersTests.cs) | ☑ |
| Pruebas HTTP / Mongo | [`FriendEndpointsIntegrationTests.cs`](../../tests/Sensus.IntegrationTests/Friends/FriendEndpointsIntegrationTests.cs), [`MongoSocialRepositoriesTests.cs`](../../tests/Sensus.IntegrationTests/Persistence/MongoSocialRepositoriesTests.cs) | ☑ |
| Arquitectura | `tests/Sensus.ArchitectureTests/` | ☑ |
| Fakes E2E | `tests/Sensus.TestKit/Fakes/InMemoryFriendRequestRepository.cs` | ☑ |
