# Checklist 027 — Registro de dispositivo para push

Alineado a `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 027 está aprobada y sin preguntas bloqueantes
- [ ] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 003, 013
- [ ] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Un token pertenece a un solo usuario
- [x] FCM reporta rechazo definitivo y se desactiva el token correspondiente
- [x] El envío no falla si no hay dispositivos
- [x] El SDK del proveedor no sale de Infrastructure

- [ ] Un teléfono Android recibe desde el backend el aviso y abre la pantalla correspondiente. (Bloqueada: acción externa y repo móvil — T027-07, T027-12 y T027-13, véase runbook en plan.md §11)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel fintech y resiliencia** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ Runbook en §11 |
| Tareas | [`tasks.md`](tasks.md) | ☑ T027-08 completada, externas identificadas |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Persistence/MongoDeviceRepositoryTests.cs` | ☑ Testcontainers MongoDB real |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-18 | Codex | Parcial | Gradle de Android declara Firebase Messaging y applicationId sensus.com. El manifiesto declara POST_NOTIFICATIONS. El adaptador de backend es simulado. Entrega real pendiente. |
| 2026-11-18 | Codex | Parcial | Firebase registra Sensus Android Dev en el proyecto Spark `sensus-dev-4f843`. La app tiene un flujo de activación que obtiene y registra el token. Queda pendiente integrar el archivo oficial, verificar la renovación y probar en un dispositivo. |
| 2026-11-18 | Codex | Parcial | Configuración oficial integrada localmente en Android. `:app:processDebugGoogleServices` y `:app:compileDebugKotlin` finalizaron con éxito. ADB no detectó un dispositivo para verificar el registro y la entrega. |
| 2026-11-18 | Codex | Entrega Android confirmada | `Prueba Sensus` se publicó en el Android físico mediante Firebase Console. Falta verificar la ruta backend → FCM y aprobar su plan técnico. |
| 2026-11-18 | Codex | Backend verificado en local | Plan aprobado. 901 pruebas pasaron, incluidas las de respuestas FCM, estado HTTP 503, preferencias sociales y arquitectura. Pendientes: parámetro SSM de pruebas, envío real desde el backend y validación en MongoDB real. |
| 2026-11-23 | Antigravity | Máximo backend alcanzado | T027-08 cerrada con pruebas contra MongoDB real en `MongoDeviceRepositoryTests.cs` (Testcontainers). T027-07, T027-12 y T027-13 documentadas en runbook `plan.md` §11 y marcadas como bloqueadas por acción externa/móvil. |

- [x] T027-08 — Verificar en MongoDB que se libera un token de otra cuenta, se conserva un token que rotó entre lectura y eliminación y el token queda asociado únicamente al nuevo destino. (Completado: `MongoDeviceRepositoryTests.cs`)
