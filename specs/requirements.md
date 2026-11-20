# Registro de requisitos

Todos los requisitos del proyecto, trazables a su spec. Formato: `FR-<spec>-<nn>` y `NFR-<spec>-<nn>`.

| ID | Spec | Tipo | Requisito | Prioridad / Métrica | Estado |
|---|---|---|---|---|---|
| `FR-001-01` | 001 | FR | Registrar cuenta con correo, nombre completo y contraseña | Must | Implementado |
| `FR-001-02` | 001 | FR | Rechazar correo con formato inválido o ya registrado | Must | Implementado |
| `FR-001-03` | 001 | FR | Exigir contraseña de 8+ caracteres con mayúscula, minúscula y número | Must | Implementado |
| `FR-001-04` | 001 | FR | Almacenar la contraseña con BCrypt y salt, nunca en texto plano | Must | Implementado |
| `FR-001-05` | 001 | FR | Asignar rol Consumidor en un modelo de cuenta unificada | Must | Implementado |
| `FR-001-06` | 001 | FR | Exigir aceptación de términos y registrar la fecha | Must | Implementado |
| `FR-001-07` | 001 | FR | Devolver sesión activa con token JWT de 60 minutos | Must | Implementado |
| `NFR-001-01` | 001 | NFR | Latencia de registro | p95 < 800 ms | Implementado |
| `NFR-001-02` | 001 | NFR | Errores 5xx en registro | < 0.5 % | Implementado |
| `FR-002-01` | 002 | FR | Autenticar con correo y contraseña | Must | Implementado |
| `FR-002-02` | 002 | FR | Emitir JWT con sub, email, name, roles, jti y exp de 60 minutos | Must | Implementado |
| `FR-002-03` | 002 | FR | Responder 401 con mensaje genérico ante credenciales inválidas | Must | Implementado |
| `FR-002-04` | 002 | FR | Actualizar la fecha de último acceso | Should | Implementado |
| `FR-002-05` | 002 | FR | Rechazar cuentas suspendidas o eliminadas | Must | Implementado |
| `NFR-002-01` | 002 | NFR | Latencia de login | p95 < 600 ms | Implementado |
| `NFR-002-02` | 002 | NFR | Vigencia del token | 60 minutos, configurable por entorno | Implementado |
| `FR-002-06` | 002 | FR | Consultar la identidad de la sesión actual (`GET /auth/me`) desde los claims del JWT | Must | Implementado |
| `FR-002-07` | 002 | FR | Responder `/auth/me` con un DTO explícito y prueba de mapeo | Should | Pendiente |
| `FR-003-01` | 003 | FR | Devolver el perfil del usuario autenticado | Must | Implementado |
| `FR-003-02` | 003 | FR | Excluir passwordHash y datos sensibles de la respuesta | Must | Implementado |
| `FR-003-03` | 003 | FR | Responder 401 sin token o con token inválido | Must | Implementado |
| `FR-003-04` | 003 | FR | Actualizar nombre completo y avatar del propio usuario | Must | Implementado |
| `FR-003-05` | 003 | FR | Rechazar cambios de correo o roles desde este endpoint | Must | Implementado |
| `FR-003-06` | 003 | FR | Aceptar solo avatares subidos por el propio usuario y aprobados | Must | Parcial (sin moderación ni validación estricta de la URL; T003-09 y T003-10) |
| `NFR-003-01` | 003 | NFR | Latencia de consulta | p95 < 300 ms | Implementado |
| `NFR-003-02` | 003 | NFR | Latencia de actualización | p95 < 500 ms | Implementado |
| `FR-003-07` | 003 | FR | Emitir firma de subida a Cloudinary limitada a `sensus/profiles/{userId}` | Must | Implementado |
| `FR-003-08` | 003 | FR | Moderar la foto de perfil antes de publicarla o guardarla | Must | Pendiente |
| `FR-003-09` | 003 | FR | Límite de tasa en la emisión de firmas | Should | Pendiente |
| `FR-004-01` | 004 | FR | Exponer POST /api/v1/auth/logout autenticado | Must | Implementado |
| `FR-004-02` | 004 | FR | Registrar el jti revocado con TTL igual a la expiración del token | Must | Parcial |
| `FR-004-03` | 004 | FR | Rechazar cualquier petición con token revocado | Must | Implementado |
| `FR-004-04` | 004 | FR | Responder 204 aunque el token ya estuviera revocado | Should | Implementado |
| `FR-004-05` | 004 | FR | Desactivar el registro push del dispositivo que se identifica al cerrar sesión | Must | Pendiente de verificación |
| `NFR-004-01` | 004 | NFR | Efectividad de la revocación | < 1 s desde el logout | Pendiente |
| `NFR-004-02` | 004 | NFR | Sobrecarga por petición | < 50 ms | Pendiente |
| `FR-005-01` | 005 | FR | Activar el rol comercial sobre la cuenta autenticada | Must | Implementado |
| `FR-005-02` | 005 | FR | Registrar nombre, categoría, dirección y coordenadas del local | Must | Implementado |
| `FR-005-03` | 005 | FR | Permitir un solo comercio activo por usuario | Must | Implementado |
| `FR-005-04` | 005 | FR | Asignar el rol Merchant tras la activación | Must | Implementado |
| `FR-005-05` | 005 | FR | Validar que la categoría pertenezca al catálogo | Must | Implementado |
| `FR-005-06` | 005 | FR | Validar rangos de latitud y longitud y calcular el punto GeoJSON | Must | Implementado |
| `FR-005-07` | 005 | FR | Rechazar la activación de cuentas suspendidas | Must | Implementado |
| `NFR-005-01` | 005 | NFR | Latencia de activación | p95 < 800 ms | Implementado |
| `NFR-005-02` | 005 | NFR | Precisión de coordenadas | 6 decimales, error < 1 m | Implementado |
| `FR-006-01` | 006 | FR | Devolver el comercio asociado al usuario autenticado | Must | Implementado |
| `FR-006-02` | 006 | FR | Responder 404 si el usuario no tiene comercio | Must | Implementado |
| `FR-006-03` | 006 | FR | Impedir la consulta de comercios de otros usuarios | Must | Implementado |
| `FR-006-04` | 006 | FR | Incluir estado del comercio y coordenadas normalizadas | Should | Implementado |
| `NFR-006-01` | 006 | NFR | Latencia de consulta | p95 < 300 ms | Implementado |
| `FR-007-01` | 007 | FR | Listar ofertas publicadas dentro de un radio de la ubicación enviada | Must | Pendiente |
| `FR-007-02` | 007 | FR | Excluir ofertas vencidas, pausadas, archivadas o sin stock | Must | Pendiente |
| `FR-007-03` | 007 | FR | Filtrar por categoría del catálogo | Should | Implementado |
| `FR-007-04` | 007 | FR | Aplicar pageSize 20 por defecto y 100 máximo | Must | Pendiente |
| `FR-007-05` | 007 | FR | Ordenar por distancia ascendente y desempatar por fecha de publicación | Must | Pendiente |
| `FR-007-06` | 007 | FR | Limitar el radio a 50 km y rechazar valores fuera de rango | Must | Pendiente |
| `FR-007-07` | 007 | FR | Devolver la distancia en metros junto a cada oferta | Should | Pendiente |
| `FR-007-08` | 007 | FR | Aceptar un radio de 1 a 5 km para el usuario registrado y rechazar valores fuera de ese rango | Must | Implementado |
| `FR-007-09` | 007 | FR | Aplicar un radio máximo estricto de 2 km para el usuario invitado | Must | Implementado |
| `NFR-007-01` | 007 | NFR | Latencia del listado | p95 < 500 ms | Pendiente |
| `NFR-007-02` | 007 | NFR | Consulta geoespacial | Sin recorrido completo de colección (índice 2dsphere) | Pendiente |
| `FR-008-01` | 008 | FR | Eliminar el grupo de rutas /api/v1/comercio | Must | Implementado |
| `FR-008-02` | 008 | FR | Eliminar el alias /api/v1/comercios | Must | Implementado |
| `FR-008-03` | 008 | FR | Devolver 404 en las rutas retiradas | Must | Implementado |
| `FR-008-04` | 008 | FR | Actualizar README, Sensus.Api.http y OpenAPI | Must | Implementado |
| `FR-008-05` | 008 | FR | No dejar referencias a rutas en español en el código | Must | Implementado |
| `NFR-008-01` | 008 | NFR | Rutas duplicadas | Cero en el contrato OpenAPI | Implementado |
| `NFR-008-02` | 008 | NFR | Cobertura del contrato | 100 % de rutas bajo /api/v1 en inglés | Implementado |
| `FR-009-01` | 009 | FR | Toda respuesta de error usa ProblemDetails | Must | Pendiente |
| `FR-009-02` | 009 | FR | Incluir traceId o correlationId en cada error | Must | Pendiente |
| `FR-009-03` | 009 | FR | No exponer stack traces ni nombres de colecciones | Must | Pendiente |
| `FR-009-04` | 009 | FR | Mantener un catálogo de códigos de error documentado | Should | Pendiente |
| `NFR-009-01` | 009 | NFR | Estabilidad del formato | Sin cambios incompatibles dentro de v1 | Pendiente |
| `FR-010-01` | 010 | FR | Exponer PagedResult con items, page, pageSize, total y totalPages | Must | Pendiente |
| `FR-010-02` | 010 | FR | Aplicar 20 elementos por defecto y 100 como máximo | Must | Pendiente |
| `FR-010-03` | 010 | FR | Rechazar pageSize fuera de rango con 400 | Must | Pendiente |
| `FR-010-04` | 010 | FR | Aplicar el contrato a todo endpoint de colección | Must | Pendiente |
| `NFR-010-01` | 010 | NFR | Sobrecarga de contar total | p95 < 100 ms adicionales | Pendiente |
| `FR-011-01` | 011 | FR | Exponer el documento OpenAPI en desarrollo | Must | Pendiente |
| `FR-011-02` | 011 | FR | Documentar todos los endpoints con esquemas y códigos de respuesta | Must | Pendiente |
| `FR-011-03` | 011 | FR | Validar el documento en CI y fallar ante cambios incompatibles | Must | Pendiente |
| `FR-011-04` | 011 | FR | Versionar el contrato junto con la API | Should | Pendiente |
| `NFR-011-01` | 011 | NFR | Cobertura del contrato | 100 % de endpoints publicados | Pendiente |
| `FR-012-01` | 012 | FR | Definir orígenes permitidos por configuración y entorno | Must | Implementado |
| `FR-012-02` | 012 | FR | Prohibir AllowAnyOrigin fuera de desarrollo | Must | Implementado |
| `FR-012-03` | 012 | FR | Redirigir a HTTPS fuera de desarrollo | Must | Implementado |
| `FR-012-04` | 012 | FR | Habilitar HSTS en producción | Should | Implementado |
| `NFR-012-01` | 012 | NFR | Orígenes permitidos | Lista explícita, cero comodines en producción | Pendiente |
| `FR-013-01` | 013 | FR | Limitar peticiones por IP | Must | Implementado |
| `FR-013-02` | 013 | FR | Limitar peticiones por usuario autenticado | Must | Implementado |
| `FR-013-03` | 013 | FR | Responder 429 con Retry-After | Must | Implementado |
| `FR-013-04` | 013 | FR | Configurar umbrales por entorno sin recompilar | Must | Implementado |
| `FR-013-05` | 013 | FR | Aplicar límites más estrictos en autenticación | Should | Implementado |
| `NFR-013-01` | 013 | NFR | Sobrecarga del limitador | < 10 ms por petición | Pendiente |
| `NFR-013-02` | 013 | NFR | Disponibilidad ante picos | Sin degradar a 5xx | Pendiente |
| `FR-014-01` | 014 | FR | Emitir logs en JSON estructurado | Must | Pendiente |
| `FR-014-02` | 014 | FR | Aceptar o generar correlation-id por petición | Must | Implementado |
| `FR-014-03` | 014 | FR | Excluir PII, tokens y contenido moderado | Must | Pendiente |
| `FR-014-04` | 014 | FR | Registrar método, ruta, estado y duración | Should | Pendiente |
| `NFR-014-01` | 014 | NFR | Campos obligatorios | timestamp, level, correlationId, message | Pendiente |
| `NFR-014-02` | 014 | NFR | Sobrecarga | < 5 ms | Pendiente |
| `FR-015-01` | 015 | FR | Publicar métricas de latencia, errores y throttling | Must | Implementado |
| `FR-015-02` | 015 | FR | Definir alarmas en IaC con umbrales explícitos | Must | Implementado |
| `FR-015-03` | 015 | FR | Notificar alarmas a un canal definido | Should | Implementado |
| `NFR-015-01` | 015 | NFR | Coste de alarmas | Dentro del Free Tier (10 alarmas) | Implementado |
| `NFR-015-02` | 015 | NFR | Latencia de detección | < 5 min | Pendiente |
| `FR-016-01` | 016 | FR | Exponer /health/live sin dependencias externas | Must | Implementado |
| `FR-016-02` | 016 | FR | Exponer /health/ready verificando MongoDB | Must | Implementado |
| `FR-016-03` | 016 | FR | No exponer cadenas de conexión ni versiones | Must | Implementado |
| `NFR-016-01` | 016 | NFR | Latencia de readiness | < 500 ms | Pendiente |
| `FR-017-01` | 017 | FR | Enviar imagen y texto en la misma solicitud de moderación | Must | Implementado |
| `FR-017-02` | 017 | FR | Consumir `gpt-4o-mini` en modo visión con la política de Sensus y tiempo de espera acotado (costo en EX-002) | Must | Implementado |
| `FR-017-03` | 017 | FR | Interpretar `decision`, `flagged_categories` y `category_scores` del JSON y tratar como fallo la respuesta malformada | Must | Implementado |
| `FR-017-04` | 017 | FR | No publicar si el proveedor falla, expira o responde de forma inválida | Must | Implementado |
| `FR-017-05` | 017 | FR | Registrar veredicto, categorías, puntajes y acción adoptada | Must | Parcial |
| `FR-017-06` | 017 | FR | Moderar toda imagen de oferta antes de exponerla públicamente | Must | Parcial |
| `FR-017-07` | 017 | FR | Exponer el resultado al flujo que lo invoca, sin endpoint público de moderación | Must | Pendiente |
| `NFR-017-01` | 017 | NFR | Latencia de moderación | p95 < 3 s | Pendiente |
| `NFR-017-02` | 017 | NFR | Disponibilidad del flujo | Fail-closed al 100 % | Pendiente |
| `FR-018-01` | 018 | FR | Definir banda baja, intermedia y alta por categoría | Must | Implementado |
| `FR-018-02` | 018 | FR | Externalizar los umbrales en configuración | Must | Implementado |
| `FR-018-03` | 018 | FR | Registrar qué umbral se aplicó en cada decisión | Must | Implementado |
| `FR-018-04` | 018 | FR | Permitir ajustar umbrales sin recompilar | Should | Implementado |
| `NFR-018-01` | 018 | NFR | Trazabilidad | 100 % de decisiones con umbral registrado | Implementado |
| `FR-019-01` | 019 | FR | Marcar el contenido no aprobado como PendienteRevision | Must | Implementado |
| `FR-019-02` | 019 | FR | Impedir el acceso público al contenido en cuarentena | Must | Implementado |
| `FR-019-03` | 019 | FR | Definir y aplicar retención máxima | Must | Implementado |
| `FR-019-04` | 019 | FR | Permitir la liberación del contenido tras aprobación | Should | Implementado |
| `NFR-019-01` | 019 | NFR | Retención | Máximo definido en la spec (propuesta: 30 días) | Implementado |
| `NFR-019-02` | 019 | NFR | Coste de almacenamiento | Dentro del Free Tier de Cloudinary (25 créditos) | Pendiente |
| `FR-020-01` | 020 | FR | Aplicar baneo cuando la banda sea alta | Must | Implementado |
| `FR-020-02` | 020 | FR | Permitir revertir una sanción | Must | Implementado |
| `FR-020-03` | 020 | FR | Registrar motivo, evidencia y responsable | Must | Implementado |
| `FR-020-04` | 020 | FR | Bloquear el acceso del usuario sancionado | Must | Implementado |
| `NFR-020-01` | 020 | NFR | Efectividad del bloqueo | < 1 s | Implementado |
| `NFR-020-02` | 020 | NFR | Reversibilidad | Sin pérdida de datos del usuario | Implementado |
| `FR-020-05` | 020 | FR | Consultar sanciones con filtros y paginación (administrador) | Must | Implementado |
| `FR-020-06` | 020 | FR | Escalar la duración de la sanción por reincidencia hasta el baneo permanente | Should | Implementado |
| `FR-020-07` | 020 | FR | Aplicar una sanción manual temporal o permanente (administrador) | Should | Implementado |
| `NFR-020-03` | 020 | NFR | Privacidad y observabilidad | 0 PII en logs; métricas de sanciones aplicadas y revertidas | Implementado |
| `NFR-020-04` | 020 | NFR | Rendimiento de consulta | Paginación 20/100 sobre índices declarados | Implementado |
| `FR-020-08` | 020 | FR | Notificar por push al usuario al aplicar y al revertir una sanción | Must | Implementado |
| `NFR-020-05` | 020 | NFR | Seguridad de nivel fintech (Principio VIII) | Sin enumeración de cuentas; autorización en borde y caso de uso | Implementado |
| `FR-021-01` | 021 | FR | Registrar cada decisión con veredicto, puntajes y umbral | Must | Pendiente |
| `FR-021-02` | 021 | FR | Impedir la modificación de registros de auditoría | Must | Pendiente |
| `FR-021-03` | 021 | FR | Permitir consulta paginada por administrador | Must | Pendiente |
| `FR-021-04` | 021 | FR | Definir retención conforme a la política de datos | Must | Pendiente |
| `NFR-021-01` | 021 | NFR | Integridad | Registros sin actualización ni borrado manual | Pendiente |
| `NFR-021-02` | 021 | NFR | Latencia de consulta | p95 < 800 ms | Pendiente |
| `FR-022-01` | 022 | FR | Aceptar una imagen multipart y devolver su identificador | Must | Implementado |
| `FR-022-02` | 022 | FR | Validar tipo MIME real (JPEG, PNG, WebP) por firma de bytes | Must | Implementado |
| `FR-022-03` | 022 | FR | Rechazar archivos mayores a 4,5 MB con 413 | Must | Implementado |
| `FR-022-04` | 022 | FR | Almacenar el archivo de forma privada hasta su aprobación | Must | Implementado |
| `FR-022-05` | 022 | FR | Registrar propietario, estado y public_id de Cloudinary | Must | Implementado |
| `FR-022-06` | 022 | FR | Moderar la imagen antes de permitir su uso público | Must | Implementado |
| `FR-022-07` | 022 | FR | Limitar la carga a 20 archivos por usuario y día | Should | Implementado |
| `NFR-022-01` | 022 | NFR | Tamaño máximo por archivo | 4,5 MB | Implementado |
| `NFR-022-02` | 022 | NFR | Latencia de carga | p95 < 2 s para 1 MB | Implementado |
| `NFR-022-03` | 022 | NFR | Coste de almacenamiento | Dentro del Free Tier de Cloudinary (25 créditos) | Implementado |
| `FR-023-01` | 023 | FR | Servir multimedia mediante Cloudinary CDN | Must | Pendiente |
| `FR-023-02` | 023 | FR | Firmar URLs cuando el contenido sea privado | Must | Pendiente |
| `FR-023-03` | 023 | FR | Aplicar cabeceras de caché por tipo de contenido | Should | Pendiente |
| `FR-023-04` | 023 | FR | Invalidar caché al retirar contenido | Should | Pendiente |
| `NFR-023-01` | 023 | NFR | Latencia de entrega | p95 < 200 ms en caché | Pendiente |
| `NFR-023-02` | 023 | NFR | Coste | Dentro del plan gratuito de Cloudinary (25 créditos) | Pendiente |
| `FR-024-01` | 024 | FR | Crear publicación con texto de 1 a 500 caracteres e imagen opcional | Must | Pendiente |
| `FR-024-02` | 024 | FR | No publicar hasta obtener veredicto favorable | Must | Pendiente |
| `FR-024-03` | 024 | FR | Registrar autor, fechas y estado | Must | Pendiente |
| `FR-024-04` | 024 | FR | Devolver 202 mientras la moderación está pendiente | Should | Pendiente |
| `FR-024-05` | 024 | FR | Permitir al autor eliminar su propia publicación | Must | Pendiente |
| `FR-024-06` | 024 | FR | Permitir a un administrador eliminar cualquier publicación | Should | Pendiente |
| `FR-024-07` | 024 | FR | Excluir del feed las publicaciones eliminadas de inmediato | Must | Pendiente |
| `NFR-024-01` | 024 | NFR | Latencia percibida | p95 < 1 s hasta aceptar la solicitud | Pendiente |
| `NFR-024-02` | 024 | NFR | Consistencia | Sin publicaciones sin veredicto | Pendiente |
| `FR-025-01` | 025 | FR | Crear comentario en una publicación existente y visible | Must | Pendiente |
| `FR-025-02` | 025 | FR | Moderar el texto antes de publicarlo | Must | Pendiente |
| `FR-025-03` | 025 | FR | Listar comentarios paginados por publicación | Must | Pendiente |
| `FR-025-04` | 025 | FR | Rechazar comentarios en publicaciones no visibles con 404 | Must | Pendiente |
| `FR-025-05` | 025 | FR | Limitar el comentario a 300 caracteres | Must | Pendiente |
| `FR-025-06` | 025 | FR | Ordenar los comentarios por fecha ascendente | Should | Pendiente |
| `NFR-025-01` | 025 | NFR | Latencia de listado | p95 < 400 ms | Pendiente |
| `NFR-025-02` | 025 | NFR | Paginación | 20 por defecto, 100 máximo | Pendiente |
| `FR-026-01` | 026 | FR | Listar publicaciones publicadas dentro del radio, en orden cronológico inverso | Must | Pendiente |
| `FR-026-02` | 026 | FR | Excluir contenido en cuarentena, rechazado o eliminado | Must | Pendiente |
| `FR-026-03` | 026 | FR | Aplicar paginación estándar | Must | Pendiente |
| `FR-026-04` | 026 | FR | Limitar el radio a 50 km y rechazar valores fuera de rango | Must | Pendiente |
| `FR-026-05` | 026 | FR | Excluir publicaciones sin ubicación del feed cercano | Must | Pendiente |
| `FR-026-06` | 026 | FR | Usar caché de corta duración para páginas recurrentes | Should | Pendiente |
| `NFR-026-01` | 026 | NFR | Latencia del feed | p95 < 300 ms con caché | Pendiente |
| `NFR-026-02` | 026 | NFR | Índice | Compuesto por status + location + createdAt | Pendiente |
| `FR-027-01` | 027 | FR | Registrar token de dispositivo con plataforma y deviceId | Must | Implementado |
| `FR-027-02` | 027 | FR | Actualizar el token existente sin duplicar el dispositivo | Must | Implementado |
| `FR-027-03` | 027 | FR | Despachar notificaciones mediante IPushDispatcher | Must | Parcial |
| `FR-027-04` | 027 | FR | Configurar FCM HTTP v1 como proveedor de producción para Android | Must | Pendiente |
| `FR-027-05` | 027 | FR | Desactivar tokens rechazados definitivamente por el proveedor | Must | Pendiente |
| `FR-027-06` | 027 | FR | Reasignar un token que cambia de usuario sin dejar acceso residual | Must | Implementado |
| `FR-027-07` | 027 | FR | No fallar si el usuario no tiene dispositivos activos | Should | Implementado |
| `NFR-027-01` | 027 | NFR | Latencia de registro | p95 < 300 ms | Implementado |
| `NFR-027-02` | 027 | NFR | Latencia de despacho | p95 < 2 s | Implementado |
| `NFR-027-03` | 027 | NFR | Acoplamiento al proveedor | Cero referencias al SDK fuera de Infrastructure | Implementado |
| `FR-028-01` | 028 | FR | Exponer un endpoint de recepción de eventos | Must | Backlog |
| `FR-028-02` | 028 | FR | Verificar la firma del emisor | Must | Backlog |
| `FR-028-03` | 028 | FR | Garantizar idempotencia por eventId | Must | Backlog |
| `FR-028-04` | 028 | FR | Responder 2xx en menos de 1 s y procesar de forma asíncrona | Must | Backlog |
| `FR-028-05` | 028 | FR | Registrar eventos fallidos para reintento | Should | Backlog |
| `NFR-028-01` | 028 | NFR | Latencia de respuesta | p95 < 500 ms | Backlog |
| `NFR-028-02` | 028 | NFR | Duplicados procesados | Cero efectos duplicados | Backlog |
| `FR-029-01` | 029 | FR | Exponer POST /api/v1/auth/recover y responder 202 siempre | Must | Pendiente |
| `FR-029-02` | 029 | FR | Generar token de 32 bytes, almacenar solo su hash y caducarlo en 30 minutos | Must | Parcial |
| `FR-029-03` | 029 | FR | Invalidar tokens anteriores del mismo usuario al emitir uno nuevo | Must | Pendiente |
| `FR-029-04` | 029 | FR | Enviar el enlace por correo mediante Amazon SES | Must | Pendiente |
| `FR-029-05` | 029 | FR | Exponer POST /api/v1/auth/recover/confirm para fijar la nueva contraseña | Must | Implementado |
| `FR-029-06` | 029 | FR | Rechazar tokens expirados, ya usados o inexistentes con 400 | Must | Pendiente |
| `FR-029-07` | 029 | FR | Revocar todas las sesiones activas tras el cambio de contraseña | Must | Pendiente |
| `NFR-029-01` | 029 | NFR | Latencia de solicitud | p95 < 700 ms | Pendiente |
| `NFR-029-02` | 029 | NFR | Enumeración de cuentas | Cero diferencia de respuesta entre correo existente e inexistente | Pendiente |
| `NFR-029-03` | 029 | NFR | Límite de solicitudes | 3 por correo y hora | Pendiente |
| `FR-029-08` | 029 | FR | Alias `POST /auth/reset-password` de `/auth/recover/confirm` | Must | Implementado |
| `FR-029-09` | 029 | FR | Retirar el alias cuando el móvil use la ruta canónica | Should | Pendiente |
| `FR-030-01` | 030 | FR | Exponer DELETE /api/v1/users/account con confirmación explícita y contraseña | Must | Implementado |
| `FR-030-02` | 030 | FR | Marcar la cuenta como deleted y revocar todas sus sesiones de inmediato | Must | Parcial |
| `FR-030-03` | 030 | FR | Impedir el login y cualquier operación autenticada tras la eliminación | Must | Implementado |
| `FR-030-04` | 030 | FR | Purgar los datos personales a los 30 días mediante proceso programado | Must | Parcial |
| `FR-030-05` | 030 | FR | Anonimizar publicaciones y comentarios del usuario en lugar de conservarlos asociados | Must | Pendiente |
| `FR-030-06` | 030 | FR | Desactivar el comercio y sus ofertas activas al eliminar una cuenta comercial | Must | Implementado |
| `FR-030-07` | 030 | FR | Conservar la auditoría de moderación anonimizada durante 90 días | Must | Pendiente |
| `FR-030-08` | 030 | FR | Borrar de inmediato intereses (054) e interacciones seudonimizadas (055) al eliminar la cuenta | Must | Implementado |
| `NFR-030-01` | 030 | NFR | Efectividad del bloqueo | < 1 s desde la eliminación | Pendiente |
| `NFR-030-02` | 030 | NFR | Purga | 100 % de cuentas marcadas, en ≤ 31 días | Pendiente |
| `NFR-030-03` | 030 | NFR | Trazabilidad | Toda eliminación registrada sin PII | Pendiente |
| `FR-031-01` | 031 | FR | Actualizar los datos editables del comercio propio | Must | Pendiente |
| `FR-031-02` | 031 | FR | Recalcular location y geohash cuando cambien las coordenadas | Must | Pendiente |
| `FR-031-03` | 031 | FR | Validar categoría del catálogo y rangos de coordenadas | Must | Implementado |
| `FR-031-04` | 031 | FR | Rechazar la edición sobre comercios ajenos con 403 | Must | Pendiente |
| `FR-031-05` | 031 | FR | Registrar quién cambió qué y cuándo | Should | Pendiente |
| `FR-031-06` | 031 | FR | Mantener las ofertas publicadas sin cambios de identificador | Must | Pendiente |
| `NFR-031-01` | 031 | NFR | Latencia de actualización | p95 < 600 ms | Pendiente |
| `NFR-031-02` | 031 | NFR | Consistencia espacial | 100 % de comercios con location válida | Pendiente |
| `FR-032-01` | 032 | FR | Devolver el detalle de una oferta visible con comercio, vigencia, stock y precio | Must | Implementado |
| `FR-032-02` | 032 | FR | Responder 404 si la oferta no existe o no es visible | Must | Implementado |
| `FR-032-03` | 032 | FR | Registrar una vista por usuario, oferta y día | Must | Implementado |
| `FR-032-04` | 032 | FR | Responder 204 a la vista sin revelar datos internos | Must | Implementado |
| `FR-032-05` | 032 | FR | Indicar si el usuario ya tiene un apartado activo sobre la oferta | Should | Implementado |
| `FR-032-06` | 032 | FR | No permitir vistas sobre ofertas archivadas | Must | Implementado |
| `NFR-032-01` | 032 | NFR | Latencia del detalle | p95 < 350 ms | Implementado |
| `NFR-032-02` | 032 | NFR | Sobrecarga de la vista | < 100 ms y sin bloquear la respuesta | Implementado |
| `FR-033-01` | 033 | FR | Crear un apartado descontando stock de forma atómica | Must | Implementado |
| `FR-033-02` | 033 | FR | Rechazar el apartado si no hay stock o la oferta no es visible | Must | Implementado |
| `FR-033-03` | 033 | FR | Permitir un solo apartado activo por usuario y oferta | Must | Implementado |
| `FR-033-04` | 033 | FR | Generar un código firmado con claimId, offerId y expiración, y su imagen QR | Must | Implementado |
| `FR-033-05` | 033 | FR | Caducar el apartado a las 24 horas y devolver el stock | Must | Implementado |
| `FR-033-06` | 033 | FR | Exponer POST /api/v1/offers/claims/{code}/redeem para el comerciante propietario | Must | Implementado |
| `FR-033-07` | 033 | FR | Marcar el canje como usado y rechazar un segundo canje del mismo código | Must | Implementado |
| `FR-033-08` | 033 | FR | Impedir que un comerciante canjee códigos de otro comercio | Must | Implementado |
| `FR-033-09` | 033 | FR | Avisar al comercio y comprador al confirmar un apartado | Must | Parcial |
| `FR-033-10` | 033 | FR | Entregar vencimiento absoluto al comprador en el aviso de apartado | Must | Parcial |
| `FR-033-11` | 033 | FR | Recuperar el apartado autenticado desde un aviso push | Must | Implementado |
| `NFR-033-01` | 033 | NFR | Latencia del apartado | p95 < 700 ms | Implementado |
| `NFR-033-02` | 033 | NFR | Consistencia de stock | Cero unidades negativas bajo concurrencia | Implementado |
| `NFR-033-03` | 033 | NFR | Liberación de stock | ≤ 5 min tras la expiración | Implementado |
| `FR-033-12` | 033 | FR | Restringir `claims/expire-check` a administrador, con límite de tasa | Must | Implementado |
| `FR-034-01` | 034 | FR | Crear una oferta asociada al comercio del token | Must | Implementado |
| `FR-034-02` | 034 | FR | Validar precio, descuento entre 1 y 90 %, stock ≥ 1 y vigencia coherente | Must | Implementado |
| `FR-034-03` | 034 | FR | Moderar texto e imágenes antes de publicar | Must | Parcial |
| `FR-034-04` | 034 | FR | Permitir guardar como Draft sin moderación y sin visibilidad pública | Must | Pendiente |
| `FR-034-05` | 034 | FR | Limitar a 20 ofertas activas por comercio | Should | Pendiente |
| `FR-034-06` | 034 | FR | Calcular y persistir el precio final a partir del descuento | Must | Implementado |
| `FR-034-07` | 034 | FR | Devolver 202 cuando la moderación quede pendiente | Should | Pendiente |
| `FR-034-08` | 034 | FR | Exigir para publicar una categoría del catálogo de producto `OfferCategory` (`Grocery`, `CleaningHome`, `Perishables`, `Dairy`, `Food`, `Bakery`, `Beverages`, `Pets` y `Pharmacy`); un valor ausente o fuera del catálogo responde `400`. Un borrador (`FR-034-04`) puede guardarse sin categoría | Must | Implementado (`OfferCategory:RequireStrictValidation=false` por defecto hasta publicar móvil T15.15) |
| `FR-034-09` | 034 | FR | Normalizar una sola vez las ofertas existentes: los nombres en español que enviaba el móvil se convierten al código (Abarrotes → `Grocery`, Limpieza y Hogar → `CleaningHome`, Perecederos → `Perishables`, Lácteos → `Dairy`, Comida → `Food`, Panadería → `Bakery`, Bebidas → `Beverages`, Mascotas → `Pets`, Farmacia → `Pharmacy`); los valores no reconocibles quedan nulos y se cuentan en la evidencia de la migración | Must | Implementado |
| `NFR-034-01` | 034 | NFR | Latencia de creación | p95 < 1 s sin moderación | Pendiente |
| `NFR-034-02` | 034 | NFR | Publicaciones sin veredicto | Cero | Pendiente |
| `FR-035-01` | 035 | FR | Actualizar los campos editables de una oferta propia | Must | Implementado |
| `FR-035-02` | 035 | FR | Rechazar la edición de ofertas de otro comercio con 403 | Must | Implementado |
| `FR-035-03` | 035 | FR | Re-moderar cuando cambie el texto o las imágenes | Must | Pendiente |
| `FR-035-04` | 035 | FR | Impedir reducir el stock por debajo de los apartados activos | Must | Implementado |
| `FR-035-05` | 035 | FR | Rechazar ediciones concurrentes con 409 usando version | Must | Implementado |
| `FR-035-06` | 035 | FR | No cambiar el identificador ni los apartados existentes | Must | Implementado |
| `FR-035-07` | 035 | FR | Mantener la oferta oculta mientras la re-moderación está pendiente | Must | Pendiente |
| `FR-035-08` | 035 | FR | Rechazar con `400` una edición que quite la categoría de una oferta publicada o use un valor fuera de `OfferCategory` (spec 034) | Must | Implementado (`OfferCategory:RequireStrictValidation=false` por defecto hasta publicar móvil T15.15) |
| `NFR-035-01` | 035 | NFR | Latencia de edición | p95 < 700 ms | Pendiente |
| `NFR-035-02` | 035 | NFR | Pérdida de cambios | Cero sobrescrituras silenciosas | Pendiente |
| `FR-036-01` | 036 | FR | Cambiar el estado de una oferta propia entre Published, Paused y Archived | Must | Implementado |
| `FR-036-02` | 036 | FR | Rechazar transiciones no permitidas con 409 | Must | Implementado |
| `FR-036-03` | 036 | FR | Ocultar de inmediato las ofertas pausadas o archivadas | Must | Implementado |
| `FR-036-04` | 036 | FR | Impedir archivar una oferta con apartados activos | Must | Implementado |
| `FR-036-05` | 036 | FR | Publicar solo ofertas con moderación aprobada y stock disponible | Must | Parcial |
| `FR-036-06` | 036 | FR | Registrar estado anterior, nuevo estado, actor y fecha | Must | Parcial |
| `NFR-036-01` | 036 | NFR | Efectividad del cambio | < 2 s en la consulta de cercanía | Pendiente |
| `NFR-036-02` | 036 | NFR | Auditoría | 100 % de transiciones registradas | Pendiente |
| `FR-036-07` | 036 | FR | Republicar oferta expirada o archivada con nuevas fechas y stock | Must | Implementado |
| `FR-036-08` | 036 | FR | Ejecutar la expiración de ofertas vencidas y devolver el conteo | Must | Implementado |
| `FR-036-09` | 036 | FR | Restringir `expire-check` a administrador, con límite de tasa | Must | Implementado |
| `FR-036-10` | 036 | FR | Límite de tasa en `republish` | Should | Pendiente |
| `FR-037-01` | 037 | FR | Listar las ofertas del comercio autenticado, incluidas las inactivas | Must | Implementado |
| `FR-037-02` | 037 | FR | Filtrar por estado de la oferta | Should | Implementado |
| `FR-037-03` | 037 | FR | Incluir apartados activos, canjes y vistas por oferta | Must | Pendiente |
| `FR-037-04` | 037 | FR | Aplicar pageSize 20 por defecto y 100 máximo | Must | Pendiente |
| `FR-037-05` | 037 | FR | Ordenar por fecha de creación descendente | Must | Implementado |
| `FR-037-06` | 037 | FR | Impedir ver ofertas de otros comercios | Must | Implementado |
| `NFR-037-01` | 037 | NFR | Latencia del listado | p95 < 600 ms | Pendiente |
| `NFR-037-02` | 037 | NFR | Costo del agregado | Sin recorrido completo por oferta | Pendiente |
| `FR-038-01` | 038 | FR | Alternar el favorito del usuario autenticado sobre una oferta visible | Must | Pendiente |
| `FR-038-02` | 038 | FR | Devolver el estado resultante y el conteo | Must | Pendiente |
| `FR-038-03` | 038 | FR | Rechazar ofertas no visibles con 404 | Must | Pendiente |
| `FR-038-04` | 038 | FR | Garantizar un solo favorito por usuario y oferta | Must | Pendiente |
| `FR-038-05` | 038 | FR | Exponer la afinidad de categoría al ranking de recomendaciones | Should | Pendiente |
| `FR-038-06` | 038 | FR | Limitar la operación a 120 alternancias por usuario y hora | Should | Pendiente |
| `NFR-038-01` | 038 | NFR | Latencia de la operación | p95 < 300 ms | Pendiente |
| `NFR-038-02` | 038 | NFR | Consistencia | Cero conteos negativos | Pendiente |
| `FR-039-01` | 039 | FR | Alternar el me gusta del usuario autenticado sobre una publicación | Must | Pendiente |
| `FR-039-02` | 039 | FR | Devolver el estado resultante y el conteo actualizado | Must | Pendiente |
| `FR-039-03` | 039 | FR | Rechazar publicaciones no visibles con 404 | Must | Pendiente |
| `FR-039-04` | 039 | FR | Garantizar un solo registro por usuario y publicación | Must | Pendiente |
| `FR-039-05` | 039 | FR | Mantener el conteo consistente ante peticiones repetidas | Must | Pendiente |
| `FR-039-06` | 039 | FR | Limitar la operación a 60 alternancias por usuario y hora | Should | Pendiente |
| `NFR-039-01` | 039 | NFR | Latencia de la operación | p95 < 300 ms | Pendiente |
| `NFR-039-02` | 039 | NFR | Consistencia del conteo | Cero conteos negativos | Pendiente |
| `FR-040-01` | 040 | FR | Crear un reporte sobre una oferta, publicación o comentario existente | Must | Pendiente |
| `FR-040-02` | 040 | FR | Aceptar solo motivos tipificados: spam, ofensivo, fraude u otro | Must | Pendiente |
| `FR-040-03` | 040 | FR | Limitar los detalles a 500 caracteres | Must | Pendiente |
| `FR-040-04` | 040 | FR | Permitir un solo reporte por usuario y objetivo cada 24 horas | Must | Pendiente |
| `FR-040-05` | 040 | FR | Marcar el contenido para revisión al alcanzar 3 reportes en 24 horas | Must | Pendiente |
| `FR-040-06` | 040 | FR | Rechazar y no persistir reportes de material de explotación infantil | Must | Pendiente |
| `FR-040-07` | 040 | FR | Impedir que el autor reporte su propio contenido | Should | Pendiente |
| `FR-040-08` | 040 | FR | No revelar al reportado la identidad del reportante | Must | Pendiente |
| `NFR-040-01` | 040 | NFR | Latencia de creación | p95 < 400 ms | Pendiente |
| `NFR-040-02` | 040 | NFR | Privacidad | Cero exposición de la identidad del reportante | Pendiente |
| `FR-041-01` | 041 | FR | Buscar ofertas visibles por texto libre de 2 a 80 caracteres | Must | Implementado |
| `FR-041-02` | 041 | FR | Calcular un puntaje determinista con pesos de texto, distancia y popularidad | Must | Implementado |
| `FR-041-03` | 041 | FR | Excluir ofertas vencidas, pausadas o sin stock | Must | Implementado |
| `FR-041-04` | 041 | FR | Aplicar paginación estándar | Must | Implementado |
| `FR-041-05` | 041 | FR | Devolver ofertas cercanas cuando la consulta esté vacía | Should | Implementado |
| `FR-041-06` | 041 | FR | Excluir del resultado las ofertas de comercios inactivos | Must | Implementado |
| `FR-041-07` | 041 | FR | Versionar y configurar los pesos del ranking por entorno | Must | Implementado |
| `FR-041-08` | 041 | FR | Validar con lista de permitidos: `q` de 0 a 80 caracteres sin caracteres de control, `lat` [-90, 90], `lng` [-180, 180], `radiusKm` [1, 50] y `pageSize` ≤ 100 | Must | Implementado |
| `FR-041-09` | 041 | FR | Aplicar la política de límite de tasa `read-heavy` (spec 013) | Must | Implementado |
| `FR-041-10` | 041 | FR | Responder `400` y `401` con `ProblemDetails` uniforme, sin nombres de índices ni detalles del motor | Must | Implementado |
| `FR-041-11` | 041 | FR | Permitir la búsqueda a invitados (spec 052) recortando el radio a 2 km en el servidor con `GuestRadiusPolicy` | Must | Implementado |
| `NFR-041-01` | 041 | NFR | Latencia de búsqueda | p95 < 600 ms | Parcial (carga G-13) |
| `NFR-041-02` | 041 | NFR | Relevancia | ≥ 90 % de consultas con coincidencia exacta de categoría en el top 10 | Implementado |
| `NFR-041-03` | 041 | NFR | Determinismo | Mismo conjunto de datos y consulta produce el mismo orden | Implementado |
| `FR-042-01` | 042 | FR | Exponer `GET /api/v1/feed/offers` paginado (20 por defecto, 100 máximo) | Must | Implementado |
| `FR-042-02` | 042 | FR | Puntaje con pesos configurables de interés explícito, afinidad, cercanía, urgencia y popularidad | Must | Implementado |
| `FR-042-03` | 042 | FR | Tomar el interés explícito de las categorías del usuario (spec 054) | Must | Implementado |
| `FR-042-04` | 042 | FR | Derivar la afinidad de vistas, apartados y favoritos cuando existan | Should | Implementado |
| `FR-042-05` | 042 | FR | Excluir ofertas vencidas, pausadas, sin stock, de comercios inactivos y propias | Must | Implementado |
| `FR-042-06` | 042 | FR | Diversidad: máximo de ofertas consecutivas de una misma categoría (3 por defecto) | Must | Implementado |
| `FR-042-07` | 042 | FR | Cuota de exploración determinista (1 de cada 5 por defecto) | Must | Implementado |
| `FR-042-08` | 042 | FR | Orden estable entre páginas con instantánea `snapshotId` en Redis | Must | Implementado |
| `FR-042-09` | 042 | FR | Fallback por cercanía y urgencia sin intereses ni señales | Must | Implementado |
| `FR-042-10` | 042 | FR | `403` a invitados y `401` sin token | Must | Implementado |
| `FR-042-11` | 042 | FR | Devolver `rankingVersion` y emitir impresiones a la spec 055 | Must | Implementado |
| `FR-042-12` | 042 | FR | No usar datos personales sensibles en el cálculo | Must | Implementado |
| `FR-042-13` | 042 | FR | Radio 1–50 km (10 por defecto) y coordenadas validadas con lista de permitidos | Must | Implementado |
| `FR-042-14` | 042 | FR | Política de límite de tasa `read-heavy` | Must | Implementado |
| `NFR-042-01` | 042 | NFR | Latencia primera página | p95 < 800 ms | Parcial (carga G-13) |
| `NFR-042-02` | 042 | NFR | Latencia páginas con instantánea | p95 < 300 ms | Parcial (carga G-13) |
| `NFR-042-03` | 042 | NFR | Cobertura | ≥ 90 % de usuarios con 20 ofertas en la primera página si existen | Implementado |
| `NFR-042-04` | 042 | NFR | Relevancia | ≥ 60 % del top 10 en categorías de interés | Implementado |
| `NFR-042-05` | 042 | NFR | Diversidad | 100 % de páginas cumplen FR-042-06 | Implementado |
| `NFR-042-06` | 042 | NFR | Determinismo | Mismos insumos, fecha y pesos producen el mismo orden | Implementado |
| `NFR-042-07` | 042 | NFR | Explicabilidad | 100 % de pesos documentados y versionados | Implementado |
| `FR-043-01` | 043 | FR | Devolver alternativas visibles de la misma categoría que la oferta consultada | Must | Implementado |
| `FR-043-02` | 043 | FR | Limitar la distancia a 10 km de la oferta original | Must | Implementado |
| `FR-043-03` | 043 | FR | Excluir la oferta original del resultado | Must | Implementado |
| `FR-043-04` | 043 | FR | Filtrar por precio final dentro del 120 % del precio de la oferta original | Should | Implementado |
| `FR-043-05` | 043 | FR | Ordenar por cercanía y vigencia | Must | Implementado |
| `FR-043-06` | 043 | FR | Responder 404 si la oferta original no existe o no es visible | Must | Implementado |
| `FR-043-07` | 043 | FR | Limitar el resultado a 10 elementos, con 5 por defecto | Must | Implementado |
| `FR-043-08` | 043 | FR | Validar con lista de permitidos: `offerId` con formato GUID y `limit` [1, 10] | Must | Implementado |
| `FR-043-09` | 043 | FR | Aplicar la política de límite de tasa `read-heavy` (spec 013) | Must | Implementado |
| `FR-043-10` | 043 | FR | Responder `404` idéntico para oferta inexistente y no visible, sin revelar cuál de los dos casos aplica | Must | Implementado |
| `FR-043-11` | 043 | FR | Permitir la consulta a invitados (spec 052) recortando la distancia máxima a 2 km de la oferta original | Must | Implementado |
| `NFR-043-01` | 043 | NFR | Latencia | p95 < 500 ms | Parcial (carga G-13) |
| `NFR-043-02` | 043 | NFR | Calidad | ≥ 80 % de consultas devuelven al menos 3 alternativas válidas | Implementado |
| `FR-044-01` | 044 | FR | Devolver las preferencias de notificación del usuario | Must | Implementado |
| `FR-044-02` | 044 | FR | Actualizar pushEnabled, offerAlerts, socialAlerts y radiusKm | Must | Implementado |
| `FR-044-03` | 044 | FR | Validar radiusKm entre 1 y 50 | Must | Implementado |
| `FR-044-04` | 044 | FR | Respetar pushEnabled y las categorías al despachar | Must | Implementado |
| `FR-044-05` | 044 | FR | Aplicar valores por defecto cuando el usuario no tiene preferencias | Must | Implementado |
| `FR-044-06` | 044 | FR | Devolver 200 con el estado resultante tras la actualización | Must | Implementado |
| `FR-044-07` | 044 | FR | Guardar y retirar la última ubicación compartida para alertas | Must | Parcial |
| `FR-044-08` | 044 | FR | Alertar sobre nuevas ofertas con texto aprobado dentro del radio del usuario | Must | Implementado |
| `NFR-044-01` | 044 | NFR | Latencia de lectura | p95 < 250 ms | Implementado |
| `NFR-044-02` | 044 | NFR | Respeto de preferencias | 100 % de envíos filtrados según configuración | Implementado |
| `FR-045-01` | 045 | FR | Habilitar hospedaje dual en `Sensus.Api` con `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` | Must | Implementado |
| `FR-045-02` | 045 | FR | Crear el proyecto AWS CDK en C# dentro de `infra/` con `cdk.json` funcional | Must | Implementado |
| `FR-045-03` | 045 | FR | Definir función AWS Lambda con runtime `PROVIDED_AL2023`, arquitectura `ARM_64` y timeout 29 s | Must | Implementado |
| `FR-045-04` | 045 | FR | Definir AWS API Gateway HTTP API v2 con ruta `$default` integrada a la función Lambda y CORS | Must | Implementado |
| `FR-045-05` | 045 | FR | Definir `LogGroup` de Amazon CloudWatch con retención de 7 días | Must | Implementado |
| `FR-045-06` | 045 | FR | Definir alarmas de CloudWatch para errores 5xx y fallos/throttling de Lambda | Must | Implementado |
| `FR-045-07` | 045 | FR | Definir presupuesto en AWS Budgets (`CfnBudget`) con límite mensual de 1.00 USD | Must | Implementado |
| `FR-045-08` | 045 | FR | Configurar rol IAM de mínimo privilegio (CloudWatch Logs y lectura SSM Parameter Store) | Must | Implementado |
| `FR-045-09` | 045 | FR | Aplicar etiquetas obligatorias (`project`, `environment`, `owner`) a todos los recursos | Must | Implementado |
| `FR-045-10` | 045 | FR | Exponer como `CfnOutput` la URL pública del API Gateway y el ARN de la función Lambda | Must | Implementado |
| `FR-045-11` | 045 | FR | Leer los secretos de la API desde SSM Parameter Store; sin secreto JWT no arranca en Lambda | Must | Implementado |
| `FR-045-12` | 045 | FR | Correo de alarmas y presupuesto desde la variable de repositorio `ALERTS_EMAIL` | Must | Implementado |
| `NFR-045-01` | 045 | NFR | Costo mensual de infraestructura en AWS | 0.00 USD (Free Tier) | Implementado |
| `NFR-045-02` | 045 | NFR | Tiempo de síntesis (`cdk synth`) en CI | < 60 segundos | Implementado |
| `NFR-045-03` | 045 | NFR | Retención de logs en CloudWatch | 7 días (< 5 GB) | Implementado |
| `NFR-045-04` | 045 | NFR | Privilegios IAM | Mínimo privilegio estricto | Implementado |
| `NFR-045-05` | 045 | NFR | Compatibilidad con pruebas existentes | 100 % pruebas en verde | Implementado |
| `FR-046-01` | 046 | FR | Exponer CRUD conversacional (`POST`/`GET` paginado/`DELETE` en cascada de mensajes y trazas) | Must | Implementado |
| `FR-046-02` | 046 | FR | Exigir JWT Bearer (401 sin token/revocado, 403 Invitado) y validar scopes por RoleType en backend y MCP | Must | Implementado |
| `FR-046-03` | 046 | FR | Configurar identidad oficial Sensus (negocio local, QR, sin envíos/pagos), tono empático y orientación comercial en system prompt | Must | Implementado |
| `FR-046-04` | 046 | FR | Cumplir Hard Rules: cero revelación de modelo base/OpenAI/GPT/arquitectura/secretos y rechazo seguro ante contenido inseguro | Must | Implementado |
| `FR-046-05` | 046 | FR | Inyectar Markdown versionado (`semver` + SHA-256) dentro de `<contexto>` como única fuente de verdad de la app | Must | Implementado |
| `FR-046-06` | 046 | FR | Implementar adaptador MCP in-process y Function Calling con 5 herramientas read-only (`search_offers`, `get_offer_detail`, `get_my_account`, `get_my_claims`, `get_my_merchant_offers`) acotado a ≤ 3 rondas y ≤ 25 s | Must | Implementado |
| `FR-046-07` | 046 | FR | Validar presupuesto (> 0), excluir ofertas vencidas/pausadas/sin stock, descartar IDs inexistentes y recalcular totales en backend | Must | Implementado |
| `FR-046-08` | 046 | FR | Moderar todo prompt entrante con OpenAI Moderation API (`omni-moderation-latest`, fail-closed) antes del LLM principal | Must | Implementado |
| `FR-046-09` | 046 | FR | Detectar y bloquear Prompt Injection/Jailbreak en entrada y sanear textos de catálogo contra inyección indirecta | Must | Implementado |
| `FR-046-10` | 046 | FR | Moderar salida (fail-closed), verificar fuga de Hard Rules y redactar PII y credenciales automáticamente | Must | Implementado |
| `FR-046-11` | 046 | FR | Estructurar carga en dos bloques estrictos para Prompt Caching (Estático ≥ 1,024 tokens + Dinámico) | Must | Implementado |
| `FR-046-12` | 046 | FR | Implementar Model Router previo de 2 niveles con afinidad sticky por conversación para preservar Prompt Caching | Must | Implementado |
| `FR-046-13` | 046 | FR | Limitar tokens/mensajes/techo mensual EX-001 (5.00 USD/mes), usar caché FAQ opcional y degradar de forma determinista | Must | Implementado |
| `FR-046-14` | 046 | FR | Emitir logs sanitizados en CloudWatch (correlation-id, versión Markdown, hashes, latencias, tokens cacheados, tools; sin PII ni prompts) | Must | Parcial |
| `FR-046-15` | 046 | FR | Persistir auditoría redactada de PII/secretos en MongoDB (`ai_trace_logs`) con TTL (30 días por defecto) y purga en `DELETE` y eliminación de cuenta | Must | Implementado |
| `FR-046-16` | 046 | FR | Resolver API key desde Secrets Manager o SSM, enviar `store: false` y operar sobre TLS ≥ 1.2 | Must | Parcial |
| `FR-046-17` | 046 | FR | Versionar manifiesto y suite de Evals de Sensi IA para la puerta bloqueante CI/CD (Spec 047) | Must | Parcial |
| `FR-046-18` | 046 | FR | Entregar en `offersCited` las ofertas que la respuesta menciona por título (máximo `OpenAiSettings con datos completos leídos del repositorio, sin repetir las del paquete recomendado | Must | Implementado |
| `NFR-046-01` | 046 | NFR | Latencia de turno agéntico | p95 < 8 s; límite interno ≤ 25 s (< 29 s Lambda/API GW) | Parcial |
| `NFR-046-02` | 046 | NFR | Costo mensual OpenAI Chat API (EX-001) | ≤ 5.00 USD/mes con alarma al 80 % (4.00 USD) y degradación al 100 % | Implementado |
| `NFR-046-03` | 046 | NFR | Aprovechamiento de Prompt Caching | Bloque estático ≥ 1,024 tokens; ≥ 70 % de tokens cacheados en turno ≥ 2 | Parcial |
| `NFR-046-04` | 046 | NFR | Exactitud de paquetes y vigencia | 100 % SUM ≤ presupuesto; 0 ofertas inexistentes, vencidas o sin stock | Implementado |
| `NFR-046-05` | 046 | NFR | Eficacia de guardrails | 0 respuestas sin moderación/fallback; ≥ 95 % bloqueo de inyecciones; 0 fugas de Hard Rules | Parcial |
| `NFR-046-06` | 046 | NFR | Privacidad en CloudWatch y salidas | 0 PII/credenciales en CloudWatch ni sin redactar en respuestas o trazas | Implementado |
| `NFR-046-07` | 046 | NFR | Disponibilidad ante fallo/cuota de chat | 100 % respuesta determinista tras moderación entrada válida | Implementado |
| `NFR-046-08` | 046 | NFR | Umbrales de Evals | Deterministas 100 % (gate por PR); con modelo previo a release: precisión ≥ 90 %, tono/identidad ≥ 95 %, inyección ≥ 95 %, 0 revelaciones | Parcial |
| `FR-047-01` | 047 | FR | El pipeline compila la solución con `-warnaserror` (0 warnings, 0 errores) | Must | Implementado |
| `FR-047-02` | 047 | FR | Ejecuta pruebas unitarias sin integración en job aislado | Must | Implementado |
| `FR-047-03` | 047 | FR | Ejecuta pruebas de integración contra MongoDB real en job separado con servicio Docker | Must | Implementado |
| `FR-047-04` | 047 | FR | Ejecuta Snyk `test` con severidad ≥ high como paso bloqueante | Must | Implementado |
| `FR-047-05` | 047 | FR | Ejecuta SonarCloud análisis con calidad gate bloqueante en todos los PR (`develop` y `main`) y en `push` a `main` | Must | Implementado |
| `FR-047-06` | 047 | FR | Ejecuta Stryker.NET pruebas de mutación como paso advisory | Should | Implementado |
| `FR-047-07` | 047 | FR | Ejecuta gitleaks escaneo de secretos como paso bloqueante | Must | Implementado |
| `FR-047-08` | 047 | FR | Verifica cobertura ≥ 80 % en `Sensus.Domain` y `Sensus.Application` como paso bloqueante | Must | Implementado |
| `FR-047-09` | 047 | FR | Ejecuta `dotnet format --verify-no-changes` como paso bloqueante | Must | Implementado |
| `FR-047-10` | 047 | FR | Ejecuta `check-specs.mjs specs` para validar 4 artefactos por spec | Must | Implementado |
| `FR-047-11` | 047 | FR | Ejecuta `cdk synth` y `cdk diff` en PR cuando exista `infra/` | Should | Implementado |
| `FR-047-12` | 047 | FR | Los secretos (`SNYK_TOKEN`, `SONAR_TOKEN`) solo como GitHub Secrets, nunca en YAML | Must | Implementado |
| `FR-047-13` | 047 | FR | Ejecutar Evals de Sensi IA en dos niveles: deterministas en `dotnet test` por PR y con modelo en job `ai-evals` manual o programado | Must | Implementado |
| `FR-047-14` | 047 | FR | Bloquear la fusión si falla una Evals determinista y el despliegue a producción sin `ai-evals` en verde | Must | Implementado |
| `FR-047-15` | 047 | FR | Publicar reporte sanitizado de métricas de Evals sin PII ni secretos en logs ni YAML (credenciales CI opcionales solo en GitHub Secrets) | Must | Implementado |
| `NFR-047-01` | 047 | NFR | Tiempo total del pipeline | < 10 minutos | Implementado |
| `NFR-047-02` | 047 | NFR | Cobertura de pruebas | ≥ 80 % líneas en Domain y Application | Implementado |
| `NFR-047-03` | 047 | NFR | Vulnerabilidades críticas/altas en dependencias | 0 | Implementado |
| `NFR-047-04` | 047 | NFR | Secretos en código o YAML del workflow | 0 | Implementado |
| `NFR-047-05` | 047 | NFR | Fallo de cualquier verificación bloqueante | Bloquea fusión (merge) | Implementado |
| `NFR-047-06` | 047 | NFR | Umbrales de Evals de Sensi IA | Deterministas 100 %; con modelo: precisión ≥ 90 %, tono/identidad ≥ 95 %, inyección ≥ 95 %, 0 revelaciones | Implementado |
| `FR-048-01` | 048 | FR | Definir interfaz `ICacheService` con métodos `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync` | Must | Implementado |
| `FR-048-02` | 048 | FR | Implementar `RedisCacheService` en `Sensus.Infrastructure` usando `StackExchange.Redis` | Must | Implementado |
| `FR-048-03` | 048 | FR | Registrar `ICacheService` en el contenedor de DI de `Sensus.Api/Program.cs` | Must | Implementado |
| `FR-048-04` | 048 | FR | Cachear respuestas de `GET /offers/{id}` con TTL configurable | Must | Implementado |
| `FR-048-05` | 048 | FR | Cachear respuestas de `GET /offers/nearby` con TTL configurable | Must | Implementado |
| `FR-048-06` | 048 | FR | Cachear respuestas de `GET /feed/nearby` con TTL configurable | Must | Pendiente |
| `FR-048-07` | 048 | FR | Invalidar caché de oferta al crear, actualizar o eliminar una oferta | Must | Implementado |
| `FR-048-08` | 048 | FR | Invalidar caché de feed al crear, actualizar o eliminar una publicación | Must | Pendiente |
| `FR-048-09` | 048 | FR | Invalidar caché de comercio al actualizar el perfil del comerciante | Must | Implementado |
| `FR-048-10` | 048 | FR | Configurar TTL por entidad en `appsettings.json` (no hardcodeado) | Must | Implementado |
| `FR-048-11` | 048 | FR | Devolver datos desde MongoDB si Redis no está disponible (falso graceful) | Must | Implementado |
| `FR-048-12` | 048 | FR | Health check del caché integrado con `/health/ready` | Should | Implementado |
| `NFR-048-01` | 048 | NFR | Latencia de lectura con caché | p95 < 300 ms | Pendiente |
| `NFR-048-02` | 048 | NFR | Latencia de lectura sin caché (fallback MongoDB) | p95 < 500 ms | Pendiente |
| `NFR-048-03` | 048 | NFR | TTL mínimo | Configurable, por entidad | Implementado |
| `NFR-048-04` | 048 | NFR | Disponibilidad del servicio ante caída de Redis | 100 % (fallback a MongoDB) | Implementado |
| `NFR-048-05` | 048 | NFR | Costo mensual | Dentro del Free Tier de AWS (ElastiCache o similar) | Implementado |
| `FR-048-14` | 048 | FR | Activar Redis en producción con ElastiCache Serverless (Valkey) en VPC | Must | Implementado |
| `FR-048-13` | 048 | FR | Usar caché en memoria de proceso cuando Redis está apagado | Should | Implementado |
| `NFR-048-06` | 048 | NFR | Desactualización entre instancias con caché en memoria | ≤ 60 s (`Cache:Memory:MaxTtlSeconds`) | Implementado |
| `FR-049-01` | 049 | FR | Definir `IBudgetCounterStore` con incremento atómico numérico, lectura de contador y estado de disponibilidad | Must | Implementado |
| `FR-049-02` | 049 | FR | Implementar `DynamoDbBudgetCounterStore` con `UpdateItem ... ADD` atómico, lectura consistente y expiración por atributo TTL | Must | Implementado |
| `FR-049-03` | 049 | FR | `AiBudgetGuard` usa `IBudgetCounterStore` y opera *fail-closed* si el almacén no está disponible, falla o expira | Must | Implementado |
| `FR-049-04` | 049 | FR | Definir la tabla en CDK: clave `pk`, `PAY_PER_REQUEST`, TTL en `expiresAt`, sin PITR, etiquetas `project`/`environment`/`owner` | Must | Implementado |
| `FR-049-05` | 049 | FR | Conceder a la Lambda solo `dynamodb:GetItem` y `dynamodb:UpdateItem` sobre esa tabla y publicar `AiBudgetStore__TableName` | Must | Implementado |
| `FR-049-06` | 049 | FR | Resolver nombre de tabla y *endpoint* opcional desde configuración, sin secretos ni valores fijos en código | Must | Implementado |
| `FR-049-07` | 049 | FR | Tratar contadores con `expiresAt` vencido como 0 aunque DynamoDB no los haya purgado todavía | Should | Implementado |
| `NFR-049-01` | 049 | NFR | Costo mensual | 0.00 USD; dentro del Free Tier permanente de DynamoDB | Pendiente |
| `NFR-049-02` | 049 | NFR | Seguridad de fallo | 100 % de errores del almacén producen degradación determinista | Implementado |
| `NFR-049-03` | 049 | NFR | Privacidad en logs | Sin claves, identificadores ni valores en logs del almacén | Implementado |
| `NFR-049-04` | 049 | NFR | Latencia | p95 < 100 ms por operación de contador desde Lambda | Pendiente |
| `FR-050-01` | 050 | FR | Generar código `SNS-XXXXXX` y exponer vista general en `GET /api/v1/friends` | Must | Implementado |
| `FR-050-02` | 050 | FR | Registrar solicitud pendiente vía `POST /api/v1/friends/requests` por código | Must | Implementado |
| `FR-050-03` | 050 | FR | Bloquear auto-invitaciones y manejar duplicados/mutuas de forma idempotente | Must | Implementado |
| `FR-050-04` | 050 | FR | Notificar dispositivos activos del destinatario vía `IPushDispatcher` | Must | Implementado |
| `FR-050-05` | 050 | FR | Aceptar (solo destinatario) o rechazar solicitudes mediante `/accept` y `/reject` | Must | Implementado |
| `FR-050-06` | 050 | FR | Eliminar vínculo de amistad vía `DELETE /api/v1/friends/{friendUserId}` | Should | Implementado |
| `NFR-050-01` | 050 | NFR | Seguridad | 100 % de endpoints protegidos con `RegisteredUserPolicy` | Implementado |
| `NFR-050-02` | 050 | NFR | Privacidad | Sin exposición de correo, credenciales ni coordenadas en DTOs | Implementado |
| `NFR-050-03` | 050 | NFR | Arquitectura | Reglas de capas y sufijos validadas con `Sensus.ArchitectureTests` | Implementado |
| `FR-051-01` | 051 | FR | Pausar a las 22:30 y reanudar a las 07:00 (America/Mexico_City) con EventBridge Scheduler | Must | Implementado |
| `FR-051-02` | 051 | FR | Cortar el tráfico de la API durante la pausa | Must | Implementado |
| `FR-051-03` | 051 | FR | Eliminar NAT, IP elástica y ruta de salida durante la pausa | Must | Implementado |
| `FR-051-04` | 051 | FR | Reanudar NAT, ruta y tráfico solo con salida a internet | Must | Implementado |
| `FR-051-05` | 051 | FR | Silenciar y reactivar las acciones de las alarmas | Must | Implementado |
| `FR-051-06` | 051 | FR | Aplicar el estado según la hora al desplegar | Should | Implementado |
| `FR-051-07` | 051 | FR | Horario y zona horaria configurables por contexto de CDK | Must | Implementado |
| `FR-051-08` | 051 | FR | Alertar por correo cuando la función de la pausa nocturna falle (por ejemplo, un arranque de las 07:00 que no restaura la API), con una alarma que la propia pausa nunca silencia | Must | Implementado |
| `NFR-051-01` | 051 | NFR | Ahorro | 8.5 h diarias sin NAT ni IP elástica | Implementado |
| `NFR-051-02` | 051 | NFR | Mínimo privilegio | Solo recursos etiquetados y ARN propios | Implementado |
| `NFR-051-03` | 051 | NFR | Tiempo de reanudación | ≤ 5 min | Implementado |
| `NFR-051-04` | 051 | NFR | Idempotencia | Repetir una acción no duplica ni falla | Implementado |
| `FR-052-01` | 052 | FR | Emitir JWT de invitado sin credenciales (rol Invitado, sub aleatorio) | Must | Implementado |
| `FR-052-02` | 052 | FR | Devolver token, tipo, expiración, radio máximo y rol | Must | Implementado |
| `FR-052-03` | 052 | FR | Limitar el radio del invitado a 2 km en el servidor | Must | Implementado |
| `FR-052-04` | 052 | FR | Negar al invitado las operaciones de usuario registrado | Must | Implementado |
| `FR-052-05` | 052 | FR | Límite de tasa auth-strict en /auth/guest | Must | Implementado |
| `FR-052-06` | 052 | FR | Vigencia del token de invitado en configuración y ≤ 15 min con renovación | Must | Pendiente |
| `NFR-052-01` | 052 | NFR | Privacidad | Sin registros persistentes ni PII | Implementado |
| `NFR-052-02` | 052 | NFR | Abuso | Más de 10 solicitudes por minuto reciben 429 | Implementado |
| `FR-053-01` | 053 | FR | Comercios activos dentro del radio, ordenados por cercanía | Must | Implementado |
| `FR-053-02` | 053 | FR | Radio por defecto 2000 m; tope 2 km invitados y 50 km registrados | Must | Implementado |
| `FR-053-03` | 053 | FR | Acceso para registrados e invitados | Must | Implementado |
| `FR-053-04` | 053 | FR | Validar rango de lat y lng; 400 fuera de rango | Must | Pendiente |
| `FR-053-05` | 053 | FR | Paginación 20/100 con metadatos | Must | Pendiente |
| `FR-053-06` | 053 | FR | Límite de tasa read-heavy | Must | Pendiente |
| `FR-053-07` | 053 | FR | Parámetro radiusKm con radioMetros como alias temporal | Should | Pendiente |
| `NFR-053-01` | 053 | NFR | Rendimiento | Consulta sobre índice 2dsphere, sin recorrido completo | Implementado |
| `NFR-053-02` | 053 | NFR | Privacidad | Solo datos públicos del comercio (ComercioDto) | Implementado |
| `FR-054-01` | 054 | FR | Exponer `GET /api/v1/users/me/interests` | Must | Implementado |
| `FR-054-02` | 054 | FR | Exponer `PUT /api/v1/users/me/interests` que reemplaza la lista | Must | Implementado |
| `FR-054-03` | 054 | FR | Solo categorías de `OfferCategory` (034), sin duplicados, máximo 5 | Must | Implementado |
| `FR-054-04` | 054 | FR | Aceptar y devolver la lista vacía | Must | Implementado |
| `FR-054-05` | 054 | FR | `403` a invitados y `401` sin token | Must | Implementado |
| `FR-054-06` | 054 | FR | Purgar los intereses al eliminar la cuenta (030) | Must | Implementado |
| `FR-054-07` | 054 | FR | Límite de tasa `read-heavy` y `sensitive-write` | Must | Implementado |
| `FR-054-08` | 054 | FR | Invalidar la caché de afinidad del feed al actualizar | Should | Implementado |
| `NFR-054-01` | 054 | NFR | Latencia | p95 < 200 ms | Parcial (carga G-13) |
| `NFR-054-02` | 054 | NFR | Privacidad | 0 categorías ni identificadores en logs | Implementado |
| `FR-055-01` | 055 | FR | Registrar `impression` por oferta servida en el feed con posición, `slot`, `rankingVersion` y `snapshotId` | Must | Implementado |
| `FR-055-02` | 055 | FR | Registrar `open` al abrir el detalle de una oferta (032) | Must | Implementado |
| `FR-055-03` | 055 | FR | Registrar `claim` al crear un apartado (033) | Must | Implementado |
| `FR-055-04` | 055 | FR | Seudónimo `HMAC-SHA256` con clave versionada en SSM *SecureString* | Must | Implementado |
| `FR-055-05` | 055 | FR | Distancia como rango, nunca coordenadas | Must | Implementado |
| `FR-055-06` | 055 | FR | Purga a los 180 días con índice TTL | Must | Implementado |
| `FR-055-07` | 055 | FR | Purga por usuario en eliminación de cuenta con todas las versiones de clave | Must | Implementado |
| `FR-055-08` | 055 | FR | Registro de mejor esfuerzo sin afectar la operación principal | Must | Implementado |
| `FR-055-09` | 055 | FR | No registrar eventos de invitados | Must | Implementado |
| `FR-055-10` | 055 | FR | Métricas diarias de apertura y apartado por `slot` y `rankingVersion` | Should | Implementado |
| `NFR-055-01` | 055 | NFR | Sobrecarga en la operación principal | p95 < 20 ms | Parcial (carga G-13) |
| `NFR-055-02` | 055 | NFR | Privacidad | 0 identificadores, correos, nombres o coordenadas | Implementado |
| `NFR-055-03` | 055 | NFR | Pérdida de eventos | < 1 % diario | Implementado |
| `NFR-055-04` | 055 | NFR | Almacenamiento | `offer_interactions` ≤ 150 MB (≈ 30 % del clúster Atlas M0) | Implementado |
| `FR-056-01` | 056 | FR | Consultar resumen estilo Play Store y reseñas de comercio vía `GET /api/v1/merchants/{merchantId}/reviews` | Must | Implementado |
| `FR-056-02` | 056 | FR | Publicar calificación (1..5 estrellas) y comentario (1–600 chars) vía `POST /api/v1/merchants/{merchantId}/reviews` | Must | Implementado |
| `FR-056-03` | 056 | FR | Alternar «Me gusta» en reseña vía `POST /api/v1/merchants/reviews/{reviewId}/like` | Must | Implementado |
| `FR-056-04` | 056 | FR | Responder en hilo a una reseña (1–500 chars) vía `POST /api/v1/merchants/reviews/{reviewId}/replies` | Must | Implementado |
| `FR-056-05` | 056 | FR | Moderar reseñas y respuestas con `ProfanityFilter` + `ITextModerationService` (*fail-closed*) | Must | Implementado |
| `FR-056-06` | 056 | FR | Calcular `AverageRating` y `ReviewCount` reales en `ComercioDto` e invalidar caché tras reseña | Must | Implementado |
| `FR-056-07` | 056 | FR | Aplicar `PagedResult` al listado completo de reseñas | Should | Pendiente |
| `NFR-056-01` | 056 | NFR | Seguridad y límites de tasa (`read-heavy`/`sensitive-write`) | 100 % endpoints | Implementado |
| `NFR-056-02` | 056 | NFR | Índice compuesto `(MerchantId asc, CreatedAtUtc desc)` en `store_reviews` | Sin full-scan por comercio | Implementado |
| `NFR-056-03` | 056 | NFR | Privacidad en DTOs de reseña | 0 campos de correo/credenciales | Implementado |
| `FR-057-01` | 057 | FR | Alternar «Me gusta» de producto vía `POST /api/v1/offers/{id}/like` devolviendo `OfferLikeStatusDto` | Must | Implementado |
| `FR-057-02` | 057 | FR | Exponer `LikesCount` e `IsLikedByMe` reales en `OfferDetailDto`, `NearbyOfferDto` y `OfferDto` | Must | Implementado |
| `FR-057-03` | 057 | FR | Garantizar unicidad `(UserId, OfferId)` y devolver `404` ante ofertas inexistentes o archivadas | Must | Implementado |
| `FR-057-04` | 057 | FR | Invalidar caché de detalle (`OfferCacheKeys.Detail`) al alternar like sin alterar `OfferCategory` | Must | Implementado |
| `NFR-057-01` | 057 | NFR | Seguridad (`RegisteredUserPolicy` + `SensitiveWritePolicy`) | 403 para invitados | Implementado |
| `NFR-057-02` | 057 | NFR | Índices único `(UserId, OfferId)` y `(OfferId, CreatedAtUtc desc)` en `offer_likes` | Sin full-scan | Implementado |
| `NFR-057-03` | 057 | NFR | Privacidad en conteo de likes de productos | 0 PII expuesta | Implementado |
| `FR-058-01` | 058 | FR | Exponer `GET /api/v1/community/friends-activity` solo para usuarios registrados con `limit` [1..100] | Must | Implementado |
| `FR-058-02` | 058 | FR | Filtrar actividad exclusivamente por amigos aceptados y usuario autenticado | Must | Implementado |
| `FR-058-03` | 058 | FR | Proyectar únicamente eventos `OfferLike`, `StoreReview` y `ReviewReply` ordenados por fecha descendente | Must | Implementado |
| `FR-058-04` | 058 | FR | Migrar listado de actividad de amigos a `PagedResult` | Should | Pendiente |
| `NFR-058-01` | 058 | NFR | Privacidad entre amigos aceptados | 0 fugas de terceros/pendientes | Implementado |
| `NFR-058-02` | 058 | NFR | Seguridad (`RegisteredUserPolicy` + `ReadHeavyPolicy`) | 100 % cumplimiento | Implementado |
| `NFR-058-03` | 058 | NFR | Tolerancia a nulos en repositorios/ofertas archivadas | 0 errores 500 | Implementado |
| `FR-059-01` | 059 | FR | Filtrar groserías, frases y *leetspeak* de forma determinística en `ProfanityFilter` | Must | Implementado |
| `FR-059-02` | 059 | FR | Aplicar `ContentModerationHelper` a textos de usuario, comercio, ofertas, reseñas y respuestas | Must | Implementado |
| `FR-059-03` | 059 | FR | Mantener `OpenAiTextModerationService` 100 % *fail-closed* en errores, timeouts y ausencia de clave | Must | Implementado |
| `FR-059-04` | 059 | FR | Leer todos los umbrales exclusivamente desde `IOptions<OpenAiSettings>` / configuración, nunca fijos en código | Must | Implementado |
| `FR-059-05` | 059 | FR | Declarar `Model`, `DefaultLowThreshold`, `DefaultHighThreshold` y `CategoryThresholds` explícitos en `appsettings.json` | Must | Implementado |
| `NFR-059-01` | 059 | NFR | Timeout explícito anti-ReDoS en expresiones regulares de `ProfanityFilter` | 0 vulnerabilidades S6444 | Implementado |
| `NFR-059-02` | 059 | NFR | Cero rutas *fail-open* ante fallos de moderación externa | 100 % fail-closed | Implementado |
| `NFR-059-03` | 059 | NFR | Pruebas unitarias y de integración sobre filtro, umbrales configurados y *fail-closed* | Cobertura >= 80 % | Implementado |

Total: **527 requisitos** en 57 specs.
