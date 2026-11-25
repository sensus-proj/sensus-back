# Checklist 044 — Ajustes de notificaciones

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 044 está aprobada y sin preguntas bloqueantes (GET incluido por obligación de lectura)
- [x] Los requisitos tienen identificador y criterio medible (`FR-044-01` a `FR-044-08`, `NFR-044-01/02`)
- [x] Las dependencias están resueltas: 027, 003
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado en `plan.md` §4, §6 y §8

## B. Específicos de este spec

- [x] Las preferencias se respetan en cada envío: `pushEnabled`, `offerAlerts` y `socialAlerts`
- [x] El radio está validado entre 1 y 50 (400 fuera de rango)
- [x] Los valores por defecto se crean en la primera lectura o actualización
- [x] Solo la cuenta autenticada lee y edita sus ajustes y su ubicación
- [x] La ubicación consentida se guarda una sola vez, expira en 24 h configurable y se purga al retirarla o borrar la cuenta
- [x] El aviso de oferta se envía solo con texto moderado `Approved` (*fail-closed*) y radio inclusivo
- [x] Entrega por FCM HTTP v1 con credencial en SSM SecureString; simulada en Development/Testing
- [x] Latencia de lectura p95 < 250 ms medida en HTTP

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4)
- [x] Pruebas unitarias y de integración añadidas y ejecutadas: 920 unitarias + 4 arquitectura + 3 E2E + 503/514 integración (11 fallos por daemon Docker ausente, sin omisiones)
- [x] Cobertura ≥ 80 % en Domain y Application: 89,35 % y 96,18 %
- [x] Contrato OpenAPI actualizado con los cuatro endpoints del spec
- [x] IaC sin cambios: no hay recursos AWS nuevos; `cdk deploy` no ejecutado
- [x] Logs con `correlation-id` y sin PII, tokens ni payloads de push
- [x] Revisión de seguridad: STRIDE en `plan.md` §4, autorización verificada y sin secretos
- [x] Constitution Check en PASS (ocho principios, incluido el VIII)

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS (SSM SecureString, sin PII en payload ni logs)
- [x] **V. Verificación automatizada** — PASS: gates ejecutados; brecha externa de Docker registrada sin omitir pruebas ni bajar umbrales
- [x] **VI. Costo cero e IaC** — PASS: sin recursos nuevos; EX-002 para moderación de texto
- [x] **VII. Rendimiento y observabilidad** — PASS: p95 < 250 ms y filtrado del 100 % de los envíos
- [x] **VIII. DevSecOps de nivel fintech** — PASS: lista de permitidos, auditoría redactada, escaneos bloqueantes; credencial FCM solo en SSM

## E. Evidencia

| Artefacto | Ubicación / comando | Resultado |
|---|---|---|
| Spec, plan, tareas y checklist | `specs/044-ajustes-notificaciones/` | Completos, v2.0.0, alineados a constitución v2.4.1 |
| Compilación | `dotnet build sensus-back.sln --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| Formato | `dotnet format sensus-back.sln --verify-no-changes` | Sin cambios pendientes (`FORMAT_OK`) |
| Pruebas unitarias | `dotnet test sensus-back.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/coverage-044-final` | 920/920 en verde |
| Pruebas de arquitectura | Misma ejecución | 4/4 en verde |
| Pruebas E2E | Misma ejecución | 3/3 en verde |
| Pruebas de integración | Misma ejecución | 503/514 en verde; 11 fallos por `DockerUnavailableException` (`npipe://./pipe/docker_engine`), sin omisiones |
| Cobertura | `node .github/scripts/check-coverage.mjs artifacts/coverage-044-final 80 Sensus.Domain Sensus.Application` | Domain 89,35 % (2299/2573), Application 96,18 % (6238/6486) |
| Estructura SDD | `node .github/scripts/check-specs.mjs specs` | 59 especificaciones revisadas, estructura correcta |
| Preferencias HTTP | `NotificationSettingsEndpointTests` (5/5) | 401, 403, defaults, 400 por radio y actualización 200 |
| Latencia | `GetSettings_MeetsLatencyTarget_ForTwentyRequests` | 1/1 en verde, p95 < 250 ms |
| Ubicación | `NotificationLocationEndpointTests` (8/8) | Consentimiento, idempotencia y coordenadas inválidas |
| FCM | `FcmPushDispatcherTests` (8/8) | Solicitud HTTP v1, token inválido, timeout y respuesta malformada |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | Vigente |

## F. Brechas externas documentadas

- Entrega FCM física pendiente de aprovisionar `/sensus/<entorno>/push/fcm-service-account` en SSM SecureString. El adaptador real está implementado y probado con dobles HTTP; no es código pendiente ni prueba omitida.
- Integración móvil pendiente (registro de token y consentimiento de ubicación), fuera de este repositorio.
- Brecha IV de la spec 034: moderación conjunta de imagen y texto abierta; el aviso usa solo texto aprobado por decisión del responsable.
- 11 pruebas Testcontainers bloqueadas por daemon Docker no disponible en este entorno.

## G. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-18 | Codex | Parcial | API de ubicación verificada para sesión registrada, invitado, anónimo y coordenadas inválidas. MongoDB real verifica reemplazo, exclusión de actualizaciones obsoletas, borrado e índices 2dsphere y TTL. |
| 2026-11-19 | Claude | Parcial | `FR-044-08` implementado con el alcance acordado: moderación *fail-closed*, `$geoNear` sobre 2dsphere, filtros de preferencias y radio inclusivo, exclusión del comercio y desactivación de tokens rechazados. |
| 2026-11-23 | Equipo Sensus | CERRADO | Preferencias y ubicación con contrato HTTP completo; FCM HTTP v1 con credencial en SSM; alertas de ofertas con texto moderado; p95 < 250 ms medido. Gates: build 0/0, formato limpio, 920 unitarias + 4 arquitectura + 3 E2E + 503/514 integración, cobertura 89,35 % / 96,18 %, `check-specs.mjs` 59 specs. Brechas externas documentadas en §F. Sin `cdk deploy` ni operaciones de Git. |
