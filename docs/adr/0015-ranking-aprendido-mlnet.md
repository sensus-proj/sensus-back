# ADR 0015 — Ranking aprendido del feed con Random Forest en ML.NET

| Campo | Valor |
|---|---|
| Estado | Propuesta — **diferida a una etapa próxima** (se reabre al cumplir los criterios de §3) |
| Fecha | 2026-11-20 |
| Decisor | Rodrigo Sánchez (responsable del proyecto) |
| Principios afectados | I, II, IV, V, VI, VII, VIII |
| Specs relacionados | 042, 055 (y una spec nueva al reabrirse) |
| Relación | Amplía ADR 0009; no lo sustituye mientras siga diferida |

> **Este ADR no autoriza implementar nada todavía.** Deja documentada la dirección elegida para cuando existan
> datos reales de interacción. Hasta entonces el feed usa la función determinista del ADR 0009.

## 1. Contexto

El feed de promociones (spec 042) ordena las ofertas con una fórmula de puntos de pesos fijos (ADR 0009). Es
explicable y gratuita, pero los pesos se calibran a mano. Un modelo entrenado con el comportamiento real
(qué se mostró, qué se abrió, qué se apartó) puede aprender esas combinaciones solo.

Hoy no es viable: no hay historial. La spec 055 empieza a registrar impresiones, aperturas y apartados
seudonimizados; cuando ese registro acumule suficiente volumen, este ADR se reabre.

Restricciones vigentes: solo AWS, costo cero salvo aprobación, inferencia en el backend, determinismo
(NFR-042-06), sin PII y controles de cadena de suministro del Principio VIII.

## 2. Decisión propuesta

1. **Modelo:** clasificación binaria «¿la persona aparta esta oferta?» con un **Random Forest de ML.NET**
   (`FastForest`, paquete `Microsoft.ML.FastTree`, licencia MIT). El feed ordena por la probabilidad predicha.
2. **Runtime:** **ML.NET dentro de la Lambda existente**, en `Sensus.Infrastructure`. Se carga el modelo una
   vez por instancia y se consulta con `PredictionEnginePool` (seguro entre hilos).
3. **Arquitectura:** puerto `IOfferFeedScorer` en Application con dos adaptadores:
   - `FormulaOfferFeedScorer`: la función del ADR 0009 (por defecto y respaldo).
   - `MlNetOfferFeedScorer`: el modelo entrenado.

   La diversidad, la exploración, la instantánea en Redis y el contrato de `GET /api/v1/feed/offers` **no
   cambian**. Si el modelo no carga o su hash no coincide, el feed usa la fórmula y emite una métrica.
4. **Variables del modelo, sin PII:** categoría de la oferta, si la categoría está en los intereses (054),
   afinidad por categoría, distancia, horas para vencer, descuento relativo, popularidad normalizada,
   antigüedad de la oferta, hora del día, día de la semana y posición servida (solo en entrenamiento, para
   corregir el sesgo de posición).
5. **Entrenamiento dentro de AWS:** una Lambda programada exporta los eventos seudonimizados de
   `offer_interactions`, entrena el modelo y guarda el `.zip` en un bucket S3 privado con versionado y su
   SHA-256. **Los datos no salen de AWS** (no se entrena en GitHub Actions). El recurso se declara en `infra/`.
6. **Versionado:** cada modelo tiene `rankingVersion` (por ejemplo `rf-v1`) y su hash. La configuración del
   entorno fija qué versión usa el feed; las impresiones de la spec 055 registran la versión servida.
7. **Determinismo:** con un modelo fijo, la predicción es la misma para la misma entrada. Se entrena con semilla
   fija para que el mismo conjunto de datos produzca el mismo modelo.

## 3. Criterios para reabrir este ADR

Se reabre cuando la spec 055 acumule, como mínimo:

| Criterio | Umbral |
|---|---|
| Días de datos continuos | ≥ 60 |
| Impresiones | ≥ 100 000 |
| Apartados atribuibles al feed | ≥ 2 000 |
| Usuarios con al menos un apartado | ≥ 500 |

Al reabrirse se confirman los umbrales con los datos reales y se crea la spec de implementación.

## 4. Criterios de aceptación del modelo

El modelo solo pasa a producción si cumple todo lo siguiente:

1. **Fuera de línea:** AUC ≥ 0.70 y mejor que la fórmula sobre los mismos datos de validación (separación
   temporal: se entrena con el pasado y se valida con las semanas más recientes).
2. **En línea:** prueba A/B de 2 a 4 semanas, asignando la mitad de los usuarios por hash estable de
   `pseudoUserId`. Éxito: tasa de apartado por impresión al menos 10 % mayor (relativo) que la fórmula, con
   significancia estadística.
3. **Sin regresiones:** latencia p95 dentro de NFR-042-01 y NFR-042-02; diversidad (NFR-042-05) intacta.
4. **Equivalencia:** pruebas con un conjunto fijo de casos que verifican que la Lambda reproduce las
   predicciones del entrenamiento.

Si no gana, se conserva la fórmula y se documenta el resultado.

## 5. Consecuencias

**Positivas**

- Todo queda en .NET: el mismo lenguaje, las mismas pruebas y el mismo pipeline del backend.
- Costo incremental cero o casi cero: Lambda existente y S3 dentro del nivel gratuito para modelos de pocos MB.
- Explicable mediante la importancia de variables (*Permutation Feature Importance* de ML.NET).
- Reversible en caliente: cambiar `rankingVersion` en configuración vuelve a la fórmula.

**Negativas**

- Aumenta el tamaño del paquete de la Lambda y el arranque en frío. Se mide antes de aceptar.
- Hay que mantener un proceso de reentrenamiento y vigilar la deriva del modelo.
- El ecosistema de ML.NET es más pequeño que el de Python.

**Mitigaciones**

- Respaldo automático a la fórmula ante cualquier fallo de carga.
- Reentrenamiento mensual, con alarma si la tasa de apartado cae respecto a la versión anterior.
- Si en el futuro se necesita una técnica que ML.NET no ofrece, se entrena en Python y se exporta a ONNX, que
  ML.NET también consume (`ApplyOnnxModel`) sin cambiar el puerto `IOfferFeedScorer`.

## 6. Seguridad y cadena de suministro (Principio VIII)

- El modelo es un artefacto de la cadena de suministro: se verifica su SHA-256 antes de cargarlo, se registra
  en el SBOM y solo lo escribe el rol IAM de la Lambda de entrenamiento (mínimo privilegio).
- `Microsoft.ML` y `Microsoft.ML.FastTree` pasan por el escaneo de dependencias (SCA) del pipeline.
- Los datos de entrenamiento son los de la spec 055: seudonimizados, sin coordenadas y con TTL de 180 días.
- STRIDE específico (envenenamiento de datos, sustitución del modelo, inferencia de pertenencia) en el
  `plan.md` de la spec de implementación.

## 7. Alternativas consideradas

| Alternativa | Motivo del descarte |
|---|---|
| Ejecutar el modelo en el celular (ONNX Runtime Mobile) | El dispositivo no tiene la popularidad global ni todas las ofertas; el modelo se podría extraer o manipular del APK; contradice la decisión de inferencia en el servidor (FT-20, FT-30) |
| Entrenar en Python y servir ONNX con `Microsoft.ML.OnnxRuntime` | Viable y compatible con este diseño; se prefiere ML.NET para no añadir otro lenguaje ni otro pipeline. Queda como plan B (§5) |
| Amazon SageMaker o Personalize | Costo fuera del nivel gratuito y servicio adicional sin necesidad para este volumen |
| Entrenar en GitHub Actions | Sacaría datos de interacción de AWS; se prefiere entrenar dentro de AWS |
| Mantener solo la fórmula | Es el estado actual y el respaldo permanente; este ADR solo la complementa si el modelo demuestra mejora |

## 8. Referencias

- ADR 0009 — Ranking determinista en el backend
- `specs/042-recomendaciones/` (feed de promociones), `specs/055-interacciones-ofertas/` (datos de entrenamiento)
- `constitution.md` Principios VI (costo) y VIII (nivel fintech)
