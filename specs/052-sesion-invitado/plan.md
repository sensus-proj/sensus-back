# Plan 052 — Sesión de invitado

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Retro-spec (documenta código existente) |
| Versión | 1.0.0 |
| Constitution Check | FAIL (retro-spec con brecha registrada) |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

`CreateGuestSessionCommand` pide a `IJwtTokenGenerator.GenerateGuestToken()` un JWT con `sub` aleatorio y rol
`Invitado`. No hay persistencia. Las restricciones viven en el servidor: `GuestRadiusPolicy` recorta el radio y las
políticas de autorización niegan al rol `Invitado` las operaciones de usuario registrado.

## 2. Cambios por capa

| Capa | Estado actual |
|---|---|
| Domain | `GuestRadiusPolicy` (2 km para invitados, 50 km para registrados) y `RoleType.Invitado` |
| Application | `CreateGuestSessionCommand` y `GuestSessionResponseDto` |
| Infrastructure | `JwtTokenGenerator.GenerateGuestToken` (120 min fijos) |
| Api | `POST /api/v1/auth/guest` con límite `auth-strict` |

## 3. Modelo de datos e índices

No aplica: sin persistencia.

## 4. Seguridad

- Sin credenciales ni PII. La autorización se decide en el servidor por rol.

### Modelo de amenazas (STRIDE, Principio VIII)

| Amenaza | Categoría | Mitigación | Riesgo residual |
|---|---|---|---|
| Un invitado intenta operaciones de registrado | Elevación de privilegios | Políticas que excluyen `Invitado`; CA-052-03 | Bajo |
| Un invitado amplía el radio desde el cliente | Manipulación | Recorte en el servidor (`GuestRadiusPolicy`) | Bajo |
| Generación masiva de tokens | Denegación de servicio | Límite `auth-strict` por cliente | Medio |
| Token de invitado robado y reutilizado | Suplantación | Vigencia corta; hoy 120 min (FR-052-06) | Medio |

## 5. Observabilidad y rendimiento

- Sin acceso a base de datos; latencia dominada por la firma del JWT.

## 6. Costo AWS

Sin costo incremental.

## 7. Estrategia de pruebas

- Existentes: integración (`GuestSessionEndpointsTests`) y E2E (`GuestModeE2EJourneyTests`).
- Pendiente con FR-052-06: prueba de que la vigencia sale de configuración.

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | Retro-spec con requisitos y criterios medibles |
| II | PASS | Capas respetadas |
| III | PASS | DTO explícito `GuestSessionResponseDto` |
| IV | FAIL | Vigencia del token fija en código; brecha FR-052-06 con tarea T052-03 |
| V | PASS | Pruebas de integración y E2E existentes |
| VI | PASS | Sin costo |
| VII | PASS | Sin colecciones ni consultas |
| VIII | FAIL | Regla 4 (≤ 15 min) incumplida; brecha FR-052-06 |

> Retro-spec: las brechas quedan registradas como deuda con tarea y fecha (§3.4 de la constitución).

## 9. Riesgos de implementación

- Acortar la vigencia obliga a la app a renovar el token de invitado.

## 10. Orden de implementación

1. `T052-01` Documentar el comportamiento existente (esta retro-spec)
2. `T052-02` Contrato OpenAPI
3. `T052-03` Vigencia configurable y alineada al Principio VIII
