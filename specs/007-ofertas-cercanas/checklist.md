# Checklist 007 — Ofertas cercanas

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 007 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [x] Las dependencias están resueltas: 005, 010, 034
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [x] La respuesta incluye metadatos de paginación
- [x] El máximo de página está validado
- [x] El índice geoespacial se usa en la consulta
- [x] Las ofertas no vigentes nunca aparecen
- [x] El radio de 1–5 km para registrado está documentado (`FR-007-08`)
- [x] El radio estricto de 2 km para invitado está documentado (`FR-007-09`)
- [x] La equivalencia SQL Server → MongoDB está documentada (`spec.md` §8)
- [x] El mapeo `SE-008/011/012/013/016 → FR` está registrado (`spec.md` §9)
- [x] La ruta canónica y el alias del catálogo de la escuela están documentados
- [x] La consulta devuelve la distancia con `$geoNear` + `distanceField`

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [x] Pruebas unitarias y de integración añadidas y en verde
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato OpenAPI actualizado
- [x] IaC actualizada y `cdk diff` revisado si aplica
- [x] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [x] **IV. Seguridad y privacidad** — PASS
- [x] **V. Verificación automatizada** — PASS
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☑ |
| Plan | [`plan.md`](plan.md) | ☑ |
| Tareas | [`tasks.md`](tasks.md) | ☑ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/Offers/` (NearbyOffersRadiusPolicyTests, GetNearbyOffersQueryValidatorTests, GetNearbyOffersQueryHandlerTests) | ☑ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/Offers/NearbyOffersEndpointsTests.cs` | ☑ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☑ |
| Equivalencia SQL Server → MongoDB | `spec.md` §8 | ☑ |
| Mapeo `SE-###` → FR | `spec.md` §9 | ☑ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-05 | Agente OpenCode | Alineación documental v1.1.0 | Se añadieron `FR-007-08`/`FR-007-09`, la equivalencia SQL Server → MongoDB y el mapeo SE-008/011/012/013/016 → FR. Sin cambios de código. |
| 2026-11-13 | Equipo Sensus | PASS | `$geoNear` sobre índice `2dsphere`, `$lookup` a `comercios` para categoría y actividad, `$facet` para conteo y página. Radio resuelto en servidor por rol (invitado 2 km, registrado 1–5 km). |
