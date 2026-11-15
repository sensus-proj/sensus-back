# Checklist 046 — Asistente conversacional Sensi

Alineado a `constitution.md` v1.4.0 · `AGENTS.md` v1.2.0.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

## A. Antes de empezar (Definition of Ready)

- [ ] La spec 046 está aprobada y sin preguntas bloqueantes
- [ ] Los requisitos tienen identificador y criterio medible
- [ ] Las dependencias están resueltas: 002, 007, 013, 017, 018, 030, 041
- [ ] El alcance y las exclusiones están declarados
- [ ] El impacto en seguridad, costo y rendimiento fue evaluado
- [ ] ADR 0011 aceptada y enmienda `constitution.md` v1.4.0 aplicada
- [ ] Excepción `EX-001` registrada en el Apéndice E con techo de gasto y responsable

## B. Específicos de este spec

- [ ] La recuperación de candidatos es determinista y no la decide el proveedor
- [ ] El total del paquete se recalcula en el backend y nunca excede el presupuesto
- [ ] Los identificadores ajenos al conjunto de candidatos se descartan
- [ ] El proveedor no accede a la base de datos, a credenciales ni a herramientas
- [ ] La entrada se modera antes de invocar al proveedor
- [ ] La salida se modera antes de devolverse
- [ ] Ante fallo, timeout o cuota agotada no se entrega texto generado
- [ ] La degradación determinista responde con el paquete y redacción fija
- [ ] Ninguna oferta no visible aparece entre candidatos ni en el paquete
- [ ] La sesión invitada se rechaza antes de consumir cuota
- [ ] Los límites de tokens, de mensajes por día y de tasa son configurables y están activos
- [ ] La clave del proveedor se resuelve desde AWS Secrets Manager
- [ ] El prompt no contiene PII y los logs no contienen mensajes ni texto generado
- [ ] El entrenamiento del proveedor sobre los datos está desactivado
- [ ] Las conversaciones se purgan al eliminar la cuenta
- [ ] La alarma de facturación está desplegada y probada

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
- [ ] **VI. Costo cero e IaC** — PASS CON OBSERVACIONES (excepción `EX-001`)
- [ ] **VII. Rendimiento y observabilidad** — PASS

## E. Evidencia

| Artefacto | Ubicación | Estado |
|---|---|---|
| Spec | [`spec.md`](spec.md) | ☐ |
| Plan | [`plan.md`](plan.md) | ☐ |
| Tareas | [`tasks.md`](tasks.md) | ☐ |
| Decisión | [`../../docs/adr/0011-asistente-generativo-sensi.md`](../../docs/adr/0011-asistente-generativo-sensi.md) | ☐ |
| Pruebas unitarias | `tests/Sensus.Application.UnitTests/` | ☐ |
| Pruebas de integración | `tests/Sensus.IntegrationTests/` | ☐ |
| Contrato OpenAPI | `contracts/openapi.yaml` | ☐ |

## F. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| — | — | — | — |
