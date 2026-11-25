# Spec 037 — Inventario de ofertas del comerciante

| Campo | Valor |
|---|---|
| ID | 037 |
| Slug | `inventario-comerciante` |
| Estado | Implementado |
| Ola | 2 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 034, 033, 010 |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

---

## 1. Contexto y problema

Permitir que el comerciante consulte el historial de sus ofertas con métricas de apartados, canjes y vistas, filtrable por estado y paginado. Las métricas se entregan en tiempo real a partir de los datos vigentes, sin desfase de minutos.

## 2. Alcance

**Incluye**

- Listado de ofertas propias activas e inactivas
- Filtro por estado resuelto en la consulta a MongoDB
- Métricas agregadas por oferta: apartados activos, canjes y vistas
- Paginación estándar de 20 por defecto y 100 como máximo

**No incluye**

- Exportación a CSV
- Series temporales
- Comparativa entre comercios
- Identidades de usuarios, códigos de canje ni hashes en la respuesta

## 3. Actores

- Usuario comerciante autenticado

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-037-01 | Listar las ofertas del comercio autenticado, incluidas las inactivas | Must |
| FR-037-02 | Filtrar por estado de la oferta | Should |
| FR-037-03 | Incluir apartados activos, canjes y vistas por oferta | Must |
| FR-037-04 | Aplicar pageSize 20 por defecto y 100 máximo; valores fuera de rango devuelven 400 | Must |
| FR-037-05 | Ordenar por fecha de creación descendente | Must |
| FR-037-06 | Impedir ver ofertas de otros comercios | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-037-01 | Latencia del listado | p95 < 600 ms |
| NFR-037-02 | Costo del agregado | Sin recorrido completo por oferta: un conteo batch por página |
| NFR-037-03 | Privacidad de métricas | Solo totales; sin PII, códigos ni hashes de apartado |

## 6. Criterios de aceptación

### CA-037-01

- **Dado** comercio con ofertas
- **Cuando** `GET /api/v1/merchants/my-offers`
- **Entonces** 200 con `PagedResult<MerchantOfferDto>` (`items`, `totalCount`, `pageNumber`, `pageSize`) y métricas por oferta

### CA-037-02

- **Dado** filtro por estado `Archived`
- **Cuando** se consulta con `?status=Archived`
- **Entonces** MongoDB recibe el filtro y solo se devuelven ofertas archivadas; un estado desconocido devuelve 400

### CA-037-03

- **Dado** `pageSize` de 200 o `page` menor que 1
- **Cuando** se consulta
- **Entonces** 400 con `ProblemDetails` y detalle de validación

### CA-037-04

- **Dado** comercio sin ofertas
- **Cuando** se consulta
- **Entonces** 200 con `items` vacío y `totalCount` 0

### CA-037-05

- **Dado** una oferta con vistas y apartados
- **Cuando** se consulta el inventario
- **Entonces** `viewsCount` proviene del contador de la oferta y `activeClaimsCount`/`redemptionsCount` de un único conteo batch sobre la página

### CA-037-06

- **Dado** dos comercios con ofertas
- **Cuando** consulta el primero
- **Entonces** la consulta usa exclusivamente su `merchantId`; no aparecen ofertas del otro comercio

## 7. Contratos expuestos

| Método | Ruta | Autorización | Respuesta |
|---|---|---|---|
| GET | `/api/v1/merchants/my-offers` | JWT con rol `Comerciante` | `PagedResult<MerchantOfferDto>` |

Parámetros de consulta: `status` (opcional), `page` (por defecto 1), `pageSize` (por defecto 20, máximo 100).

Contrato detallado: [`contracts/openapi.yaml`](contracts/openapi.yaml).

## 8. Dependencias y supuestos

- Depende de: 034 (inventario base), 033 (apartados), 010 (paginación estándar); las tres están implementadas.
- El `merchantId` se obtiene del usuario autenticado, nunca del cliente.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos mitigados

- La agregación de métricas se limita al lote de la página (máximo 100 ofertas) con un solo `Find` por `offerId`.
- El total se calcula en la misma consulta paginada del repositorio, no con un segundo recuento independiente.
- Los índices `(MerchantId, CreatedAtUtc)`, `(MerchantId, Status, CreatedAtUtc)` y `(OfferId, Status)` cubren filtro, orden y conteo.

## 10. Decisión registrada

La pregunta original sobre desfase de métricas quedó resuelta: **las métricas son en tiempo real**. `viewsCount` se lee del contador `Offer.ViewCount`, actualizado al registrar vistas, y los apartados se cuentan al momento de la consulta con `Filter.In` sobre los identificadores de la página.

## 11. Trazabilidad

- Requisitos de este spec: `FR-037-01`, `FR-037-02`, `FR-037-03`, `FR-037-04`, `FR-037-05`, `FR-037-06`
- Requisitos no funcionales: `NFR-037-01`, `NFR-037-02`, `NFR-037-03`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
