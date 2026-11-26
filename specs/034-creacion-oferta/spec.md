# Spec 034 — Creación de oferta

| Campo | Valor |
|---|---|
| ID | 034 |
| Slug | `creacion-oferta` |
| Estado | Parcial |
| Ola | 2 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 005, 017, 022 |
| Versión | 1.1.0 |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

---

## 1. Contexto y problema

Permitir que un comerciante publique una oferta con precio, descuento, stock, vigencia e imágenes, sujeta a moderación previa.

## 2. Alcance

**Incluye**

- Alta de oferta con validaciones de negocio
- Moderación de texto e imágenes antes de publicar
- Estado inicial Draft o Published
- Límite de ofertas activas por comercio
- Cálculo del precio con descuento

**No incluye**

- Edición (spec 035)
- Cambio de estado (spec 036)
- Duplicado de ofertas
- Pagos

## 3. Actores

- Usuario comerciante
- Servicio de moderación

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-034-01 | Crear una oferta asociada al comercio del token | Must |
| FR-034-02 | Validar precio, descuento entre 1 y 90 %, stock ≥ 1 y vigencia coherente | Must |
| FR-034-03 | Moderar texto e imágenes antes de publicar | Must |
| FR-034-04 | Permitir guardar como Draft sin moderación y sin visibilidad pública | Must |
| FR-034-05 | Limitar a 20 ofertas activas por comercio | Should |
| FR-034-06 | Calcular y persistir el precio final a partir del descuento | Must |
| FR-034-07 | Devolver 202 cuando la moderación quede pendiente | Should |
| FR-034-08 | Exigir para publicar una categoría del catálogo de producto `OfferCategory` (`Grocery`, `CleaningHome`, `Perishables`, `Dairy`, `Food`, `Bakery`, `Beverages`, `Pets` y `Pharmacy`); un valor ausente o fuera del catálogo responde `400`. Un borrador (`FR-034-04`) puede guardarse sin categoría | Must |
| FR-034-09 | Normalizar una sola vez las ofertas existentes: los nombres en español que enviaba el móvil se convierten al código (Abarrotes → `Grocery`, Limpieza y Hogar → `CleaningHome`, Perecederos → `Perishables`, Lácteos → `Dairy`, Comida → `Food`, Panadería → `Bakery`, Bebidas → `Beverages`, Mascotas → `Pets`, Farmacia → `Pharmacy`); los valores no reconocibles quedan nulos y se cuentan en la evidencia de la migración | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-034-01 | Latencia de creación | p95 < 1 s sin moderación |
| NFR-034-02 | Publicaciones sin veredicto | Cero |

## 6. Criterios de aceptación

### CA-034-01

- **Dado** datos válidos, sin imágenes
- **Cuando** POST /api/v1/offers
- **Entonces** 201 en estado Published

### CA-034-02

- **Dado** moderación pendiente o moderador de texto temporalmente no disponible (`timeout`/error transitorio)
- **Cuando** `POST /api/v1/offers` (`saveAsDraft=false`)
- **Entonces** la API responde `202 Accepted` con `status=PendingModeration` y cabecera `Location`, la oferta queda oculta al público general (`404` para no propietarios, sin cercanía ni notificaciones) hasta aprobarse en re-moderación diferida y ser notificada únicamente al publicarse (`FR-034-07`)

### CA-034-03

- **Dado** descuento de 95 %
- **Cuando** se intenta crear
- **Entonces** 400

### CA-034-04

- **Dado** vigencia con fin anterior al inicio
- **Cuando** se intenta crear
- **Entonces** 400

### CA-034-05

- **Dado** un comercio con `Offers:MaxActivePerMerchant` (20 por defecto) ofertas en estado `Published` con `EndsAt > nowUtc` (excluye `Draft`, `Paused`, `Expired`, `Archived` y `Published` vencidas)
- **Cuando** intenta crear una oferta publicada (`saveAsDraft=false`), reanudar una oferta pausada a `Published` o republicar una oferta vencida
- **Entonces** el servidor responde `409 Conflict` (`ProblemDetails` con código `Offer.ActiveLimitReached`); con 19 activas o al crear un borrador (`saveAsDraft=true`) con 20 activas permite crear con `201 Created` (`FR-034-05`)

### CA-034-06 — Categoría obligatoria

- **Dado** una oferta para publicar sin categoría o con `"Moda"`
- **Cuando** `POST /api/v1/offers`
- **Entonces** `400` con `ProblemDetails` que indica el campo `category`

### CA-034-07 — Categoría válida

- **Dado** una oferta con `category = "Bakery"`
- **Cuando** `POST /api/v1/offers`
- **Entonces** `201` y la oferta guarda el código `Bakery`

### CA-034-08 (FR-034-04) — Borrador privado

- **Dado** un comerciante autenticado con una oferta válida
- **Cuando** solicita crear con `saveAsDraft=true`
- **Entonces** recibe `201` con `status=Draft`. La oferta aparece en su inventario, queda oculta en los endpoints públicos y no genera notificaciones.

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| POST | `/api/v1/offers` | JWT |

## 8. Dependencias y supuestos

- Depende de: 005, 017, 022
- `OfferCategory` describe el producto y es distinto de `MerchantCategory`, que clasifica al comercio (005). Lo consumen el feed de promociones (042) y los intereses del usuario (054). Decisión del responsable del 2026-11-20.
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Publicar antes del veredicto si se invierte el orden de las operaciones
- Un límite de ofertas mal calibrado bloquea comercios legítimos

## 10. Preguntas abiertas

El contrato actual recibe el porcentaje. El precio final lo calcula el servidor conforme a `FR-034-06`.

## 11. Trazabilidad

- Requisitos de este spec: `FR-034-01`, `FR-034-02`, `FR-034-03`, `FR-034-04`, `FR-034-05`, `FR-034-06`, `FR-034-07`, `FR-034-08`, `FR-034-09`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
