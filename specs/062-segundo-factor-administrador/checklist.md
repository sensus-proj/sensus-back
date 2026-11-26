# Checklist 062 — Segundo factor TOTP para Administrador

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 062 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 002, 004, 020, 021
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] Vectores oficiales SHA-1 del RFC 6238 en verde sin `Otp.NET`
- [x] Secreto cifrado AES-256-GCM en reposo y códigos de recuperación SHA-256 de un solo uso
- [x] Desafío intermedio en login cuando Admin tiene TOTP activo; token de desafío rechazado fuera de `/mfa/verify`
- [x] Todas las rutas `AdminOnlyPolicy` rechazan `403` sin `amr=mfa` o con `mfa_at` vencido
- [x] Lockout tras 5 intentos fallidos y auditoría de solo adición registrada

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado y validado
- [x] Logs con correlation-id y sin PII ni secretos/códigos TOTP
- [x] Constitution Check en PASS

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. Nivel Fintech** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-26 | Ejecutor nocturno | PASS | `MfaHandlersTests` + `LoginCommandHandlerTests` (12/12) y `Rfc6238TotpServiceTests` + `MfaEndpointsTests` + suites Admin + `OpenApiContractTests` (66/66). Brecha `G-05` cerrada en código; falta provisión SSM `/sensus/prod/Mfa/EncryptionKey` en despliegue. |
