# Tareas 061 — Anonimización de contenido y cierre comercial

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Cerrado |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T061-01 | Añadir `Anonymize` a `FeedPost` y `FeedComment` en Domain | Domain | `FR-061-01`, `FR-061-02` | — | Implementado |
| T061-02 | Ampliar repositorios de feed con anonimización en lote por autor e implementarlos en Mongo | Domain / Infrastructure | `FR-061-01`, `FR-061-02` | T061-01 | Implementado |
| T061-03 | Añadir índice `ix_feed_comments_author` para la anonimización por lote | Infrastructure | `NFR-061-01` | T061-02 | Implementado |
| T061-04 | Añadir expiración de apartados activos por comercio al repositorio de claims y su implementación Mongo | Domain / Infrastructure | `FR-061-03` | — | Implementado |
| T061-05 | Añadir índice `ix_offer_claims_merchant_status` | Infrastructure | `NFR-061-02` | T061-04 | Implementado |
| T061-06 | Crear entidad y documento `ModerationAudit` con TTL de 90 días e índices | Domain / Infrastructure | `FR-061-05`, `NFR-061-03` | — | Implementado |
| T061-07 | Implementar repositorio de auditoría anonimizada y copia desde `Sanction.Evidence` | Infrastructure | `FR-061-05` | T061-06 | Implementado |
| T061-08 | Implementar `CloseAccountContentCommand` y su handler (anonimizar, expirar claims, archivar ofertas y auditar) | Application | `FR-061-01` a `FR-061-06` | T061-02, T061-04, T061-07 | Implementado |
| T061-09 | Invocar el cierre desde `DeleteAccountCommandHandler` antes de la baja lógica | Application | `FR-061-01` | T061-08 | Implementado |
| T061-10 | Añadir la configuración `AccountDeletion:ModerationAuditRetentionDays` | Api | `NFR-061-03` | T061-06 | Implementado |
| T061-11 | Pruebas unitarias y de integración, incluida la regresión de apartados activos | Pruebas | `NFR-061-01` a `NFR-061-04` | T061-10 | Implementado |
| T061-12 | Gates (build, formato, suites, cobertura) y cierre documental | Docs | `FR-061-06` | T061-11 | Implementado |

## Notas de ejecución

- Cada tarea se cierra actualizando `checklist.md` y `plan.md` si cambia el diseño.
- Ninguna tarea se considera terminada sin pruebas verdes y cobertura dentro del umbral.
- Los identificadores de requisito remiten a `spec.md` §4 y §5.
- La colección `sanctions` no se modifica: la copia anonimizada convive con ella y expira a 90 días.
