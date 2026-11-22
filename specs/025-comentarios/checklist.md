# Checklist 025 — Comentarios del feed

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 025 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas o desacopladas: `017` (`ITextModerationService`), `010` (`PagedResult<T>`), `024` desacoplado vía `IFeedPostVisibilityLookup`
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] El listado está paginado
- [x] El índice compuesto existe (`ix_feed_comments_post_created_at`)
- [x] Todo comentario pasa por moderación (`ProfanityFilter` + `ITextModerationService`)
- [x] No se comenta en publicaciones ocultas o inexistentes (`404 NotFound`)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado (`contracts/openapi.yaml`)
- [x] IaC sin cambios requeridos (solo MongoDB Atlas / Lambda existente)
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Feed/FeedCommentsTests.cs` | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Feed/FeedCommentEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-21 | Beto (Sprint 5) | IMPLEMENTADO | Desacoplado de Spec 024 mediante `IFeedPostVisibilityLookup` sobre `feed_posts`. |
