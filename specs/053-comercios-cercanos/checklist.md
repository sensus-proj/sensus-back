# Checklist 053 — Comercios cercanos

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0.

El backend está completado y verificado localmente. Los escaneos de CI siguen pendientes para el cierre formal.

## A. Antes de empezar (Definition of Ready)

- [x] Retro-spec del código existente con requisitos y criterios medibles
- [x] Dependencias identificadas: 005, 010, 013, 052

## B. Específicos de este spec

- [x] Solo comercios activos, ordenados por cercanía
- [x] Radio recortado en el servidor para invitados
- [x] Coordenadas validadas (T053-03)
- [x] Resultado paginado 20/100 (T053-04)
- [x] Límite de tasa (T053-05)

## C. Antes de entregar (Definition of Done)

- [x] Contrato OpenAPI documentado
- [x] Pruebas de integración del endpoint y de MongoDB real (T053-06)
- [ ] Constitution Check completo. Pendientes los escaneos bloqueantes de CI del Principio VIII.

## D. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Contrato | [`contracts/openapi.yaml`](contracts/openapi.yaml) | ☑ |
| HTTP | `MerchantProfileEndpointsTests`, `GuestSessionEndpointsTests` | ☑ |
| MongoDB | `NotificationMongoRepositoryTests.NearbyMerchants_ReturnsStablePagesAndCountsOnlyActiveMerchantsInsideRadius` | ☑ |

## E. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-19 | Rodrigo Sánchez (con asistente) | Retro-spec creada | Comportamiento leído del código; 4 brechas registradas |
| 2026-11-22 | Codex | Verificada en local | Respuesta paginada, alias, validación y límite de tasa probados. Pendientes los escaneos de CI. |

Para el cierre formal, ejecutar el flujo [CI del repositorio](../../.github/workflows/ci.yml) sobre la revisión que contiene la paginación y registrar el identificador de la corrida y el resultado de los escaneos bloqueantes.
