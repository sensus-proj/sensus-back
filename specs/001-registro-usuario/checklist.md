# Checklist 001 — Registro de cuenta unificada

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 001 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: Ninguna
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El contrato de registro está en OpenAPI (`specs/001-registro-usuario/contracts/openapi.yaml`; `OpenApiContractTests`)
- [x] El índice único de correo se crea al iniciar (`MongoDbContext.cs:277-281`; `MongoUserAndComercioRepositoryTests`)
- [x] Ninguna respuesta incluye passwordHash (`AuthEndpointsTests.Register_WhenValid_Returns201WithJwtAndNoPasswordHash`)
- [x] No se registra PII en logs

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (`Sensus.ArchitectureTests` 6/6)
- [x] Pruebas unitarias y de integración añadidas y en verde (`RegisterUser*` + `AuthEndpointsTests` + `MongoUserAndComercioRepositoryTests`)
- [x] Cobertura ≥ 80 % en Domain y Application (`90.05 %` / `88.64 %`)
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica (no aplica en 001; SSM en 045)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada (`appsettings.json` sin secreto JWT)
- [ ] Rotación operativa del secreto JWT en SSM tras retiro de `appsettings.json` (acción del usuario: rotar valor `/sensus/prod/JwtSettings/Secret` en SSM; ver plan cierre §5.Z-5)
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS (`appsettings.json` sin secreto; BCrypt + DTO sin `passwordHash`)
- [x] **V. Verificación automatizada** — PASS (`AuthEndpointsTests`, `MongoUserAndComercioRepositoryTests`, `RegisterUserCommand*Tests`)
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS (índice único en `correo`, `correlation-id`)
- [x] **VIII. Nivel fintech y ciclo seguro** — PASS (STRIDE en `plan.md` §4 y escaneos bloqueantes de CI en verde `ci.yml`/`dast-zap.yml`, corrida `4a00d24`)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Users/Commands/RegisterUserCommand*Tests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Authentication/AuthEndpointsTests.cs`, `tests/Sensus.IntegrationTests/Persistence/MongoUserAndComercioRepositoryTests.cs` | ☑ |
| Contrato OpenAPI | `specs/001-registro-usuario/contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Antigravity (Bloque B1) | Completada (rotación JWT en SSM en §5.Z-5) | Verificado en código/pruebas: `AuthEndpointsTests` (201/409/400 sin `passwordHash`), `MongoUserAndComercioRepositoryTests` (`ux` en `users.correo` contra MongoDB real Testcontainers), `appsettings.json` con `"Secret": ""` y Constitución I–VIII PASS (`4a00d24`). |
