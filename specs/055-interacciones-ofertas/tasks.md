# Tareas 055 — Registro de interacciones con ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Plan | [`plan.md`](plan.md) |
| Checklist | [`checklist.md`](checklist.md) |
| Estado | Implementado |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## Tareas

| ID | Tarea | Capa | Requisito | Depende | Estado |
|---|---|---|---|---|---|
| T055-01 | `OfferInteraction` y `DistanceBucket` con invariantes y pruebas unitarias | Domain | `FR-055-05` | — | Hecho |
| T055-02 | `HmacUserPseudonymizer` con claves versionadas en SSM *SecureString* y su parámetro en `infra/` | Infrastructure | `FR-055-04` | T055-01 | Hecho |
| T055-03 | `MongoOfferInteractionRecorder` de mejor esfuerzo con índices TTL, de purga y de métricas | Infrastructure | `FR-055-06`, `FR-055-08` | T055-02 | Hecho |
| T055-04 | Invocar el registro desde el feed (042), el detalle (032, solo registrados) y el apartado (033) | Application | `FR-055-01`, `FR-055-02`, `FR-055-03`, `FR-055-09` | T055-03 | Hecho (032/033; 042 en Paso 6) |
| T055-05 | Purga por usuario con todas las versiones de clave en la eliminación de cuenta (030) | Infrastructure | `FR-055-07` | T055-03 | Hecho |
| T055-06 | Métricas EMF de escritura y calidad diaria por `slot` y `rankingVersion`; alarma de tamaño de colección | Observabilidad | `FR-055-10`, `NFR-055-03` | T055-04 | Hecho |
| T055-07 | Integración con Testcontainers: CA-055-01 a CA-055-05 e índice TTL | Pruebas | `FR-055-01`, `NFR-055-02` | T055-04, T055-05 | Hecho |
| T055-08 | Métrica diaria de tamaño (`collStats` y `dbStats`) y alarmas al 70 % del tope de la colección y del clúster M0 | Observabilidad | `NFR-055-04` | T055-07 | Hecho |
