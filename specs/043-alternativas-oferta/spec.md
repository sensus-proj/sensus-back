# Spec 043 — Alternativas a una oferta

| Campo | Valor |
|---|---|
| ID | 043 |
| Slug | `alternativas-oferta` |
| Estado | Pendiente |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 013, 032, 041, 052 |
| Versión | 1.1.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Sugerir ofertas similares a una dada —misma categoría, cercanas y con precio comparable— para evitar que el usuario abandone la búsqueda.

## 2. Alcance

**Incluye**

- Alternativas por categoría y cercanía
- Filtro por precio comparable
- Exclusión de la oferta original
- Límite configurable de resultados

**No incluye**

- Sustitución automática de apartados
- Comparación de comercios
- Recomendaciones personalizadas

## 3. Actores

- Usuario registrado
- Invitado (spec 052), con distancia recortada a 2 km

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-043-01 | Devolver alternativas visibles de la misma categoría que la oferta consultada | Must |
| FR-043-02 | Limitar la distancia a 10 km de la oferta original | Must |
| FR-043-03 | Excluir la oferta original del resultado | Must |
| FR-043-04 | Filtrar por precio final dentro del 120 % del precio de la oferta original | Should |
| FR-043-05 | Ordenar por cercanía y vigencia | Must |
| FR-043-06 | Responder 404 si la oferta original no existe o no es visible | Must |
| FR-043-07 | Limitar el resultado a 10 elementos, con 5 por defecto | Must |
| FR-043-08 | Validar con lista de permitidos: `id` de la ruta con formato GUID y `limit` [1, 10] | Must |
| FR-043-09 | Aplicar la política de límite de tasa `read-heavy` (spec 013) | Must |
| FR-043-10 | Responder `404` idéntico para oferta inexistente y no visible, sin revelar cuál de los dos casos aplica | Must |
| FR-043-11 | Permitir la consulta a invitados (spec 052) recortando la distancia máxima a 2 km de la oferta original | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-043-01 | Latencia | p95 < 500 ms |
| NFR-043-02 | Calidad | ≥ 80 % de consultas devuelven al menos 3 alternativas válidas |

## 6. Criterios de aceptación

### CA-043-01

- **Dado** oferta visible con alternativas
- **Cuando** GET /api/v1/offers/{id}/alternatives
- **Entonces** 200 con hasta 5 alternativas

### CA-043-02

- **Dado** sin alternativas en el radio
- **Cuando** se consulta
- **Entonces** 200 con lista vacía

### CA-043-03

- **Dado** oferta archivada
- **Cuando** se consulta
- **Entonces** 404

### CA-043-04

- **Dado** limit=20
- **Cuando** se consulta
- **Entonces** 400 por límite excedido

### CA-043-05 — Error uniforme

- **Dado** un `id` inexistente y otro de una oferta pausada
- **Cuando** se consultan ambos
- **Entonces** las dos respuestas son `404` con el mismo cuerpo `ProblemDetails`

### CA-043-06 — Sin token

- **Dado** ninguna cabecera `Authorization`
- **Cuando** se consulta
- **Entonces** `401` con `ProblemDetails`

### CA-043-07 — Invitado

- **Dado** un token de invitado
- **Cuando** `GET /api/v1/offers/{id}/alternatives`
- **Entonces** `200` solo con alternativas a 2 km o menos de la oferta original

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| GET | `/api/v1/offers/{id}/alternatives` | JWT |

## 8. Dependencias y supuestos

- Depende de: 013, 032, 041, 052
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Un radio corto deja la respuesta vacía en zonas con poca oferta

## 10. Preguntas abiertas

- Resuelto (2026-11-20): la ruta es `GET /api/v1/offers/{id}/alternatives` (subrecurso, ADR 0008) y los invitados la usan con radio de 2 km.
- Resuelto (2026-11-20): el precio comparable usa el **precio final** (`finalPrice` / `discountedPrice`): alternativa con `finalPrice <= 120 %` del `finalPrice` de la oferta original (`FR-043-04`).

## 11. Trazabilidad

- Requisitos de este spec: `FR-043-01`, `FR-043-02`, `FR-043-03`, `FR-043-04`, `FR-043-05`, `FR-043-06`, `FR-043-07`, `FR-043-08`, `FR-043-09`, `FR-043-10`, `FR-043-11`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
