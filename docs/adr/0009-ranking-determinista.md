# ADR 0009 — Ranking determinista en el backend, sin servicio de ML externo

| Campo | Valor |
|---|---|
| Estado | Aceptada |
| Fecha | 2026-10-29 |
| Principios afectados | I, VI, VII |
| Specs relacionados | 041, 042, 043, 054, 055 |

## Contexto

El catálogo incluye tres endpoints de «Machine Learning» (búsqueda, recomendaciones y alternativas) sin
definir fuente de datos, proveedor ni métrica. La constitución prohíbe proveedores fuera de AWS, prohíbe
modelos propios de visión artificial y exige que todo criterio de aceptación sea medible. Entrenar o
servir un modelo externo añadiría costo y una dependencia sin justificación para el alcance del curso.

## Decisión

- Implementar el ranking en `Sensus.Application` como función determinista y explicable.
- Combinar señales disponibles en MongoDB: coincidencia de texto, distancia, vigencia, popularidad
  (vistas, apartados) y afinidad (favoritos, me gusta, apartados).
- Externalizar los pesos por configuración y versionarlos; registrar la versión del ranking usada.
- Usar los índices de texto y `2dsphere` de MongoDB 8 como motor de recuperación.
- Definir métricas verificables: latencia p95, cobertura de recomendaciones, proporción de consultas con
  coincidencia de categoría en el top 10 y estabilidad del orden.
- No introducir servicios de ML gestionados ni modelos entrenados en esta fase.

## Consecuencias

**Positivas**

- Costo cero adicional y despliegue dentro de la Lambda existente.
- Resultados reproducibles y probables con datos fijos.
- Sin dependencia de un proveedor de ML ni de un pipeline de entrenamiento.

**Negativas**

- Menor calidad que un sistema entrenado con datos reales.
- Los pesos requieren calibración manual.
- La búsqueda depende de las limitaciones del índice de texto de MongoDB.

**Mitigación**

- Métricas de calidad en las specs 041–043 y revisión periódica de pesos.
- Camino abierto a sustituir la función por un adaptador si el producto lo exige (nuevo ADR).

## Alternativas consideradas

| Alternativa | Motivo del descarte |
|---|---|
| Amazon Personalize | Costo fuera del Free Tier y sin datos históricos suficientes |
| Modelo entrenado propio | Prohibido para visión; además no hay corpus ni pipeline |
| Servicio de búsqueda externo | Proveedor fuera de AWS y costo adicional |
| Ordenar solo por fecha | Ignora cercanía y preferencias, contradice el objetivo del catálogo |

## Actualización 2026-11-20

La spec 042 pasa a ser el **feed de promociones por intereses** (`GET /api/v1/feed/offers`), con intereses
explícitos (054), diversidad y exploración deterministas. La decisión de este ADR se mantiene: el ranking sigue
siendo una función determinista. La spec 055 registra impresiones, aperturas y apartados seudonimizados para
medir el feed y, en el futuro, evaluar un modelo aprendido (por ejemplo, Random Forest con ML.NET dentro de la
Lambda). Esa dirección queda documentada en el [ADR 0015](0015-ranking-aprendido-mlnet.md), diferido hasta tener datos.

## Referencias

- `constitution.md` P-I, P-VI y P-VII
- `specs/041-busqueda-inteligente/`, `specs/042-recomendaciones/`, `specs/043-alternativas-oferta/`
