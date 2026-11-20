# Checklist 059 — Filtro de groserías y moderación textual fail-closed

| Criterio | Estado | Verificación |
|---|---|---|
| Spec 059 completa con 4 artefactos y `contracts/openapi.yaml` | `[x]` | `check-specs.mjs specs` |
| Constitución: STRIDE y 8 principios evaluados en `plan.md` | `[x]` | `plan.md` §8 |
| Moderación textual 100 % fail-closed ante errores, timeouts o ausencia de clave | `[x]` | `OpenAiTextModerationServiceTests` |
| Cero umbrales numéricos hardcodeados en `OpenAiTextModerationService` | `[x]` | Revisión de `GetThresholdForCategory` + `appsettings.json` |
| Cobertura Domain y Application >= 80 % | `[x]` | `Sensus.Domain` 87.01 %, `Sensus.Application` 88.43 % |
