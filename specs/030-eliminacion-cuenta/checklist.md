# Checklist 030 — Eliminación de cuenta y datos

Alineado a `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1.

Usar antes, durante y al cierre de la implementación. Ninguna tarea se cierra con casillas pendientes.

**Estado: CERRADO POR ESCISIÓN (v2.0.0)** — 2026-11-23. El trabajo pendiente se ejecutó en las specs 060 y 061; este checklist conserva la trazabilidad histórica y registra la evidencia agregada.

## A. Antes de empezar (Definition of Ready)

- [x] La spec 030 está aprobada y sin preguntas bloqueantes (preguntas resueltas en §10 de la spec y de la Spec 061)
- [x] Los requisitos tienen identificador y criterio medible (`FR-030-01` a `FR-030-08`, `NFR-030-01` a `NFR-030-03`)
- [x] Las dependencias están resueltas: 003, 004, 020
- [x] El alcance y las exclusiones están declarados
- [x] El impacto en seguridad, costo y rendimiento fue evaluado en `plan.md` §4, §6 y §8

## B. Específicos de este spec

- [x] La eliminación exige confirmación explícita (Spec 060)
- [x] El token deja de servir de inmediato (Spec 060: `SecurityStamp`, `OnTokenValidated`)
- [x] La purga cubre users, profiles y tokens (Spec 060: purga diferida e idempotente)
- [x] La auditoría queda anonimizada (Spec 061: `moderation_audit` sin PII con TTL)
- [x] Intereses e interacciones del feed borrados al eliminar la cuenta (T030-13)
- [x] Los apartados activos se expiran antes de archivar las ofertas (Spec 061)
- [x] El contrato conserva `204` en `DELETE /api/v1/users/account` (no el `202` del borrador)

## C. Antes de entregar (Definition of Done)

- [x] Código en la capa correcta y test de arquitectura en verde (4/4)
- [x] Pruebas unitarias y de integración añadidas y en verde; E2E 3/3
- [x] Cobertura ≥ 80 % en Domain y Application
- [x] Contrato existente verificado (sin endpoints nuevos)
- [x] IaC sin cambios: no hay recursos AWS nuevos
- [x] Logs con `correlation-id` y sin PII
- [x] Constitution Check en PASS (ocho principios, incluido el VIII), con la brecha histórica resuelta por 060/061

## D. Constitution Check

- [x] **I. Especificación y trazabilidad** — PASS (030 Como Escindida, 060 y 061 con cuatro artefactos y requisitos propios)
- [x] **II. Clean Architecture** — PASS
- [x] **III. CQRS y contratos** — PASS (204 conservado; comando de cierre separado en 061)
- [x] **IV. Seguridad y privacidad** — PASS (brecha de borrado físico inmediato y apartados activos cerrada por 060/061)
- [x] **V. Verificación automatizada** — PASS con brecha externa de Docker documentada, sin omisiones ni umbrales reducidos
- [x] **VI. Costo cero e IaC** — PASS (sin recursos nuevos)
- [x] **VII. Rendimiento y observabilidad** — PASS
- [x] **VIII. DevSecOps de nivel fintech** — PASS (auditoría de solo adición, sin PII ni secretos, retención declarada)

## E. Evidencia

| Artefacto | Ubicación / comando | Resultado |
|---|---|---|
| Spec, plan, tareas y checklist | `specs/030-eliminacion-cuenta/` | Completos, v2.0.0, alineados a constitución v2.4.1; estado `Escindida` |
| Ejecución de la baja lógica y purga | `specs/060-baja-logica-cuenta-purga-diferida/` | Cerrada v2.0.0 con evidencia |
| Ejecución de anonimización y cierre comercial | `specs/061-anonimizacion-contenido-cierre-comercial/` | Cerrada v2.0.0 con evidencia |
| Compilación | `dotnet build sensus-back.sln --configuration Release -warnaserror` | 0 advertencias, 0 errores |
| Formato | `dotnet format sensus-back.sln --verify-no-changes` | Sin cambios pendientes |
| Pruebas | `dotnet test sensus-back.sln --configuration Release --no-build --collect:"XPlat Code Coverage"` | 932 unitarias, 4 arquitectura y 3 E2E en verde; 506/517 integración (11 bloqueadas por Docker) |
| Cobertura | `node .github/scripts/check-coverage.mjs tests 80` | Domain 89,59 % (2357/2631), Application 96,18 % (6342/6594) |
| Estructura SDD | `node .github/scripts/check-specs.mjs specs` | 61 especificaciones revisadas, estructura correcta |

## F. Brechas externas documentadas

- 11 pruebas Testcontainers bloqueadas por daemon Docker no disponible en este entorno; no se omitieron ni se redujeron umbrales.
- La verificación de índices/TTL contra MongoDB real depende de esas pruebas; las declaraciones y pruebas HTTP están en verde en 060/061.
- Sin `cdk deploy` ni operaciones de Git.

## G. Registro

| Fecha | Responsable | Resultado | Observaciones |
|---|---|---|---|
| 2026-11-15 | Equipo Sensus | PARCIAL | `DELETE /api/v1/users/account` con contraseña; borra foto, comercio, tokens de recuperación y usuario. Defecto: ofertas huérfanas quedan Published. Auditoría documental 2026-11-15: build -warnaserror OK; 523 unitarias, 4 de arquitectura y 116 de integración en verde; check-specs OK. |
| 2026-11-15 | Equipo Sensus | Corrección FR-030-06 | `DeleteAccountCommandHandler` archiva las ofertas no archivadas del comercio e invalida su caché antes de borrarlo. Regresión (falló antes del arreglo) y 2 unitarias nuevas. Sigue pendiente T030-09 (apartados activos). |
| 2026-11-20 | Ingeniería Sensus (con asistente) | Cierre FR-030-08 (`T030-13`) | Purga inmediata de `user_interests`, `offer_interactions` (todas las versiones HMAC) y caché de afinidad, con regresión unitaria e integración. |
| 2026-11-23 | Equipo Sensus (con asistente) | CERRADO POR ESCISIÓN | Brecha constitucional del Principio IV resuelta: Spec 060 (baja lógica, rotación de stamp, rechazo de token/login, purga diferida con índice) y Spec 061 (anonimización, expiración de apartados antes de archivar, auditoría sin PII con TTL). Contrato `204` conservado. Gates agregados: build 0/0, formato limpio, 932 unitarias + 4 arquitectura + 3 E2E + 506/517 integración, cobertura 89,59 % / 96,18 %, `check-specs.mjs` 61 specs. Brecha externa de Docker documentada en §F. Sin `cdk deploy` ni operaciones de Git. |
