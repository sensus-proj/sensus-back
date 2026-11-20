# Plan 054 — Intereses del usuario

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Propuesto |
| Versión | 1.0.0 |
| Constitution Check | PASS (previo a implementación) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

`UserInterests` es una entidad de Domain con la invariante de categorías (catálogo `OfferCategory`, sin duplicados,
máximo 5). `GetMyInterestsQuery` y `ReplaceMyInterestsCommand` leen y reemplazan el documento del usuario del
token. La escritura es idempotente (*upsert* por `userId`).

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad `UserInterests` con `Replace(IEnumerable<OfferCategory>)` y su invariante |
| Application | `GetMyInterestsQuery`, `ReplaceMyInterestsCommand` + validador, `UserInterestsDto`, puerto `IUserInterestsRepository`; invalidación de `feed:affinity:{userId}` vía puerto de caché |
| Infrastructure | `MongoUserInterestsRepository`; borrado dentro de la purga de cuenta (030) |
| Api | `GET` y `PUT /api/v1/users/me/interests` con `RegisteredUser`, `read-heavy` y `sensitive-write` |

## 3. Modelo de datos e índices

- Colección `user_interests`: `{ _id, userId, categories: [string], updatedAtUtc }`.
- Índice único sobre `userId` (lectura y *upsert* por usuario; sin recorridos de colección).
- Tamaño acotado: máximo 5 categorías por documento.

## 4. Seguridad

- El `userId` sale siempre del token; no hay parámetro de ruta con identificador (sin IDOR).
- Lista de permitidos sobre el catálogo; tamaño de cuerpo ≤ 1 KB.
- Logs sin categorías ni identificadores: solo resultado, latencia y `correlation-id`.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Leer o modificar intereses de otro usuario | Elevación de privilegios / Divulgación | `userId` del token; sin identificadores en la ruta | Bajo |
| Inyectar valores arbitrarios en el perfil | Manipulación | Lista de permitidos del catálogo; invariante en Domain | Bajo |
| Escrituras masivas para degradar el servicio | Denegación de servicio | `sensitive-write`; cuerpo ≤ 1 KB | Bajo |
| Invitado guarda intereses | Suplantación | Política `RegisteredUser` | Bajo |
| Intereses retenidos tras eliminar la cuenta | Divulgación de información | Purga en la spec 030 con prueba de regresión | Bajo |

## 5. Observabilidad y rendimiento

- Una lectura o escritura indexada por operación; sin caché propia (el feed cachea su afinidad).

## 6. Costo AWS

Sin costo incremental: MongoDB existente; ≈ 200 B por usuario.

## 7. Estrategia de pruebas

- Unitarias de la invariante de `UserInterests` y del validador.
- Integración con Testcontainers: éxito, lista vacía, `400` por cada regla, `401`, `403` invitado,
  idempotencia del `PUT` y purga en eliminación de cuenta.
- Prueba de mapeo a `UserInterestsDto`.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | 8 FR y 2 NFR medibles |
| II | PASS | Invariante en Domain; persistencia en Infrastructure |
| III | PASS | Una consulta y un comando separados; DTO propio |
| IV | PASS | JWT, `RegisteredUser`, validación de borde e invariante de dominio |
| V | PASS | Unitarias e integración (§7) |
| VI | PASS | Sin costo (§6) |
| VII | PASS | Respuesta acotada (≤ 5 elementos, no requiere paginación); índice único declarado |
| VIII | PASS | STRIDE (§4), lista de permitidos, límite de tasa y purga de datos |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Olvidar la purga en 030: se cubre con prueba de regresión obligatoria (T054-05).

## 10. Orden de implementación

1. `T054-01` Contrato OpenAPI
2. `T054-02` Entidad e invariante
3. `T054-03` Consulta, comando y validador
4. `T054-04` Repositorio e índice
5. `T054-05` Purga en eliminación de cuenta
6. `T054-06` Endpoints
7. `T054-07` Pruebas de integración
