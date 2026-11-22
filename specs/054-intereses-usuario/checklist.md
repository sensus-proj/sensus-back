# Checklist 054 — Intereses del usuario

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Alcance confirmado por el responsable: selección opcional al registrarse y editable en Perfil
- [x] Requisitos y criterios de aceptación medibles
- [x] Modelo de amenazas STRIDE en el plan
- [x] Dependencias identificadas: 002, 003, 013, 030, 052

## B. Específicos de este spec

- [x] Invariante de categorías en Domain (T054-02)
- [x] Purga en eliminación de cuenta con prueba de regresión (T054-05)
- [x] Invitado recibe `403` (T054-06)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [x] Pruebas unitarias e integración en verde; cobertura ≥ 80 % en Domain y Application
- [x] Test de arquitectura en verde
- [x] Logs con `correlation-id` y sin PII
- [x] Constitution Check en PASS tras la implementación
- [x] `memory.md` actualizado

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Unitarias | `tests/Sensus.Application.UnitTests/Users/Interests/UserInterestsTests.cs` | ☑ |
| Integración HTTP/Repo | `tests/Sensus.IntegrationTests/Users/UserInterestsEndpointsTests.cs`, `DeleteAccountEndpointTests.cs`, `Persistence/MongoUserInterestsRepositoryTests.cs` | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-20 | Rodrigo Sánchez (con asistente) | Spec creada | Insumo de interés explícito para el feed de la spec 042 |
| 2026-11-20 | Ingeniería Sensus (con asistente) | Implementado (`T054-01`..`T054-07`) | `UserInterests` en Domain, `GetMyInterestsQuery` + `ReplaceMyInterestsCommand` (`ISensitiveRequest`) con invalidación `feed:affinity:{userId}`, `MongoUserInterestsRepository` (`ux_user_interests_user_id`), purga en `DeleteAccountCommandHandler` (+ `InMemoryUserInterestsRepository` en E2E), endpoints `GET`/`PUT /api/v1/users/me/interests` (`RegisteredUserPolicy`, `ReadHeavyPolicy`, `SensitiveWritePolicy`, tope 1 KB). Probado en local contra MongoDB 7.0 real (`MongoUserInterestsRepositoryTests`) y suite completa (`828` unitarias, `417` integración, `4` arquitectura, `3` E2E; cobertura `91.00 %` Domain / `96.46 %` Application). Pendiente en CI/entorno: pruebas de carga p95 (`G-13`). |
