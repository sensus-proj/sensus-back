# Checklist 030 — Eliminación de cuenta y datos

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 030 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 003, 004, 020
- [x] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La eliminación exige confirmación explícita
- [x] El token deja de servir de inmediato
- [x] La purga cubre users, profiles y tokens
- [ ] La auditoría queda anonimizada

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [ ] Cobertura ≥ 80 % en Domain y Application
- [ ] Contrato OpenAPI actualizado
- [ ] IaC actualizada y `cdk diff` revisado si aplica
- [ ] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [ ] **IV. Seguridad y privacidad** — BRECHA: borrado físico inmediato sin estado Deleted ni purga a 30 días; los apartados activos de las ofertas archivadas no se resuelven (T030-09)
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☐ |
| Plan | [`plan.md`](plan.md) | ☐ |
| Tareas | [`tasks.md`](tasks.md) | ☐ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☐ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☐ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `DELETE /api/v1/users/account` con contraseña; borra foto, comercio, tokens de recuperación y usuario. Defecto: ofertas huérfanas quedan Published. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-15 | Equipo Sensus | Corrección FR-030-06 | `DeleteAccountCommandHandler` archiva las ofertas no archivadas del comercio e invalida su caché antes de borrarlo. Regresión `DeleteAccount_ShouldArchiveMerchantOffers_WhenAccountOwnsMerchant` (falló antes del arreglo) y 2 unitarias nuevas. Build sin advertencias, formato limpio; 525 unitarias, 4 de arquitectura, 117 de integración y 3 E2E en verde. Sigue pendiente T030-09 (apartados activos). |
