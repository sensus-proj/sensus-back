# Plan 009 — Errores uniformes con ProblemDetails

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Garantizar que toda respuesta de error use ProblemDetails RFC 7807 (`type`, `title`, `status`, `detail`, `instance`, `extensions.correlationId`) con un identificador de correlación y sin filtrar detalles internos (stack traces, nombres de colecciones o tipos de base de datos).

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Api | GlobalExceptionHandler, CorrelationIdMiddleware, ResultExtensions.ToHttpResult/ToProblemDetails, y SecurityExtensions para 429 |
| Application | Common/Exceptions mapeados centralizadamente; Result/Error sin acoplamiento a HTTP |
| Domain | Common/Error y ErrorType sin dependencias externas |

## 3. Modelo de datos e índices

- Sin cambios de datos

## 4. Seguridad

- No filtrar rutas internas, versiones, nombres de colecciones de MongoDB ni detalles de infraestructura (OWASP ASVS / Principio IV y VIII).
- Encabezado `X-Correlation-Id` propagado uniformemente en encabezados HTTP y cuerpo de error.

## 5. Observabilidad y rendimiento

- Cada error registra correlationId y código interno (Principio VII).
- Sin overhead de serialización manual ni duplicación de payloads.

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias del mapeo de excepciones y de `ResultExtensions`.
- Integración de 400, 401, 403, 404, 409, 429 y 500 en `ProblemDetailsUniformityTests.cs`.
- Arquitectura en `ArchitectureTests.cs` verificando que ningún endpoint instancie `new ProblemDetails` manualmente.

## 8. Constitution Check

Evaluación integral tras la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-009-01` a `FR-009-04` cubiertos con criterios de aceptación medibles y catálogo de errores |
| II | PASS | Cambios acotados a las capas declaradas en §2; Domain y Application limpios |
| III | PASS | Result/Error utilizados de forma canónica; sin entidades de dominio en respuestas |
| IV | PASS | Reglas de seguridad aplicadas: cero fugas de stack trace, colecciones o PII |
| V | PASS | 7 pruebas de integración en `ProblemDetailsUniformityTests` y 1 de arquitectura en verde |
| VI | PASS | Costo cero verificado en §6 |
| VII | PASS | `correlationId` en todas las respuestas de error (header y cuerpo) y sin recorridos costosos |
| VIII | PASS | OWASP ASVS L2/L3: respuestas de error estandarizadas sin enumeración ni fuga de metadatos |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Cambiar el formato rompe clientes que parsean el cuerpo actual

## 10. Orden de implementación

1. `T009-01` Implementar: GlobalExceptionHandler y middleware de correlación
2. `T009-02` Implementar: Excepciones de dominio mapeadas a códigos
3. `T009-03` Implementar: Sin dependencias de presentación
4. `T009-04` Unificar el mapeo de Result y excepciones a ProblemDetails
5. `T009-05` Añadir correlationId a todas las respuestas de error
6. `T009-06` Implementar las pruebas de integración del flujo completo
7. `T009-07` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
8. `T009-08` Registrar evidencias del Constitution Check
