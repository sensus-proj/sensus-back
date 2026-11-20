# Tasks 057 — Filtro de groserías y moderación textual fail-closed

| ID | Requisito | Descripción | Estado |
|---|---|---|---|
| `T057-01` | `FR-057-01..02` | `ProfanityFilter`, `ContentModerationHelper` e integración en handlers de usuario, comercio, ofertas y reseñas | `[x]` Completado |
| `T057-02` | `FR-057-01..03` | Contrato OpenAPI de respuestas `400 Bad Request` en `contracts/openapi.yaml` | `[x]` Completado |
| `T057-03` | `FR-057-03..05`, `NFR-057-01..02` | Eliminar literales de umbral en `OpenAiTextModerationService`, declarar umbrales explícitos en `appsettings.json`, asegurar regex timeout y preservar fail-closed | `[x]` Completado |
| `T057-05` | `NFR-057-03` | Pruebas unitarias (`ProfanityFilterTests`) e integración (`OpenAiTextModerationServiceTests`) para umbrales configurables y fail-closed | `[x]` Completado (unitarias PASS; integración en CI) |
