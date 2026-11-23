# Checklist 058 — Actividad de amigos en comunidad

| Criterio | Estado | Verificación |
|---|---|---|
| Spec 058 completa con 4 artefactos y `contracts/openapi.yaml` | `[x]` | `check-specs.mjs specs` |
| Separación explícita documentada respecto a Specs 024–026 | `[x]` | Sección 1 de `spec.md` |
| Constitución: STRIDE y 8 principios evaluados en `plan.md` | `[x]` | Paginación e índices de las tres fuentes verificados |
| Constitución Check en `PASS` (sin brechas abiertas) | `[ ]` | Pendientes los escaneos bloqueantes del pipeline |
| Solo incluye amigos `Accepted` + actividad propia (likes y comentarios/respuestas) | `[x]` | `GetFriendsCommunityActivityQueryHandler` |
| Pruebas HTTP y MongoDB en verde | `[x]` | `CommunityEndpointsTests`, `MongoCommunityActivityTests` y `ReviewModerationTests` |

Evidencia local del 2026-11-22. Compilación con .NET 10.0.401 sin advertencias, 1111 pruebas aprobadas y cobertura de 87,81 % en Domain y 88,72 % en Application. Falta ejecutar los escaneos de CI sobre la revisión.
