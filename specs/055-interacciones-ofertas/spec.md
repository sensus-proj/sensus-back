# Spec 055 — Registro de interacciones con ofertas

| Campo | Valor |
|---|---|
| ID | 055 |
| Slug | `interacciones-ofertas` |
| Estado | Pendiente |
| Ola | 5 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 014, 030, 032, 033, 042 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Para medir si el feed de promociones (042) funciona, y para entrenar en el futuro un modelo de ranking (por
ejemplo un Random Forest, que exigirá un ADR nuevo), se necesita saber **qué ofertas se mostraron, cuáles se
abrieron y cuáles se apartaron**. Hoy solo existen vistas (032) y apartados (033) sueltos, sin relación con lo
que se mostró. Este spec registra esas interacciones de forma **seudonimizada**, con retención acotada y sin
endpoints públicos nuevos.

## 2. Alcance

**Incluye**

- Eventos `impression` (oferta servida en el feed), `open` (detalle abierto) y `claim` (apartado creado)
- Seudónimo del usuario con HMAC-SHA256 y clave versionada en SSM; nunca el `userId` en claro
- Contexto del evento: posición, `slot`, `rankingVersion`, `snapshotId`, categoría y rango de distancia
- Retención de 180 días con índice TTL
- Purga de los eventos del usuario al eliminar la cuenta (030)
- Métricas agregadas de calidad del feed: tasa de apertura y de apartado por `slot` y `rankingVersion`

**No incluye**

- Endpoints públicos para que el cliente envíe eventos
- Exportación de datos o entrenamiento de modelos (spec y ADR futuros)
- Coordenadas exactas, texto libre o datos personales
- Eventos de invitados

## 3. Actores

- Sistema (backend), a partir de las acciones del usuario registrado

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-055-01 | Registrar un evento `impression` por cada oferta servida en una página del feed (042) con posición, `slot`, `rankingVersion` y `snapshotId` | Must |
| FR-055-02 | Registrar un evento `open` cuando un usuario registrado abre el detalle de una oferta (032) | Must |
| FR-055-03 | Registrar un evento `claim` cuando se crea un apartado (033) | Must |
| FR-055-04 | Identificar al usuario solo por `HMAC-SHA256(userId, clave)` con la versión de la clave; la clave vive en SSM *SecureString* | Must |
| FR-055-05 | Guardar la distancia como rango (`0-1`, `1-3`, `3-10`, `10+` km), nunca coordenadas | Must |
| FR-055-06 | Purgar los eventos a los 180 días con índice TTL | Must |
| FR-055-07 | Purgar los eventos del usuario al eliminar la cuenta, calculando el seudónimo con todas las versiones de clave vigentes | Must |
| FR-055-08 | Registrar los eventos sin bloquear ni hacer fallar la operación principal; un fallo de registro se cuenta como métrica | Must |
| FR-055-09 | No registrar eventos de invitados | Must |
| FR-055-10 | Publicar métricas diarias de tasa de apertura y de apartado por `slot` y `rankingVersion` | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-055-01 | Sobrecarga en la operación principal | p95 < 20 ms añadidos |
| NFR-055-02 | Privacidad | 0 `userId`, correos, nombres o coordenadas en la colección y en logs |
| NFR-055-03 | Pérdida de eventos | < 1 % de eventos no registrados por día |
| NFR-055-04 | Almacenamiento | `offer_interactions` ≤ 150 MB (≈ 30 % del clúster Atlas M0) |

## 6. Criterios de aceptación

### CA-055-01 — Impresiones

- **Dado** una página del feed con 20 ofertas
- **Cuando** se sirve
- **Entonces** existen 20 eventos `impression` con posiciones 1–20, el mismo `snapshotId` y su `rankingVersion`

### CA-055-02 — Seudónimo

- **Dado** cualquier evento registrado
- **Cuando** se inspecciona el documento
- **Entonces** no contiene el `userId` en claro ni coordenadas, y sí `pseudoUserId` y `keyVersion`

### CA-055-03 — Fallo de registro

- **Dado** que la escritura de eventos falla
- **Cuando** se sirve el feed o se crea un apartado
- **Entonces** la operación principal responde con éxito y se incrementa la métrica de eventos perdidos

### CA-055-04 — Eliminación de cuenta

- **Dado** un usuario con eventos registrados con dos versiones de clave
- **Cuando** se ejecuta la purga de la spec 030
- **Entonces** no queda ningún evento de ese usuario

### CA-055-05 — Invitado

- **Dado** un invitado que abre el detalle de una oferta
- **Cuando** se registra la vista
- **Entonces** no se crea ningún evento `open`

## 7. Contratos expuestos

Ninguno. Los eventos se registran dentro de los casos de uso de 032, 033 y 042.

## 8. Dependencias y supuestos

- 014 (logs y métricas), 030 (purga), 032 (vistas), 033 (apartados) y 042 (feed).
- El aviso de privacidad informa el registro seudonimizado de interacciones, su finalidad (medir y mejorar el feed) y su retención de 180 días; el titular ejerce la cancelación eliminando su cuenta (030). Decisión del 2026-11-20: solo aviso, sin ajuste para desactivarlo.
- MongoDB de producción es **Atlas M0 (512 MB compartidos por todas las colecciones)**, confirmado el 2026-11-20.
- La clave HMAC se rota cada 90 días como máximo; las versiones anteriores se conservan 180 días (lo que dura un
  evento) solo para poder purgar por usuario.

## 9. Riesgos

- Volumen: con 1 000 usuarios activos y 3 páginas diarias ≈ 60 000 impresiones/día; con 180 días ≈ 11 M
  documentos de ~200 B ≈ 2.2 GB sin agrupar. El clúster es Atlas M0 (512 MB): se agrupan las impresiones por página
  y se aplican los controles del plan §6 para cumplir NFR-055-04.

## 10. Preguntas abiertas

- Ninguna bloqueante. El muestreo de impresiones (plan §6) se activa solo si el volumen lo exige.

## 11. Trazabilidad

- Requisitos: `FR-055-01` a `FR-055-10`, `NFR-055-01` a `NFR-055-04`
- Plan: [`plan.md`](plan.md) · Tareas: [`tasks.md`](tasks.md) · Checklist: [`checklist.md`](checklist.md)
- Consumidor: [Spec 042](../042-recomendaciones/spec.md)
