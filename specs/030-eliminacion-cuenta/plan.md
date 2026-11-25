# Plan 030 — Eliminación de cuenta y datos

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Escindida |
| Versión | 2.0.0 |
| Constitution Check | PASS (brechas cerradas por las specs 060 y 061) |
| Alineado a | `constitution.md` v2.4.1 · `AGENTS.md` v2.4.1 |

## 1. Resumen técnico

Permitir que el titular elimine su cuenta con confirmación explícita, aplicando una retención declarada y purgando sus datos personales en cascada.

**Cierre por escisión (2026-11-23).** El diseño original preveía borrado físico inmediato y un único caso de uso de purga. La implementación real se dividió en dos specs: la 060 (baja lógica, revocación y purga diferida) y la 061 (anonimización de contenido, expiración de apartados, archivado y auditoría anonimizada). Este plan se conserva como registro histórico; las decisiones técnicas vigentes están en los planes de esas dos specs. El endpoint sigue devolviendo `204`.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Estado Deleted y reglas de retención |
| Application | DeleteAccountCommand y caso de uso de purga |
| Infrastructure | Purga programada y anonimización en MongoDB |
| Api | Endpoint DELETE /api/v1/users/account |

## 3. Modelo de datos e índices

- users con deletedAt; offers y posts anonimizados; moderation_audit sin PII
- `user_interests` (054) borrado por `userId` y `offer_interactions` (055) borrado por `pseudoUserId` calculado con cada versión de clave HMAC vigente (FR-030-08)

## 4. Seguridad

- Confirmación con contraseña
- Revocación inmediata
- Sin PII en logs
- Purga verificable

## 5. Observabilidad y rendimiento

- Métricas de eliminaciones y de purgas completadas
- Alerta si la purga no se ejecuta

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de transición a Deleted
- Integración de bloqueo y revocación
- Prueba de purga con reloj simulado

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-030-01` y 6 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS (cerrado por 060/061) | La brecha de borrado físico inmediato y apartados activos se resolvió con baja lógica, purga a 30 días, expiración previa y auditoría sin PII |
| V | PASS con brecha externa de entorno | Gates ejecutados en 060/061; 11 pruebas Testcontainers bloqueadas por Docker no disponible, sin omisiones ni umbrales reducidos |
| VI | PASS | Costo estimado declarado en §6; sin recursos nuevos en 060/061 |
| VII | PASS | Paginación, índices y observabilidad en §5; índices nuevos declarados en 060/061 |
| VIII | PASS (cerrado por 060/061) | Auditoría de solo adición, `correlation-id`, sin PII ni secretos, retención configurable y modelo STRIDE en el plan 061 |

> Sin excepciones registradas (Apéndice E de la constitución). La brecha original del Principio IV quedó documentada y resuelta en los checklists de las specs 060 y 061.

## 9. Riesgos de implementación

- Una purga incompleta deja datos personales retenidos
- Eliminar una cuenta comercial oculta ofertas con apartados activos

## 10. Orden de implementación

1. `T030-01` Actualizar y validar el contrato OpenAPI del spec
2. `T030-02` Implementar: Estado Deleted y reglas de retención
3. `T030-03` Implementar: DeleteAccountCommand y caso de uso de purga
4. `T030-04` Implementar: Purga programada y anonimización en MongoDB
5. `T030-05` Implementar: Endpoint DELETE /api/v1/users/account
6. `T030-06` Modelar el estado Deleted y la retención
7. `T030-07` Implementar la purga programada con evidencia
8. `T030-08` Anonimizar contenido público en lugar de borrarlo
9. `T030-09` Resolver apartados activos antes de desactivar ofertas
10. `T030-10` Implementar las pruebas de integración del flujo completo
11. `T030-11` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
12. `T030-12` Registrar evidencias del Constitution Check
