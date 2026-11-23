# Spec 053 — Comercios cercanos

| Campo | Valor |
|---|---|
| ID | 053 |
| Slug | `comercios-cercanos` |
| Estado | Backend completado |
| Ola | 2 |
| Tipo | Retro-spec |
| Dueño | Por asignar |
| Dependencias | 005, 010, 013, 052 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

---

## 1. Contexto y problema

La app muestra en el mapa los comercios cercanos al usuario. `GET /api/v1/merchants/nearby` ya existe en producción
(historia SE-008). Esta retro-spec documenta el cambio de contrato a una página de comercios y los controles de entrada y tasa.

## 2. Alcance

**Incluye**

- Búsqueda de comercios activos dentro de un radio, ordenados por cercanía.
- Radio por defecto y máximos distintos para invitados y registrados.

**No incluye**

- Ofertas cercanas (Spec 007).
- Filtros por categoría u horario de apertura.

## 3. Actores

- Usuario registrado
- Invitado (Spec 052)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad | Estado |
|---|---|---|---|
| FR-053-01 | Devolver los comercios activos dentro del radio, ordenados del más cercano al más lejano (`$nearSphere`) | Must | Implementado |
| FR-053-02 | Usar 2000 m si no se indica radio; tope de 2 km para invitados y de 50 km para registrados, aplicado en el servidor | Must | Implementado |
| FR-053-03 | Permitir el acceso a usuarios registrados e invitados (política `GuestOrRegistered`) | Must | Implementado |
| FR-053-04 | Validar `lat` en [-90, 90] y `lng` en [-180, 180]; responder `400` si están fuera de rango | Must | Implementado |
| FR-053-05 | Paginar el resultado (20 por defecto, 100 máximo) con metadatos de paginación | Must | Implementado |
| FR-053-06 | Aplicar el límite de tasa `read-heavy` | Must | Implementado |
| FR-053-07 | Aceptar `radiusKm` y conservar `radioMetros` como alias temporal. Si se envían ambos, prevalece `radiusKm` | Should | Implementado |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-053-01 | Rendimiento | Consulta sobre el índice `2dsphere` de `comercios.ubicacion`; sin recorrido completo |
| NFR-053-02 | Privacidad | La respuesta solo expone datos públicos del comercio (`ComercioDto`) |

## 6. Criterios de aceptación

### CA-053-01

- **Dado** un usuario autenticado y comercios activos a 500 m y a 3 km
- **Cuando** consulta con `radioMetros=2000`
- **Entonces** recibe `200` solo con el comercio a 500 m

### CA-053-02

- **Dado** un invitado
- **Cuando** consulta con un radio de 10 000 m
- **Entonces** el servidor usa 2000 m

### CA-053-03

- **Dado** un comercio inactivo dentro del radio
- **Cuando** se consulta
- **Entonces** no aparece en el resultado

### CA-053-04

- **Dado** una solicitud sin token
- **Cuando** llama al endpoint
- **Entonces** recibe `401`

### CA-053-05 (FR-053-04)

- **Dado** `lat=120`
- **Cuando** se consulta
- **Entonces** recibe `400` `ProblemDetails`

### CA-053-06 (FR-053-05, FR-053-06)

- **Dado** tres comercios activos dentro del radio
- **Cuando** se consulta `page=2&pageSize=1`
- **Entonces** se recibe el segundo comercio por distancia, total 3 y una página siguiente. Si se supera la cuota de solicitudes, se recibe `429` `ProblemDetails`.

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| `GET` | `/api/v1/merchants/nearby` | JWT de registrado o invitado |

Contrato completo en [`contracts/openapi.yaml`](contracts/openapi.yaml).

## 8. Dependencias y supuestos

- Depende de: 005 (activación de comercios), 010 (paginación estándar), 013 (límites de tasa) y 052 (invitado).

## 9. Riesgos

- La consulta calcula el total de comercios dentro del radio. El índice geoespacial, el límite de radio y la política de tasa acotan su impacto.

## 10. Preguntas abiertas

Ninguna.

## 11. Trazabilidad

- Requisitos de este spec: `FR-053-01` a `FR-053-07` y `NFR-053-01` a `NFR-053-02`
- Código: `MerchantEndpoints.cs` (`/nearby`), `GetNearbyComerciosQuery.cs`, `MongoComercioRepository.GetNearbyAsync`, `GuestRadiusPolicy`
- Pruebas: `GuestModeE2EJourneyTests.cs`, `MerchantE2EJourneyTests.cs`
- Plan: [`plan.md`](plan.md) · Tareas: [`tasks.md`](tasks.md) · Checklist: [`checklist.md`](checklist.md)
