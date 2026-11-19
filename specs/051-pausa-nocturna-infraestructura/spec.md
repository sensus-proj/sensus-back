# Spec 051 — Pausa nocturna de la infraestructura

| Campo | Valor |
|---|---|
| ID | 051 |
| Slug | `pausa-nocturna-infraestructura` |
| Estado | Implementado (pendiente de despliegue) |
| Ola | 7 |
| Tipo | Nuevo |
| Dueño | Rodrigo Sánchez |
| Dependencias | 015, 045, 048 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Activar Redis en AWS (Spec 048, activación de Redis) exige una VPC y un NAT Gateway, que cobran por hora aunque no haya
tráfico. Entre las 22:30 y las 07:00 las tiendas están cerradas y no hay tráfico útil. El responsable decidió
el 2026-11-19 pausar la infraestructura en ese horario para no pagar esas horas.

## 2. Alcance

**Incluye**

- Pausa diaria a las 22:30 y reanudación a las 07:00, hora del centro de México (`America/Mexico_City`).
- Durante la pausa: la API no acepta tráfico, el NAT Gateway y su IP elástica se eliminan y las acciones de
  las alarmas se silencian.
- Al reanudar: se recrean el NAT y la ruta de salida, se reactivan las alarmas y la API vuelve a aceptar tráfico.
- Horario y zona horaria configurables por contexto de CDK, nunca fijos en código.
- Al desplegar se aplica el estado que corresponde a la hora actual.

**No incluye**

- Pausar la caché Valkey Serverless: solo se puede pausar borrándola, lo que cambia su endpoint. Se mantiene
  encendida (mínimo facturable de 100 MB, ≈ 6 USD/mes).
- Pausar MongoDB Atlas, Cloudinary, OpenAI o Firebase, que están fuera de AWS.
- Recursos sin costo por hora: Lambda, API Gateway y DynamoDB solo cobran por uso; CloudWatch y Budgets tienen
  costo fijo mínimo.
- Un mensaje de mantenimiento en la app móvil (pertenece al repositorio móvil).

## 3. Actores

- Sistema (EventBridge Scheduler)
- Operador (puede invocar la función a mano)

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-051-01 | Pausar a las 22:30 y reanudar a las 07:00 (`America/Mexico_City`) con EventBridge Scheduler | Must |
| FR-051-02 | En la pausa, cortar el tráfico de la API con concurrencia reservada en 0 | Must |
| FR-051-03 | En la pausa, eliminar el NAT Gateway, su IP elástica y la ruta de salida de las subredes privadas | Must |
| FR-051-04 | Al reanudar, recrear NAT, IP y ruta, y devolver la concurrencia a la API solo cuando haya salida a internet | Must |
| FR-051-05 | Silenciar las acciones de las alarmas durante la pausa y reactivarlas al reanudar | Must |
| FR-051-06 | Aplicar el estado según la hora actual al desplegar | Should |
| FR-051-07 | Configurar horario y zona horaria por contexto de CDK | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-051-01 | Ahorro | 8.5 h diarias sin NAT Gateway ni IP elástica (≈ 35 % de su costo) |
| NFR-051-02 | Mínimo privilegio | La función solo borra recursos con la etiqueta `sensus:night-switch=<entorno>` y solo toca la API, las alarmas y las tablas de rutas propias |
| NFR-051-03 | Tiempo de reanudación | API disponible en ≤ 5 min tras las 07:00 |
| NFR-051-04 | Idempotencia | Ejecutar dos veces la misma acción no duplica recursos ni falla |

## 6. Criterios de aceptación

### CA-051-01

- **Dado** las 22:30 en `America/Mexico_City`
- **Cuando** corre la pausa
- **Entonces** la API tiene concurrencia reservada 0, no existe NAT ni IP elástica etiquetados y las alarmas no notifican

### CA-051-02

- **Dado** las 07:00
- **Cuando** corre la reanudación
- **Entonces** existe un NAT disponible, las subredes privadas tienen ruta `0.0.0.0/0` hacia él, las alarmas notifican y la API responde

### CA-051-03

- **Dado** un despliegue a cualquier hora
- **Cuando** termina
- **Entonces** el estado coincide con la franja horaria (pausado de 22:30 a 07:00, activo el resto)

### CA-051-04

- **Dado** que la acción ya se aplicó
- **Cuando** se repite
- **Entonces** termina sin error y sin recursos duplicados

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| — | — | — |

Sin endpoints. Durante la pausa, la API responde con error de API Gateway por falta de concurrencia.

## 8. Dependencias y supuestos

- Depende de: 015 (alarmas), 045 (stack serverless) y 048 (activación de Redis en VPC).
- MongoDB Atlas debe aceptar conexiones desde la IP del NAT. Como la IP elástica cambia cada día, la lista de
  acceso de Atlas no puede depender de una IP fija.
- Los procesos internos que expiran ofertas y apartados no corren durante la pausa y se ponen al día al reanudar.

## 9. Riesgos

- Si la reanudación falla, la API queda sin servicio hasta la siguiente ejecución o hasta invocar la función a
  mano. Mitigación: dos reintentos del programador y salida `NightSwitchFunctionName` para invocarla.
- Un usuario que use la app de noche verá error. Es una decisión de negocio aceptada por el responsable.

## 10. Preguntas abiertas

Ninguna.

## 11. Trazabilidad

- Requisitos de este spec: `FR-051-01` a `FR-051-07` y `NFR-051-01` a `NFR-051-04`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
- ADR: [`docs/adr/0014-redis-en-vpc-y-pausa-nocturna.md`](../../docs/adr/0014-redis-en-vpc-y-pausa-nocturna.md)
