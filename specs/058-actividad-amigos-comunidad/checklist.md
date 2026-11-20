# Checklist 058 — Actividad de amigos en comunidad

| Criterio | Estado | Verificación |
|---|---|---|
| Spec 058 completa con 4 artefactos y `contracts/openapi.yaml` | `[x]` | `check-specs.mjs specs` |
| Separación explícita documentada respecto a Specs 024–026 | `[x]` | Sección 1 de `spec.md` |
| Constitución: STRIDE y 8 principios evaluados en `plan.md` | `[x]` | Principio VII en `FAIL` por brecha registrada `FR-058-04` |
| Constitución Check en `PASS` (sin brechas abiertas) | `[ ]` | Queda en `FAIL` hasta migrar a `PagedResult` (`FR-058-04` / `T058-05`) |
| Solo incluye amigos `Accepted` + actividad propia (likes y comentarios/respuestas) | `[x]` | `GetFriendsCommunityActivityQueryHandler` |
| Pruebas unitarias e integración en verde | `[x]` | `dotnet test` + `SocialEndpointsIntegrationTests` + `MongoSocialRepositoriesTests` |
