# Spec 052 — Sesión de invitado

| Campo | Valor |
|---|---|
| ID | 052 |
| Slug | `sesion-invitado` |
| Estado | Implementado (retro-spec) |
| Ola | 1 |
| Tipo | Retro-spec |
| Dueño | Por asignar |
| Dependencias | 002, 013 |
| Versión | 1.0.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

La app permite explorar ofertas y comercios sin registrarse. `POST /api/v1/auth/guest` ya existe en producción
(historia SE-008) sin spec que lo gobierne. Esta retro-spec documenta el comportamiento real y registra las brechas
frente a la constitución v2.4.0.

## 2. Alcance

**Incluye**

- Emisión de un JWT de invitado sin credenciales ni datos personales.
- Restricciones del invitado: radio de búsqueda máximo de 2 km y bloqueo de las operaciones de usuario registrado.
- Límite de tasa del endpoint.

**No incluye**

- Conversión de invitado a cuenta registrada conservando estado (no hay estado que conservar).
- Persistencia del invitado: no se crea ningún registro en MongoDB.

## 3. Actores

- Visitante sin cuenta

## 4. Requisitos funcionales

| ID | Requisito | Prioridad | Estado |
|---|---|---|---|
| FR-052-01 | Emitir un JWT firmado con rol `Invitado`, `sub` aleatorio por sesión y nombre `Invitado`, sin pedir credenciales | Must | Implementado |
| FR-052-02 | Devolver token, tipo `Bearer`, fecha de expiración, radio máximo (`2000` m) y rol | Must | Implementado |
| FR-052-03 | Limitar el radio de búsqueda del invitado a 2 km en el servidor, aunque el cliente pida más | Must | Implementado |
| FR-052-04 | Negar al invitado las operaciones reservadas a usuarios registrados (políticas `RegisteredUser`, `MerchantOnly` y `AdminOnly`) | Must | Implementado |
| FR-052-05 | Aplicar el límite de tasa `auth-strict` (10 solicitudes por 60 s por cliente) | Must | Implementado |
| FR-052-06 | Externalizar la vigencia del token de invitado a configuración (antes 120 min fijos en `JwtTokenGenerator`; hoy `JwtSettings:GuestExpiryMinutes`, 15 min) y alinearla al Principio VIII, regla 4 (≤ 15 min con renovación) | Must | Implementado |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-052-01 | Privacidad | El invitado no genera registros persistentes ni PII |
| NFR-052-02 | Abuso | Más de 10 solicitudes por minuto del mismo cliente reciben `429` |

## 6. Criterios de aceptación

### CA-052-01

- **Dado** un visitante sin cuenta
- **Cuando** llama a `POST /api/v1/auth/guest`
- **Entonces** recibe `200` con un token de rol `Invitado`, `maxRadiusMeters = 2000` y una expiración futura

### CA-052-02

- **Dado** un token de invitado
- **Cuando** busca ofertas o comercios con un radio mayor a 2 km
- **Entonces** el servidor recorta el radio a 2 km

### CA-052-03

- **Dado** un token de invitado
- **Cuando** llama a una operación de usuario registrado (por ejemplo, apartar una oferta o gestionar amigos)
- **Entonces** recibe `403`

### CA-052-04

- **Dado** un mismo cliente
- **Cuando** supera 10 solicitudes en 60 s
- **Entonces** recibe `429`

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| `POST` | `/api/v1/auth/guest` | Pública, límite `auth-strict` |

Contrato completo en [`contracts/openapi.yaml`](contracts/openapi.yaml).

## 8. Dependencias y supuestos

- Depende de: 002 (emisión de JWT) y 013 (límites de tasa).
- El token de invitado se valida con la misma firma, emisor y audiencia que el de usuarios registrados.

## 9. Riesgos

- Un token de invitado de 120 minutos es más largo que lo que exige el Principio VIII; se registra como FR-052-06.
- La creación masiva de tokens de invitado está acotada solo por el límite de tasa por cliente.

## 10. Preguntas abiertas

Ninguna.

## 11. Trazabilidad

- Requisitos de este spec: `FR-052-01` a `FR-052-06` y `NFR-052-01` a `NFR-052-02`
- Código: `AuthEndpoints.cs` (`/guest`), `CreateGuestSessionCommand.cs`, `JwtTokenGenerator.GenerateGuestToken`, `GuestRadiusPolicy`
- Pruebas: `tests/Sensus.IntegrationTests/Guest/GuestSessionEndpointsTests.cs`, `tests/Sensus.E2ETests/Journeys/GuestModeE2EJourneyTests.cs`
- Plan: [`plan.md`](plan.md) · Tareas: [`tasks.md`](tasks.md) · Checklist: [`checklist.md`](checklist.md)
