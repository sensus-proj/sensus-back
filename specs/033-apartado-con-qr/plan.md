# Plan 033 — Apartado de oferta con código QR

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado |
| Versión | 1.0.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## 1. Resumen técnico

Permitir que un usuario aparte una unidad de una oferta y reciba un código QR de un solo uso que el comerciante canjea en el local.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Entidad OfferClaim con estados Active, Redeemed, Expired y reglas de canje |
| Application | ClaimOfferCommand, RedeemClaimCommand y ExpireClaimsJob |
| Infrastructure | Actualización atómica con findOneAndUpdate, firma HMAC y generación de QR |
| Api | Endpoints de apartado y canje |

Para `FR-033-09` y `FR-033-10`, Application avisa después de persistir el apartado y usa `IPushDispatcher`. La clave por apartado y dispositivo reserva como máximo un intento para evitar duplicados incluso cuando el resultado de FCM es incierto. Infrastructure registra la reserva atómica en MongoDB con TTL sobre `expiresAtUtc`. Se intenta avisar a ambos destinatarios en paralelo dentro de un presupuesto compartido de dos segundos; medir el impacto sobre NFR-033-01 antes de desplegar. Si se agota el tiempo, la confirmación del apartado conserva su respuesta y la entrega no se reintenta automáticamente. El móvil consumirá `expiresAtUtc` para mostrar la cuenta regresiva. No se modifica el contrato HTTP ni se provisionan servicios AWS nuevos.

Las pruebas de MongoDB usan `Testcontainers.MongoDb` 4.16.0 (licencia MIT, costo de licencia cero, dependencia exclusivamente de pruebas) y la imagen `mongo:7.0` que usa el repositorio en CI.

## 3. Modelo de datos e índices

- offer_claims con índice único userId + offerId + status, índice único codeHash e índice TTL en expiresAt

## 4. Seguridad

- Código firmado con HMAC y caducidad
- Solo se guarda el hash del código
- Canje restringido al comercio propietario
- Sin datos personales en el QR

## 5. Observabilidad y rendimiento

- Métricas de apartados, canjes y expiraciones
- Alerta si el stock liberado no coincide con los apartados vencidos

## 6. Costo AWS

Sin costo incremental: Lambda y API Gateway dentro del Free Tier.

## 7. Estrategia de pruebas

- Unitarias de la máquina de estados
- Integración de concurrencia por la última unidad
- Prueba de canje duplicado y de código ajeno
- Probar destinatarios, preferencias, duplicados y fallo de despacho sin revertir el apartado. Verificar unicidad de la reserva con MongoDB real.

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-033-01` y 7 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | PASS | Paginación, índices y observabilidad en §5 |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Una condición de carrera puede vender stock inexistente
- Un QR filtrado permite el canje si no se valida la propiedad

## 10. Orden de implementación

1. `T033-01` Actualizar y validar el contrato OpenAPI del spec
2. `T033-02` Implementar: Entidad OfferClaim con estados Active, Redeemed, Expired y reglas de canje
3. `T033-03` Implementar: ClaimOfferCommand, RedeemClaimCommand y ExpireClaimsJob
4. `T033-04` Implementar: Actualización atómica con findOneAndUpdate, firma HMAC y generación de QR
5. `T033-05` Implementar: Endpoints de apartado y canje
6. `T033-06` Modelar OfferClaim y sus transiciones
7. `T033-07` Garantizar la reserva atómica de stock
8. `T033-08` Implementar el job de expiración y liberación
9. `T033-09` Firmar y verificar el código del QR con HMAC
10. `T033-10` Implementar las pruebas de integración del flujo completo
11. `T033-11` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
12. `T033-12` Registrar evidencias del Constitution Check

FR-033-11 utiliza el índice _id de apartados, el repositorio de comercios y el firmador existente. CQRS de lectura, DTO explícito y verificación del propietario antes de regenerar el QR. Sin infraestructura nueva ni costo incremental.
