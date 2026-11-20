# Checklist 055 — Registro de interacciones con ofertas

Alineado a `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0.

## A. Antes de empezar (Definition of Ready)

- [x] Alcance confirmado: insumo de métricas del feed y de un modelo futuro, sin endpoints públicos
- [x] Requisitos y criterios de aceptación medibles
- [x] Modelo de amenazas STRIDE en el plan
- [x] Riesgo de almacenamiento estimado con controles (plan §6); clúster confirmado Atlas M0
- [ ] Aviso de privacidad actualizado con el registro de interacciones (landing y FT-23 — FT-23 se implementa en móvil Paso 8)

## B. Específicos de este spec

- [x] Seudónimo HMAC con clave versionada en SSM (T055-02)
- [x] Escritura de mejor esfuerzo que no rompe la operación principal (T055-03)
- [x] Índice TTL de 180 días (T055-03)
- [x] Purga en eliminación de cuenta con todas las versiones de clave (T055-05)

## C. Antes de entregar (Definition of Done)

- [x] Pruebas unitarias e integración en verde; cobertura ≥ 80 % en Domain y Application
- [x] Test de arquitectura en verde
- [x] 0 PII en la colección y en logs (NFR-055-02)
- [ ] IaC del parámetro SSM con `cdk diff` revisado (`Sensus.Infra.csproj` compila 0 errores/warnings; `cdk` CLI ausente en entorno local Windows y pendiente `cdk diff` en CI)
- [x] Constitution Check en PASS tras la implementación
- [x] `memory.md` actualizado

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Unitarias | `tests/Sensus.Application.UnitTests/Offers/Interactions/OfferInteractionTests.cs` | ☑ |
| Integración HTTP/Repo | `tests/Sensus.IntegrationTests/Offers/OfferInteractionEndpointsTests.cs`, `Persistence/MongoOfferInteractionRepositoryTests.cs` | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-20 | Rodrigo Sánchez (con asistente) | Spec creada | Base de datos de entrenamiento para un ranking aprendido futuro (requiere ADR nuevo) |
| 2026-11-20 | Ingeniería Sensus (con asistente) | Implementado (`T055-01`..`T055-08`) | `OfferInteraction` + `OfferInteractionCatalog` (`DistanceBucket`), `HmacUserPseudonymizer` (SSM `/sensus/<env>/Interactions/HmacKeysJson`), `MongoOfferInteractionRecorder` (timeout 200 ms, muestreo solo `impression`), índices TTL 180 d + purga + calidad en `MongoDbContext`, integrado en 032/033 (042 en Paso 6), `EmbeddedInteractionMetricsPublisher` (`InteractionStorageMaxPct` 10/10 Free Tier + logs estructurados sin PII) y alarma CloudWatch al 70 % en `SensusInfraStack`. Pendientes externos: MongoDB real en CI (Docker detenido), `cdk synth`/`diff` en CI (`cdk` CLI ausente local), carga p95 (`G-13`) y aviso FT-23/landing. |
