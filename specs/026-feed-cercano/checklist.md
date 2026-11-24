# Checklist 026 — Feed geolocalizado

Alineado a `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 026 está aprobada y sin preguntas bloqueantes
- [ ] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 024, 007, 010. La 024 está a cargo de su responsable. Para integrar la 026 falta verificar su contrato de lectura y el cambio de visibilidad al eliminar.
- [ ] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [ ] El contenido no publicado nunca aparece
- [ ] La consulta usa el índice
- [ ] El TTL de caché está documentado
- [ ] Las publicaciones sin ubicación quedan fuera
- [ ] La API acepta un radio de 5 km y devuelve 400 por encima del máximo (`CA-026-01`, `CA-026-05`)

## C. Antes de entregar (Definition of Done)

- [ ] Código en la capa correcta y test de arquitectura en verde
- [ ] Pruebas unitarias y de integración añadidas y en verde
- [ ] Cobertura ≥ 80 % en Domain y Application
- [ ] Contrato OpenAPI actualizado
- [ ] IaC actualizada y `cdk diff` revisado si aplica
- [ ] Logs con correlation-id y sin PII
- [ ] Revisión de seguridad: sin secretos, autorización verificada
- [ ] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [ ] **I. Especificación y trazabilidad** — PASS
- [ ] **II. Clean Architecture** — PASS
- [ ] **III. CQRS y contratos** — PASS
- [ ] **IV. Seguridad y privacidad** — PASS
- [ ] **V. Verificación automatizada** — PASS
- [ ] **VI. Costo cero e IaC** — PASS
- [ ] **VII. Rendimiento y observabilidad** — PASS
- [ ] **VIII. DevSecOps de nivel fintech** — verificar los controles de STRIDE y las puertas de CI aplicables

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☐ |
| Plan | [`plan.md`](plan.md) | ☐ |
| Tareas | [`tasks.md`](tasks.md) | ☐ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☐ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☐ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| — | — | — | — |
