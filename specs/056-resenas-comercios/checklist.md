# Checklist 056 — Reseñas y calificaciones de comercios

| Criterio | Estado | Verificación |
|---|---|---|
| Spec 056 completa con 4 artefactos y `contracts/openapi.yaml` | `[x]` | `check-specs.mjs specs` |
| Todos los `FR-056-*` y `NFR-056-*` registrados en `specs/requirements.md` | `[x]` | Revisión cruzada con `requirements.md` |
| Constitución: STRIDE y 8 principios evaluados en `plan.md` | `[x]` | Principio VII en `PASS` tras implementar paginación en BD y agregación de estrellas |
| Constitución Check en `PASS` (sin brechas abiertas) | `[x]` | Principio VII en `PASS` (`FR-056-07` / `T056-05`); todos los principios en `PASS` |
| Moderación fail-closed activa en reseñas y respuestas | `[x]` | `ContentModerationHelper.EnsureApprovedAsync` |
| Pruebas unitarias e integración reales en verde | `[x]` | `dotnet test` + `SocialEndpointsIntegrationTests` + `MongoSocialRepositoriesTests` |
