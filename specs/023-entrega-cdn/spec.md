# Spec 023 — Entrega de contenido por CDN

| Campo | Valor |
|---|---|
| ID | 023 |
| Slug | `entrega-cdn` |
| Estado | Implementada |
| Ola | 4 |
| Tipo | Nuevo |
| Dueño | Rodrigo |
| Dependencias | 022 |
| Versión | 2.0.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` |

---

## 1. Contexto y problema

Servir el contenido aprobado a través de Cloudinary CDN con optimización automática y control de acceso.

## 2. Alcance

**Incluye**

- Distribución Cloudinary CDN
- URLs firmadas para contenido privado
- Cabeceras de caché

**No incluye**

- Optimización de imágenes
- Streaming de vídeo

## 3. Actores

- Cliente móvil
- Cliente web

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-023-01 | Servir multimedia mediante Cloudinary CDN | Must |
| FR-023-02 | Firmar URLs cuando el contenido sea privado | Must |
| FR-023-03 | Aplicar cabeceras de caché por tipo de contenido | Should |
| FR-023-04 | Invalidar caché al retirar contenido | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-023-01 | Latencia de entrega | p95 < 200 ms en caché |
| NFR-023-02 | Coste | Dentro del plan gratuito de Cloudinary (25 créditos) |

## 6. Criterios de aceptación

### CA-023-01

- **Dado** contenido público aprobado
- **Cuando** se solicita la URL
- **Entonces** 200 desde Cloudinary CDN

### CA-023-02

- **Dado** URL sin firma para contenido privado
- **Cuando** se solicita
- **Entonces** 403

### CA-023-03

- **Dado** contenido retirado
- **Cuando** se solicita la URL
- **Entonces** 404 tras invalidación

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| POST | `/api/v1/media/{id}/delivery-url` | JWT (Invitado o Registrado) |
| GET | `/api/v1/media/{id}/delivery` | Ninguna (firma HMAC en query) |

## 8. Dependencias y supuestos

- Depende de: 022
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Costo por transferencia si el contenido se comparte masivamente

## 10. Preguntas abiertas

- ¿Todo el contenido requiere URL firmada?
  - **[RESUELTO 2026-11-22]**: Sí, la entrega a clientes mediante `/api/v1/media/{id}/delivery` requiere firma HMAC criptográfica con expiración corta (TTL acotado a 15 min por defecto, máx 24 h).
- ¿Cómo se maneja el contenido en cuarentena frente a la entrega?
  - **[RESUELTO 2026-11-22]**: Fail-closed estricto: el contenido en cuarentena (`MediaAssetStatus.Quarantined`, `Pending` o `Rejected`) **NO** se entrega por `/delivery` a nadie (responde 404 uniforme como recurso no disponible). La revisión de cuarentena por administradores usa su propio flujo en Spec 019, nunca este endpoint de entrega.

## 11. Trazabilidad

- Requisitos de este spec: `FR-023-01`, `FR-023-02`, `FR-023-03`, `FR-023-04`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
