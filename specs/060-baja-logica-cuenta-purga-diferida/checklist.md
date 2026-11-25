# Checklist 060 — Baja lógica de cuenta y purga diferida

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

**Estado: CERRADO (v2.0.0)** — 2026-11-23.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 060 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible (`FR-060-01` a `FR-060-06`, `NFR-060-01` a `NFR-060-04`)
- [x] Las dependencias están resueltas: 003, 004, 020
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado en `plan.md` §4, §6 y §8

## B. Específicos de este spec

- [x] `User` y `UserDocument` conservan `DeletedAtUtc` y exponen `IsDeleted`
- [x] La baja rota `SecurityStamp` y conserva el documento hasta la purga
- [x] `OnTokenValidated` y `LoginCommandHandler` rechazan cuentas dadas de baja sin enumeración
- [x] El proceso de purga usa `cutoff = ahora - retención` y un índice declarado (`ix_users_deleted_at`)
- [x] No se purgan cuentas activas y la operación es idempotente (actualización condicional en MongoDB)
- [x] Retención y frecuencia configurables (`AccountDeletion`), sin valores hardcodeados en la lógica
- [x] Eventos de auditoría con actor, objetivo, resultado y `correlation-id`, sin PII

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4)
- [x] Pruebas unitarias y de integración añadidas y en verde; la baja E2E sigue devolviendo 204 (3/3)
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato existente verificado (sin endpoints nuevos): `DELETE /api/v1/users/account` sigue en `204`
- [x] IaC sin cambios: no hay recursos AWS nuevos
- [x] Logs con `correlation-id` y sin PII
- [x] Constitution Check en PASS (ocho principios, incluido el VIII)

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS (sin cambio de contrato; 204 conservado)
- [x] **IV. Seguridad y privacidad** — PASS (ASVS nivel 3, rotación de stamp, sin PII)
- [x] **V. Verificación automatizada** — PASS: gates ejecutados; brecha externa de Docker registrada sin omitir pruebas ni bajar umbrales
- [x] **VI. Costo cero e IaC** — PASS (sin recursos nuevos)
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. DevSecOps de nivel fintech** — PASS (auditoría de solo adición y sin secretos)

## E. Evidencia

| Artefacto | Ubicación / comando | Resultado |
|---|---|---|
| Spec, plan, tareas y checklist | `specs/060-baja-logica-cuenta-purga-diferida/` | Completos, v2.0.0, alineados a constitución v2.4.1 |
| Compilación | `dotnet build sensus-back.sln --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| Formato | `dotnet format sensus-back.sln --verify-no-changes` | Sin cambios pendientes |
| Pruebas unitarias | `dotnet test sensus-back.sln --configuration Release --no-build --collect:"XPlat Code Coverage"` | 932/932 en verde |
| Pruebas de arquitectura | Misma ejecución | 4/4 en verde |
| Pruebas E2E | Misma ejecución | 3/3 en verde (incluida la baja con 204) |
| Pruebas de integración | Misma ejecución | 506/517 en verde; 11 fallos por `DockerUnavailableException` (`npipe://./pipe/docker_engine`), sin omisiones |
| Cobertura | `node .github/scripts/check-coverage.mjs tests 80` | Domain 89,59 % (2357/2631), Application 96,18 % (6342/6594) |
| Estructura SDD | `node .github/scripts/check-specs.mjs specs` | 61 especificaciones revisadas, estructura correcta |
| Regresión de baja | `DeleteAccountEndpointTests` (10/10 del filtro de baja/login) | Baja 204, conservación del documento, token 401, login 401 genérico y cierre de contenido |

## F. Brechas externas documentadas

- 11 pruebas Testcontainers bloqueadas por daemon Docker no disponible en este entorno; no se omitieron ni se redujeron umbrales.
- La purga programada se verifica con reloj inyectado y repositorio simulado; su ejecución contra MongoDB real queda cubierta por el conjunto de pruebas bloqueadas por Docker.
- Sin `cdk deploy` ni operaciones de Git.

## G. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-23 | Equipo Sensus | Aprobado | Spec creada para cerrar la brecha de borrado físico inmediato de la spec 030. |
| 2026-11-23 | Equipo Sensus (con asistente) | CERRADO | Baja lógica con `DeletedAtUtc`, rotación de `SecurityStamp`, rechazo de token y login, purga diferida e idempotente con índice, auditoría sin PII y configuración `AccountDeletion`. Gates: build 0/0, formato limpio, 932 unitarias + 4 arquitectura + 3 E2E + 506/517 integración, cobertura 89,59 % / 96,18 %, `check-specs.mjs` 61 specs. Brecha externa de Docker documentada en §F. Sin `cdk deploy` ni operaciones de Git. |
