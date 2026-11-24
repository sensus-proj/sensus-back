# Plan 023 — Entrega de contenido por CDN

| Campo | Valor |
|---|---|
| Spec | [`spec.md`](spec.md) |
| Estado del plan | Implementado |
| Versión | 1.1.0 |
| Constitution Check | PASS |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

## 1. Resumen técnico

Servir multimedia aprobado a través de Cloudinary CDN con optimización automática, control de acceso fail-closed, URLs firmadas HMAC-SHA256 con TTL corto para contenido privado e invalidación de caché ante eventos de purga o cuarentena.

## 2. Cambios por capa

| Capa | Cambio |
|---|---|
| Domain | Modelos y contratos de entrega multimedia; invariantes de estado de moderación (fail-closed: solo se entrega contenido `Approved`). |
| Application | Casos de uso `CreateMediaDeliveryUrlCommand` y `GetMediaDeliveryQuery`, DTOs inmutables `MediaDeliveryUrlDto` y `MediaDeliveryResponse`, interfaces `IMediaDeliveryUrlGenerator`, `IMediaContentProxy`, `ICdnCacheInvalidator`. Integración de invalidación CDN en `PurgeQuarantinedMediaCommandHandler`. |
| Infrastructure | Generador criptográfico `HmacMediaDeliveryUrlGenerator` (HMAC-SHA256, comparación en tiempo constante `CryptographicOperations.FixedTimeEquals`), proxy con mitigación SSRF `CloudinaryMediaContentProxy`, invalidador `CloudinaryCdnCacheInvalidator`. Configuración en `MediaDeliveryOptions`. |
| Api | Endpoints RESTful `POST /api/v1/media/{id}/delivery-url` y `GET /api/v1/media/{id}/delivery`, cabeceras HTTP `Cache-Control` según privacidad (`public, max-age=...` vs `private, no-cache, no-store`). |

## 3. Modelo de datos e índices

- Colección `media_assets` almacena metadatos y estado de moderación.
- No se requieren índices nuevos; las búsquedas por `id` emplean la clave primaria `_id`.

## 4. Seguridad y Modelo de Amenazas (Fintech / OWASP ASVS)

- **Spoofing / Tampering**: Generación y verificación de firma criptográfica HMAC-SHA256 con clave secreta de servidor. Parámetros `expires` e `id` firmados; validación en tiempo constante (`CryptographicOperations.FixedTimeEquals`) para evitar ataques de canal lateral (*timing attacks*).
- **Repudiation**: Solicitudes de URLs firmadas vinculadas al usuario autenticado (o comerciante propietario del recurso).
- **Information Disclosure / Leaks**: Prohibido servir multimedia con estado distinto de `Approved`.
  - *Justificación del Proxy de Streaming vs Redirección 302*:
    Un 302 hacia la URL permanente en Cloudinary filtraría la URL directa de almacenamiento del proveedor y, si el redirect se cachea, permitiría continuar accediendo al recurso aún tras su revocación. Se implementa un proxy de streaming (`CloudinaryMediaContentProxy`) donde el backend actúa como frontera de seguridad fail-closed: verifica la firma en cada invocación, deniega con 404 cualquier activo no aprobado o retirado, y emite cabeceras dinámicas `Cache-Control: public, max-age={remainingSeconds}, s-maxage={remainingSeconds}` acotadas estrictamente a los segundos remanentes de vida de la firma HMAC.
  - *Mitigación SSRF*: El proxy valida de forma estricta que la URL remota pertenezca exclusivamente al host oficial `https://res.cloudinary.com`.
  - *Límites operativos*: Timeout explícito (15 s) y límite de tamaño de respuesta (50 MB) configurados para prevenir agotamiento de recursos.
- **Denial of Service**: Expiración corta (TTL configurable, 900 segundos por defecto, máximo 86400 segundos); streaming asíncrono en bloques sin cargar archivos en memoria.
- **Elevation of Privilege**: Usuarios no autorizados o invitados no pueden generar URLs de entrega de recursos ajenos privados. Contenido en cuarentena nunca se entrega por `/delivery` (responde 404 uniforme; la revisión administrativa usa el flujo propio de Spec 019).
- **Parámetros SSM en Despliegue (M4)**:
  - `/sensus/<env>/MediaDelivery/SigningKey`: Clave criptográfica en SSM Parameter Store tipo *SecureString*, longitud mínima de 32 bytes (256 bits), rotación cada ≤ 90 días. Si falta o es insegura en producción, la aplicación falla cerrado en el arranque.
  - `/sensus/<env>/MediaDelivery/PublicBaseUrl`: URL pública base de la API (ej. `https://api.sensus.com`), requerida en producción para generar URLs absolutas seguras. Prohibido `localhost` en entornos desplegados.

## 5. Observabilidad y rendimiento

- Latencia p95 esperada inferior a 200 ms para recursos cacheados en CDN (`NFR-023-01`).
- Proxy de streaming asíncrono con `ResponseOwnedStream` evitando cargar archivos completos en memoria del servidor.

## 6. Costo AWS

- Nivel gratuito de Cloudinary (25 créditos mensuales).
- Cómputo serverless en AWS Lambda + API Gateway dentro de los límites de invocaciones gratuitas.

## 7. Estrategia de pruebas

- Unitarias para `CreateMediaDeliveryUrlCommandHandler` y `GetMediaDeliveryQueryHandler` (happy path, no encontrado, no aprobado, no autorizado, expirado, firma inválida).
- Unitarias para validadores FluentValidation.
- Integración para `HmacMediaDeliveryUrlGenerator` (determinismo, validación de expiración, manipulación de firma).
- Integración para `CloudinaryMediaContentProxy` (prevención de SSRF, respuesta exitosa, error 404 remoto).
- Integración para `CloudinaryCdnCacheInvalidator` (firma de invalidación y manejo de errores de red).
- Integración HTTP en `MediaDeliveryEndpointsTests` (POST delivery-url y GET delivery).

## 8. Constitution Check

| Principio | Decisión | Evidencia |
|---|---|---|
| I. Especificación y trazabilidad | PASS | `FR-023-01` a `FR-023-04`, `NFR-023-01`, `NFR-023-02` implementados y probados. |
| II. Clean Architecture | PASS | Contratos en Application/Domain, implementaciones de Cloudinary en Infrastructure, endpoints en Api. |
| III. CQRS y contratos | PASS | Comandos y consultas separados, DTOs inmutables, contrato OpenAPI v1.1.0 documentado. |
| IV. Seguridad y privacidad | PASS | Fail-closed (solo `Approved`), HMAC-SHA256 con tiempo constante, allowlist estricta SSRF. |
| V. Verificación automatizada | PASS | Pruebas unitarias e integración en verde con cobertura > 90% en Domain y Application. |
| VI. Costo cero e IaC | PASS | Utilización de Cloudinary Free Tier (25 créditos) y AWS Lambda en CDK. |
| VII. Rendimiento y observabilidad | PASS | Streaming sin sobrecarga de memoria, cabeceras `Cache-Control` diferenciadas por privacidad. |
| VIII. Nivel Fintech | PASS | Firmas criptográficas no reutilizables tras expiración, mitigación SSRF y modelo STRIDE. |

## 9. Riesgos de implementación

- Mitigado: Fuga de recursos privados prevenida mediante URLs firmadas de corta duración y proxy seguro.

## 10. Orden de implementación

1. `T023-01` Implementar: Integración con Cloudinary CDN y configuración de entrega (Completada)
2. `T023-02` Implementar: Generación de URLs firmadas (Completada)
3. `T023-03` Configurar transformaciones y optimización en Cloudinary CDN (Completada)
4. `T023-04` Generar URLs firmadas para contenido privado (Completada)
5. `T023-05` Implementar las pruebas de integración del flujo completo (Completada)
6. `T023-06` Actualizar `specs/README.md`, `requirements.md` y el checklist del spec (Completada)
7. `T023-07` Registrar evidencias del Constitution Check (Completada)
