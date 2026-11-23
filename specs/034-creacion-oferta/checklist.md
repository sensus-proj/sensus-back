# Checklist 034 — Creación de oferta

Alineado a `constitution.md` v1.2.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 034 está aprobada y sin preguntas bloqueantes
- [x] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 005, 017, 022
- [x] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado

## B. Específicos de este spec

- [ ] Ninguna oferta se expone sin veredicto
- [x] El comercio proviene del token
- [x] El precio final se calcula en el servidor
- [x] El borrador se lista en el inventario privado, se oculta en el detalle público y no genera avisos (`FR-034-04`)
- [x] Al crear una oferta publicada con imagen se exige el activo aprobado del titular y la coincidencia con el contexto moderado (`T034-14`)
- [ ] El límite por comercio está probado

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde
- [ ] Pruebas unitarias y de integración añadidas y en verde
- [ ] Cobertura ≥ 80 % en Domain y Application
- [ ] Contrato OpenAPI actualizado
- [ ] IaC actualizada y `cdk diff` revisado si aplica
- [ ] Logs con correlation-id y sin PII
- [x] Revisión de seguridad: sin secretos, autorización verificada
- [x] Constitution Check en PASS o brecha registrada

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS
- [ ] **IV. Seguridad y privacidad** — Falta moderar el flujo de ofertas sin imagen y resolver la cuarentena previa a Cloudinary. Las ofertas con imagen verifican aprobación, titular y huella del contexto.
- [ ] **V. Verificación automatizada** — El contrato actual tiene pruebas HTTP. Faltan los casos de moderación pendiente, límite activo y publicación segura.
- [x] **VI. Costo cero e IaC** — PASS
- [x] **VII. Rendimiento y observabilidad** — PASS

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

- [ ] Para habilitar las alertas de `FR-044-08`, persistir evidencia de moderación conjunta vinculada a la versión y contenido de la oferta. La ruta de creación actual puede guardar `Published` sin esa evidencia; ningún aviso debe usar el estado como sustituto del veredicto (`FR-034-03`, `NFR-034-02`).

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `POST /api/v1/offers` con invariantes en `Offer.Create`. Faltan Draft, límite de 20 activas y respuesta 202. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-22 | Codex | Parcial | OpenAPI documenta el contrato actual. `CreateOfferEndpointsTests` comprobó 201, precio calculado, asociación al comercio, 400, 401 y 403. El responsable mantuvo la regla de cuarentena vigente. |
| 2026-11-22 | Codex | T034-13 verificada | `OfferTests` y `CreateOfferEndpointsTests` verifican el borrador privado, inventario, falta de aviso y transiciones bloqueadas. |
| 2026-11-22 | Codex | T034-14 verificada | Se rechazó el cambio de descripción tras moderar y se comprobó con MongoDB real la coincidencia de titular, URL, estado aprobado y huella de contexto. Una liberación manual no recupera la prueba de aprobación automática. |
