# Checklist 020 — Sanciones y baneo de usuario

Alineado a `constitution.md` v2.3.0 · `AGENTS.md` v2.3.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 020 v2.1.0 está aprobada por el responsable (2026-11-19)
- [x] Los valores de D-1 (24 h, 7 d, 30 d, permanente; ventana de 90 días) están confirmados
- [x] La alerta push al usuario sancionado está confirmada (D-7)
- [x] Los requisitos tienen identificador y criterio medible (CA-020-01 a CA-020-12)
- [x] Las dependencias están resueltas: 004, 017, 018, 019, 027
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La sanción es reversible y auditada (motivo, evidencia, responsable y fecha)
- [x] El bloqueo es efectivo en el login (403 `Auth.UserSanctioned`)
- [x] Los tokens emitidos antes de la sanción dejan de funcionar (401) en la siguiente solicitud
- [x] Solo un administrador puede consultar, aplicar manualmente y revertir
- [x] Un administrador no puede sancionarse a sí mismo ni a otro administrador
- [x] La decisión `Quarantine` (incluida la falla de moderación) no genera sanción
- [x] Una sanción revertida no cuenta para el escalamiento
- [x] Las duraciones y la ventana salen de `Moderation:Sanctions`, no del código
- [x] La respuesta del login no expone evidencia, categorías ni puntajes
- [x] Un fallo al sancionar no publica el contenido ni rompe la respuesta de la subida
- [x] El usuario recibe una alerta push al aplicarse y al revertirse la sanción
- [x] Sin enumeración de cuentas: la sanción solo se revela con credenciales válidas

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application (92.0 % en la medición del 2026-11-19)
- [x] Contrato OpenAPI actualizado (spec 020 y respuesta 403 de `/auth/login`)
- [x] IaC sin cambios confirmada (sin recursos nuevos; no se ejecutó `cdk diff`)
- [x] Logs con correlation-id y sin PII (eventos `SecurityAudit` con identificadores, sin motivo libre)
- [x] Revisión de seguridad: sin secretos, autorización verificada en el borde y en el caso de uso
- [x] Constitution Check en PASS o brecha registrada (VIII con observación: brecha G-05)
- [x] `tasks.md`, `specs/README.md`, `requirements.md` y `memory.md` reflejan el estado real
- [x] Modelo de amenazas (STRIDE) en `plan.md`
- [ ] Escaneos bloqueantes DAST y SBOM (Principio VIII, regla 8): no existen aún en el pipeline; brechas G-02 y G-04 del Apéndice F

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. DevSecOps de nivel fintech** — PASS CON OBSERVACIONES (G-02, G-04 y G-05 abiertas)

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Sanctions/` (dominio, política, handlers, notificador) y `Users/Commands/LoginCommandHandlerTests.cs` | ☑ 711 en verde |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Endpoints/SanctionEndpointsTests.cs` | ☑ 27 casos |
| Persistencia en MongoDB real | `tests/Sensus.IntegrationTests/Persistence/MongoSanctionRepositoryTests.cs` | ☑ 12 casos contra `mongod` 8.0 |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |
| Medición de NFR-020-01 | `ExistingToken_AfterSuspension_IsRejectedImmediately_AndWorksAgainAfterReversal` | ☑ la siguiente solicitud ya es rechazada |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Spec corregida a v2.0.0 | Contratos, decisiones D-1 a D-6 y tareas rehechas |
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Implementada v2.1.0 | Alerta push (D-7), modelo de amenazas, pruebas unitarias (711), de integración (283) y 12 contra MongoDB real. `NFR-020-01` se verificó como «siguiente solicitud rechazada», no con una medición de latencia |
