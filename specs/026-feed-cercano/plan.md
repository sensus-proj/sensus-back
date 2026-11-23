# Plan 026 — Feed geolocalizado

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Pendiente de validar la integración con la Spec 024 |
| Versión | 1.0.0 |
| Constitution Check | Pendiente de verificación |
| Alineado a | `constitution.md` v3.0.0 · `AGENTS.md` v3.0.0 |

## 1. Resumen técnico

Entregar un feed paginado de publicaciones visibles cercanas a la ubicación del usuario, ordenado cronológicamente.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Application | GetNearbyFeedQuery con proyección |
| Infrastructure | Índice geoespacial y caché de corta duración |
| Api | Endpoint GET /api/v1/feed/nearby |

## 3. Modelo de datos e índices

- Índice compuesto de estado, ubicación `2dsphere` y fecha según `NFR-026-02`. Los nombres de la colección y de los campos se toman del contrato persistido de la Spec 024.
- El filtro exige el estado publicado de forma explícita y una ubicación válida. Se ordena por fecha descendente y se usa el identificador como desempate estable.

## 4. Seguridad

- Solo contenido publicado
- Sin datos privados del autor
- Radio máximo de 5 km inclusive, validado en el borde de la API (`FR-026-04`)

### Modelo de amenazas (STRIDE)

| Amenaza | Control previsto | Verificación |
|---|---|---|
| Suplantación | Exigir JWT y aplicar la política de autenticación existente antes de consultar | Sin JWT o con JWT inválido, responder 401. Verificar permisos para el rol de invitado conforme a la política vigente. |
| Manipulación | Validar coordenadas, radio y paginación antes de construir la consulta MongoDB | Cubrir coordenadas fuera de rango, valores no finitos y radio superior a 5 km. |
| Repudio | Propagar el identificador de correlación a los registros operativos | Verificar correlación sin registrar coordenadas ni contenido. |
| Divulgación | Proyectar campos públicos y excluir estados no publicados, incluso al consultar la caché | Probar una publicación eliminada después de llenar la caché y comprobar que desaparece en la siguiente lectura. |
| Denegación de servicio | Acotar radio y tamaño de página, propagar cancelación y verificar el uso del índice | Probar los límites y revisar el plan de ejecución de MongoDB. |
| Elevación de privilegios | Aplicar la visibilidad del servidor antes de devolver cualquier publicación | Probar que una publicación rechazada o en cuarentena queda fuera con cualquier consulta válida. |

## 5. Observabilidad y rendimiento

- Métricas de aciertos de caché y latencia

## 6. Costo AWS

Caché en memoria del proceso; sin costo adicional.

## 7. Estrategia de pruebas

- Integración de paginación y filtrado
- Prueba de caché y de radio inválido
- Prueba de exclusión del resultado y del total tras eliminar una publicación que ya aparecía en caché (`CA-026-06`)

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | Pendiente | Falta verificar el contrato persistido de la dependencia 024 antes de cerrar OpenAPI y la proyección. |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |
| VIII | Pendiente | STRIDE documentado en §4. Faltan pruebas del flujo autenticado, visibilidad con caché y evidencia de los controles de CI. |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- El caché en memoria no se comparte entre instancias de Lambda
- La ubicación heredada puede quedar desactualizada si el comercio se muda

## 10. Orden de implementación

1. `T026-01` Actualizar y validar el contrato OpenAPI del spec
2. `T026-02` Implementar: GetNearbyFeedQuery con proyección
3. `T026-03` Implementar: Índice geoespacial y caché de corta duración
4. `T026-04` Implementar: Endpoint GET /api/v1/feed/nearby
5. `T026-05` Crear índice geoespacial y de estado
6. `T026-06` Implementar caché con TTL e invalidación
7. `T026-07` Validar radio y paginación
8. `T026-08` Implementar las pruebas de integración del flujo completo
9. `T026-09` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
10. `T026-10` Registrar evidencias del Constitution Check
