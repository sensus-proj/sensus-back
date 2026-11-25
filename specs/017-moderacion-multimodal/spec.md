# Spec 017 — Moderación multimodal de contenido

| Campo | Valor |
|---|---|
| ID | 017 |
| Slug | `moderacion-multimodal` |
| Estado | Parcial |
| Ola | 4 |
| Tipo | Nuevo |
| Dueño | Por asignar |
| Dependencias | 009, 022 |
| Versión | 1.2.0 |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

---

## 1. Contexto y problema

Validar imagen y texto antes de publicar ofertas, publicaciones o comentarios, usando `gpt-4o-mini` en modo visión con la política de Sensus (armas, drogas, simbología de odio, contenido sexual y violencia) y comportamiento fail-closed. Sustituye a `omni-moderation-latest`, que dejaba pasar imágenes con armas (decisión D28); `omni-moderation-latest` queda para el texto de Sensi IA (Spec 046).

## 2. Alcance

**Incluye**

- Envío conjunto de imagen y texto
- Interpretación de `decision`, `flagged_categories` y `category_scores`
- Fail-closed ante error o timeout
- Registro de la decisión
- Aplicación a ofertas del catálogo y a contenido social

**No incluye**

- Visión artificial local
- Entrenamiento de modelos
- Reportes de explotación infantil
- Panel de revisión manual (specs 019 y 021)

## 3. Actores

- Usuario que publica
- Servicio de moderación
- Moderador humano

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-017-01 | Enviar imagen y texto en la misma solicitud de moderación | Must |
| FR-017-02 | Consumir `gpt-4o-mini` en modo visión (`detail: low`) con la política de Sensus y tiempo de espera acotado; el modelo se fija en `OpenAiSettings:Model`, con costo bajo la excepción `EX-002` | Must |
| FR-017-03 | Interpretar `decision`, `flagged_categories` y `category_scores` del JSON estructurado (armas, odio, drogas, violencia, contenido sexual e ilícitos) y tratar como fallo toda respuesta malformada | Must |
| FR-017-04 | No publicar si el proveedor falla, expira o responde de forma inválida | Must |
| FR-017-05 | Registrar veredicto, categorías, puntajes y acción adoptada | Must |
| FR-017-06 | Moderar toda imagen de oferta antes de exponerla públicamente | Must |
| FR-017-07 | Exponer el resultado al flujo que lo invoca, sin endpoint público de moderación | Must |
| FR-017-08 | Moderar las publicaciones de la Spec 024 sin imagen enviando solo el texto a `gpt-4o-mini` con la política de Sensus y el mismo tratamiento fail-closed de `FR-017-04` | Must |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-017-01 | Latencia de moderación | p95 < 3 s |
| NFR-017-02 | Disponibilidad del flujo | Fail-closed al 100 % |

## 6. Criterios de aceptación

### CA-017-01

- **Dado** contenido apto
- **Cuando** se modera
- **Entonces** publicación automática y registro de puntajes

### CA-017-02

- **Dado** proveedor con timeout
- **Cuando** se modera
- **Entonces** contenido en cuarentena, nunca publicado

### CA-017-03

- **Dado** respuesta sin category_scores
- **Cuando** se modera
- **Entonces** respuesta inválida tratada como fallo

### CA-017-04

- **Dado** contenido con riesgo alto
- **Cuando** se modera
- **Entonces** rechazo registrado con las categorías activadas

### CA-017-05

- **Dado** imagen de oferta sin moderar
- **Cuando** se consulta la oferta
- **Entonces** la oferta permanece oculta

### CA-017-06 — Moderación de solo texto

- **Dado** el texto de una publicación sin imagen
- **Cuando** se modera el contenido
- **Entonces** el proveedor recibe solo el texto con la política de Sensus y una respuesta ausente, inválida o fuera de tiempo produce cuarentena

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| — | — | — |

## 8. Dependencias y supuestos

- Depende de: 009, 022
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- Dependencia externa sin SLA
- Coste oculto si el proveedor cambia de política

## 10. Preguntas abiertas

- ¿Se modera también el texto por separado?
- ¿Qué se hace con el contenido en cuarentena tras 30 días?

## 11. Trazabilidad

- Requisitos de este spec: `FR-017-01`, `FR-017-02`, `FR-017-03`, `FR-017-04`, `FR-017-05`, `FR-017-06`, `FR-017-07`, `FR-017-08`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
