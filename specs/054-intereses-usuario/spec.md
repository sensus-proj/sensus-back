# Spec 054 — Intereses del usuario

| Campo | Valor |
|---|---|
| ID | 054 |
| Slug | `intereses-usuario` |
| Estado | Pendiente |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 002, 003, 013, 030, 034, 052 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

El feed de promociones (spec 042) necesita saber qué le interesa a una persona desde el primer día, antes de
que existan vistas, apartados o favoritos. Este spec permite que el usuario registrado elija las categorías que
le interesan, de forma **opcional** al registrarse y editable después desde su perfil.

## 2. Alcance

**Incluye**

- Consultar y reemplazar las categorías de interés del usuario autenticado
- Categorías del catálogo de producto `OfferCategory` (spec 034)
- Lista vacía válida (el usuario omitió el paso)
- Purga de los intereses al eliminar la cuenta (spec 030)

**No incluye**

- Hacer obligatorio el paso en el registro
- Intereses por subcategoría, marca o comercio
- Intereses de invitados
- Inferir intereses automáticamente (lo hace el ranking de 042 con las señales de 055)

## 3. Actores

- Usuario registrado (Consumidor o Comerciante)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-054-01 | Exponer `GET /api/v1/users/me/interests` con las categorías del usuario autenticado | Must |
| FR-054-02 | Exponer `PUT /api/v1/users/me/interests` que reemplaza la lista completa | Must |
| FR-054-03 | Aceptar solo valores de `OfferCategory` (spec 034), sin duplicados y con un máximo de 5 | Must |
| FR-054-04 | Aceptar la lista vacía y devolverla vacía cuando el usuario nunca eligió | Must |
| FR-054-05 | Devolver `403` a invitados y `401` sin token | Must |
| FR-054-06 | Purgar los intereses dentro del proceso de eliminación de cuenta (030) | Must |
| FR-054-07 | Aplicar límite de tasa: `read-heavy` en lectura y `sensitive-write` en escritura (013) | Must |
| FR-054-08 | Invalidar la caché de afinidad del feed (`feed:affinity:{userId}`) al actualizar los intereses | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-054-01 | Latencia | p95 < 200 ms en lectura y escritura |
| NFR-054-02 | Privacidad | 0 categorías ni identificadores de usuario en logs de CloudWatch |

## 6. Criterios de aceptación

### CA-054-01 — Guardar intereses

- **Dado** un usuario registrado
- **Cuando** `PUT /api/v1/users/me/interests` con `["Bakery","Dairy"]`
- **Entonces** `200` y un `GET` posterior devuelve esas dos categorías

### CA-054-02 — Lista vacía

- **Dado** un usuario que omitió el paso
- **Cuando** `GET /api/v1/users/me/interests`
- **Entonces** `200` con `categories: []`

### CA-054-03 — Valor no permitido

- **Dado** una lista con un valor fuera del catálogo (por ejemplo `"Moda"`), duplicados o más de 5 elementos
- **Cuando** `PUT`
- **Entonces** `400` con `ProblemDetails` y sin cambios guardados

### CA-054-04 — Invitado y sin token

- **Dado** un token de invitado o ningún token
- **Cuando** `GET` o `PUT`
- **Entonces** `403` o `401` con `ProblemDetails`

### CA-054-05 — Eliminación de cuenta

- **Dado** un usuario con intereses que elimina su cuenta
- **Cuando** se ejecuta la purga de la spec 030
- **Entonces** no queda ningún documento de intereses de ese usuario

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| GET | `/api/v1/users/me/interests` | JWT, `RegisteredUser` |
| PUT | `/api/v1/users/me/interests` | JWT, `RegisteredUser` |

Contrato en [`contracts/openapi.yaml`](contracts/openapi.yaml).

## 8. Dependencias y supuestos

- 002 (JWT), 003 (perfil `/users/me`), 013 (límite de tasa), 030 (purga) y 052 (rol invitado).
- El móvil ofrece la selección tras el registro y en Perfil; si se omite no se llama al `PUT`.
- El catálogo es `OfferCategory`, el mismo que usan las ofertas (034); no es el catálogo de comercios.
- El aviso de privacidad informa que los intereses se usan para ordenar el feed de promociones (decisión del 2026-11-20: solo aviso, sin ajuste para desactivarlo).

## 9. Riesgos

- Agregar categorías al catálogo exige revisar los textos del móvil; no rompe datos existentes.

## 10. Preguntas abiertas

- Ninguna.

## 11. Trazabilidad

- Requisitos: `FR-054-01` a `FR-054-08`, `NFR-054-01`, `NFR-054-02`
- Plan: [`plan.md`](plan.md) · Tareas: [`tasks.md`](tasks.md) · Checklist: [`checklist.md`](checklist.md)
- Consumidor: [Spec 042](../042-recomendaciones/spec.md)
