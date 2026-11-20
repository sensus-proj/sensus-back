# Plan 041 — Búsqueda de ofertas

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Propuesto (realineado a v2.4.0) |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Permitir buscar ofertas por texto combinando relevancia y cercanía, con un ranking determinista y medible dentro del backend.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Application | SearchOffersQuery, cálculo de puntaje y DTO |
| Infrastructure | Índice de texto en MongoDB y consulta geoespacial combinada |
| Api | Endpoint GET /api/v1/offers/search |

## 3. Modelo de datos e índices

- offers con índice de texto en title, description y merchantName; índice 2dsphere

## 4. Seguridad

- Sin exponer documentos internos
- Consulta y paginación acotadas
- Validación con lista de permitidos, límite de tasa y errores uniformes (FR-041-08 a FR-041-10)
- Invitado: radio recortado a 2 km en el servidor con `GuestRadiusPolicy` (FR-041-11); el cliente no puede ampliarlo
- Ruta: `/api/v1/offers/search` se registra antes de `/api/v1/offers/{id}` y el identificador usa la restricción `{id:guid}` para evitar colisiones

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Extracción masiva del catálogo con búsquedas vacías paginadas | Divulgación de información | `read-heavy`, `pageSize` ≤ 100 y radio ≤ 50 km | Bajo |
| Consultas de texto costosas o con caracteres de control para degradar MongoDB | Denegación de servicio | Longitud ≤ 80, lista de permitidos y consulta siempre acotada por índice `2dsphere` | Bajo |
| Inyección de operadores en la consulta (`$where`, regex) | Manipulación | El texto se pasa solo a `$text` como valor, nunca como operador ni regex construida | Bajo |
| Ofertas no visibles expuestas por coincidencia de texto | Divulgación de información | Filtro de visibilidad (FR-041-03, FR-041-06) aplicado en la consulta | Bajo |
| Términos de búsqueda en logs | Divulgación de información | Logs sin `q` ni coordenadas: solo longitud, conteo y latencia | Bajo |

## 5. Observabilidad y rendimiento

- Métricas de latencia, consultas vacías y cero resultados

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias del cálculo de puntaje
- Integración de orden, fallback y exclusiones
- Prueba de determinismo con datos fijos

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-041-01` y 9 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |
| VIII | PASS | STRIDE en §4, lista de permitidos, errores uniformes y límite de tasa |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Sin motor de búsqueda dedicado, el índice de texto de MongoDB tiene límites de idioma
- Los pesos mal calibrados degradan la relevancia

## 10. Orden de implementación

1. `T041-01` Actualizar y validar el contrato OpenAPI del spec
2. `T041-02` Implementar: SearchOffersQuery, cálculo de puntaje y DTO
3. `T041-03` Implementar: Índice de texto en MongoDB y consulta geoespacial combinada
4. `T041-04` Implementar: Endpoint GET /api/v1/offers/search
5. `T041-05` Definir la función de puntaje y sus pesos
6. `T041-06` Crear el índice de texto y verificar su uso
7. `T041-07` Implementar el fallback a ofertas cercanas
8. `T041-08` Implementar las pruebas de integración del flujo completo
9. `T041-09` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
10. `T041-10` Registrar evidencias del Constitution Check
