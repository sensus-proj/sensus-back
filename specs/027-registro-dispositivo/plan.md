# Plan 027 — Registro de dispositivo para push

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Aprobado por el responsable (2026-11-18) |
| Versión | 1.1.0 |
| Constitution Check | Brechas de implementación y evidencia registradas en el checklist |
| Alineado a | `constitution.md` v2.2.0 · `AGENTS.md` v2.2.0 |

## 1. Resumen técnico

Registrar y actualizar el token de dispositivo del usuario para habilitar notificaciones push, con despacho abstracto y limpieza de tokens inválidos.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Reutilizar `Device` y su invariante de activación |
| Application | Mantener `IPushDispatcher` como puerto. Distinguir resultado aceptado, token rechazado definitivamente y fallo del proveedor. Aplicar el resultado en el registro y despacho social |
| Infrastructure | Adaptador HTTP v1, autenticación OAuth para el alcance `firebase.messaging`, lectura de SSM y reutilización de credenciales en la instancia Lambda. Mantener la simulación configurable solo en Development y Testing |
| Api | Componer configuración y cliente HTTP. Mantener la ruta de registro y devolver `503` con ProblemDetails si la validación no está disponible |

## 3. Modelo de datos e índices

- devices con índice único en token y en userId + deviceId

## 4. Seguridad

- El token pertenece al usuario autenticado
- Sin datos sensibles en la carga
- Parámetro SSM `SecureString` estándar bajo el prefijo de cada ambiente. Leer con el rol Lambda existente y la clave administrada por AWS. El proyecto Firebase debe corresponder al ambiente y la identidad de Google solo necesita permiso para enviar mensajes en ese proyecto.
- El nombre del parámetro es `${AWS_SSM_PARAMETER_PREFIX}/push/fcm-service-account`. Su valor es el JSON de cuenta de servicio del ambiente, aprovisionado por el responsable. El adaptador toma `project_id` de esa credencial. En Development, `Push__Simulate=false` selecciona el envío real con un perfil AWS y región de pruebas. La simulación nunca se selecciona en Production.
- Validar tokens con `validate_only`. Solo un rechazo inequívoco del token permite desactivarlo. Los fallos de red, autorización, cuota o protocolo preservan el dispositivo. En el registro fallido se limita la desactivación a dispositivos de la cuenta autenticada.
- Despacho social con `pushEnabled` y `socialAlerts` antes de enumerar destinos (`FR-044-04`, `FR-050-04`). Mantener la operación social confirmada ante fallos de FCM. Los avisos de apartados y ofertas cercanas se incorporan en las specs correspondientes.

## 5. Observabilidad y rendimiento

- Registrar resultado y categoría del fallo con el contexto de correlación existente, sin token, carga de usuario ni cuerpo de respuesta de FCM. Medir los p95 de registro y despacho con la configuración de pruebas, incluyendo arranque en frío.
- Para la primera prueba social, el backend solicita entrega una vez por dispositivo activo. La persistencia de eventos y los reintentos deduplicados se definen en las specs de apartados y ofertas cercanas antes de incorporar esos disparadores.

## 6. Costo AWS

Estimación incremental para pruebas: USD 0/mes mientras Lambda, salida a internet y CloudWatch permanezcan dentro de los límites aplicables. FCM no cobra por mensajería y se consume un parámetro SSM estándar con volumen bajo y rendimiento estándar bajo el rol Lambda existente. No se añade VPC, NAT ni recursos AWS de pago. [Firebase](https://firebase.google.com/pricing) · [AWS Systems Manager](https://aws.amazon.com/systems-manager/pricing/)

Dependencias propuestas, limitadas a Infrastructure: `Google.Apis.Auth` 1.78.0 para obtención y renovación de OAuth, y `AWSSDK.SimpleSystemsManagement` 4.0.105 para leer el parámetro con el SDK AWS v4. Ambas tienen licencia Apache 2.0 y costo de licencia cero. Validar la compatibilidad con el SDK de DynamoDB mediante `dotnet restore` antes de incorporar código.

## 7. Estrategia de pruebas

- Unitarias de registro y reasignación
- Dobles HTTP para éxito, validación sin entrega, token dado de baja, fallo de autenticación, timeout y respuesta malformada. Verificar que errores operativos preservan el token y los avisos sociales respetan preferencias.
- Integración del registro con éxito, entrada inválida, falta de autenticación, usuario invitado y `503`. Verificar un aviso de amistad disparado por el backend en el Android de pruebas sin publicar una campaña.
- Test de arquitectura: Infrastructure no filtra el SDK

## 8. Constitution Check

Evaluación previa a la implementación.

| Principio | Decisión | Evidencia |
|---|---|---|
| I | PASS | `FR-027-01` y 6 requisitos más, con criterios de aceptación medibles |
| II | PASS | Cambios acotados a las capas declaradas en §2 |
| III | PASS | DTOs, comandos y consultas separados; sin exponer entidades de dominio |
| IV | PASS | Reglas de seguridad aplicadas en §4 |
| V | PASS | Estrategia de pruebas definida en §7 |
| VI | PASS | Costo estimado declarado en §6 |
| VII | Brecha registrada | Medición real de p95 y reasignación con índice único pendientes. Véanse §§5 y 9 |

> Sin excepciones registradas (Apéndice E de la constitución).

## 9. Riesgos de implementación

- Tokens duplicados entre usuarios
- Brecha existente de `FR-027-06`: la reasignación actual desactiva el documento anterior sin liberar su token, pero MongoDB aplica unicidad también a dispositivos inactivos. La prueba unitaria con un doble de repositorio no acredita este escenario. Corregir con una operación de persistencia segura y validar en MongoDB antes de cerrar el requisito.
- Un adaptador mal configurado deja las notificaciones sin enviar

## 10. Orden de implementación

1. `T027-01` Actualizar y validar el contrato OpenAPI del spec
2. `T027-02` Implementar: Entidad Device e IPushDispatcher
3. `T027-03` Implementar: RegisterDeviceCommand y DispatchNotificationCommand
4. `T027-04` Implementar repositorio de dispositivos y adaptador simulado existente
5. `T027-05` Implementar: Endpoint PUT /api/v1/users/device-token
6. `T027-06` Definir IPushDispatcher y la entidad Device
7. `T027-07` Implementar el adaptador FCM HTTP v1 con credenciales gestionadas en AWS
8. `T027-08` Resolver la reasignación de token entre usuarios
9. `T027-09` Implementar las pruebas de integración del flujo completo
10. `T027-10` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec
11. `T027-11` Registrar evidencias del Constitution Check
12. `T027-12` Registrar en Firebase la app Kotlin con `applicationId` `sensus.com` y verificar el registro y la renovación del token desde Android
13. `T027-14` Tratar el resultado del proveedor en el registro y en el despacho
14. `T027-15` Componer el adaptador y el contrato HTTP 503
15. `T027-16` Verificar fallos del proveedor y la integración HTTP
16. `T027-13` Verificar entrega y apertura del aviso en un dispositivo físico tras completar las pruebas del backend y la configuración Android

### Reasignación de token (FR-027-06)

El índice único de token se mantiene. Cuando un token validado ya está asociado a otro registro, se elimina esa asociación por comparación de identificador y token antes de persistir el nuevo destino. Una colisión concurrente devuelve un conflicto reintentable. Si el segundo paso falla, el dispositivo queda sin asociación con ese token hasta que el cliente reintente. Esto evita que un intento fallido reactive la asociación anterior. Sin recursos nuevos ni costo incremental.
