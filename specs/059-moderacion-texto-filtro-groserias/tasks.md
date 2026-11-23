# Tasks 059 — Filtro de groserías y moderación textual fail-closed

| ID | Requisito | Descripción | Estado |
|---|---|---|---|
| `T059-01` | `FR-059-01..02` | Filtro e integración de moderación textual | Parcial en Back_Rocio. Integrado en reseñas y respuestas. |
| `T059-02` | `FR-059-01..03` | Contrato OpenAPI de respuestas `400 Bad Request` en `contracts/openapi.yaml` | `[x]` Completado |
| `T059-03` | `FR-059-03..05`, `NFR-059-01..02` | Umbrales en configuración y modo fail-closed | Parcial en Back_Rocio. Veredicto obligatorio para los flujos incorporados. |
| `T059-05` | `NFR-059-03` | Pruebas de filtro y publicación ante fallo | Parcial. `ReviewModerationTests` cubre veredictos y falta de respuesta. |
