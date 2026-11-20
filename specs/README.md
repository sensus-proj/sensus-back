# Índice de especificaciones — Sensus Backend

Cada spec es una unidad independiente de alcance con su propio `spec.md`, `plan.md`, `tasks.md` y `checklist.md`.
La numeración es estable: un spec no cambia de ID una vez creado.

| ID | Spec | Área | Tipo | Ola | Estado | Dependencias |
|---|---|---|---|---|---|---|
| 001 | [Registro de cuenta unificada](001-registro-usuario/spec.md) | Autenticación | Retro-spec | 1 | Implementado (retro-spec) | Ninguna |
| 002 | [Inicio de sesión](002-inicio-sesion/spec.md) | Autenticación | Retro-spec | 1 | Implementado (retro-spec) | 001 |
| 003 | [Perfil del usuario autenticado](003-perfil-usuario/spec.md) | Autenticación | Retro-spec | 1 | Implementado (retro-spec) | 002, 022 |
| 004 | [Cierre de sesión y revocación de token](004-cierre-sesion-y-revocacion/spec.md) | Autenticación | Nuevo | 6 | Implementado | 002 |
| 005 | [Activación del rol comercial](005-activacion-comerciante/spec.md) | Comercios | Retro-spec | 2 | Implementado (retro-spec) | 001, 002 |
| 006 | [Consulta de mi comercio](006-consulta-mi-comercio/spec.md) | Comercios | Retro-spec | 2 | Implementado (retro-spec) | 005 |
| 007 | [Ofertas cercanas](007-ofertas-cercanas/spec.md) | Ofertas | Nuevo | 2 | Implementado | 005, 010, 034 |
| 008 | [Retiro de endpoints legacy](008-retiro-endpoints-legacy/spec.md) | Plataforma | Nuevo | 6 | Implementado | 001, 005, 006 |
| 009 | [Errores uniformes con ProblemDetails](009-errores-problemdetails/spec.md) | Plataforma | Nuevo | 0 | Parcial | Ninguna |
| 010 | [Paginación estándar](010-paginacion-estandar/spec.md) | Plataforma | Nuevo | 0 | Parcial | 009 |
| 011 | [Contrato OpenAPI](011-contrato-openapi/spec.md) | Plataforma | Nuevo | 0 | Parcial | 009, 010 |
| 012 | [CORS y HTTPS por entorno](012-cors-y-https/spec.md) | Seguridad | Nuevo | 6 | Parcial | Ninguna |
| 013 | [Control de tasa de peticiones](013-rate-limiting/spec.md) | Seguridad | Nuevo | 6 | Implementado | 012 |
| 014 | [Logs estructurados y correlación](014-logs-estructurados-correlacion/spec.md) | Observabilidad | Nuevo | 6 | Implementado | 009 |
| 015 | [Métricas y alarmas](015-metricas-y-alarmas/spec.md) | Observabilidad | Nuevo | 6 | Implementado | 014 |
| 016 | [Health checks](016-health-checks/spec.md) | Observabilidad | Nuevo | 6 | Implementado | 014 |
| 017 | [Moderación multimodal de contenido](017-moderacion-multimodal/spec.md) | Moderación | Nuevo | 4 | Parcial | 009, 022 |
| 018 | [Umbrales y bandas de decisión](018-umbrales-y-bandas/spec.md) | Moderación | Nuevo | 4 | Implementado | 017 |
| 019 | [Cuarentena de contenido](019-cuarentena-contenido/spec.md) | Moderación | Nuevo | 4 | Implementado | 017, 018 |
| 020 | [Sanciones y baneo de usuario](020-sanciones-y-baneo/spec.md) | Moderación | Nuevo | 4 | Implementado | 004, 017, 018, 019, 027 |
| 021 | [Auditoría de moderación](021-auditoria-moderacion/spec.md) | Moderación | Nuevo | 4 | Pendiente | 017, 020 |
| 022 | [Carga de multimedia](022-carga-multimedia/spec.md) | Multimedia | Nuevo | 4 | Implementado | 017, 013 |
| 023 | [Entrega de contenido por CDN](023-entrega-cdn/spec.md) | Multimedia | Nuevo | 4 | Pendiente | 022 |
| 024 | [Publicaciones del feed](024-publicaciones-feed/spec.md) | Contenido | Nuevo | 3 | Pendiente | 017, 022 |
| 025 | [Comentarios del feed](025-comentarios/spec.md) | Contenido | Nuevo | 3 | Pendiente | 017, 024, 010 |
| 026 | [Feed geolocalizado](026-feed-cercano/spec.md) | Contenido | Nuevo | 3 | Pendiente | 024, 007, 010 |
| 027 | [Registro de dispositivo para push](027-registro-dispositivo/spec.md) | Integraciones | Nuevo | 4 | Parcial | 003, 013 |
| 028 | [Webhooks entrantes (backlog)](028-webhooks-entrantes/spec.md) | Integraciones | Backlog | Backlog | Backlog | 009, 014 |
| 029 | [Recuperación de contraseña](029-recuperacion-password/spec.md) | Autenticación | Nuevo | 1 | Parcial | 001, 004, 013 |
| 030 | [Eliminación de cuenta y datos](030-eliminacion-cuenta/spec.md) | Autenticación | Nuevo | 1 | Parcial | 003, 004, 020 |
| 031 | [Edición del perfil de comercio](031-edicion-perfil-comercio/spec.md) | Comercios | Nuevo | 2 | Implementado | 005, 006 |
| 032 | [Detalle de oferta y contador de vistas](032-detalle-oferta-y-vistas/spec.md) | Ofertas | Nuevo | 2 | Implementado | 007 |
| 033 | [Apartado de oferta con código QR](033-apartado-con-qr/spec.md) | Ofertas | Nuevo | 2 | Parcial | 007, 027, 032, 037 |
| 034 | [Creación de oferta](034-creacion-oferta/spec.md) | Inventario | Nuevo | 2 | Parcial | 005, 017, 022 |
| 035 | [Edición de oferta](035-edicion-oferta/spec.md) | Inventario | Nuevo | 2 | Parcial | 034, 033 |
| 036 | [Ciclo de vida de la oferta](036-ciclo-vida-oferta/spec.md) | Inventario | Nuevo | 2 | Parcial | 034, 035 |
| 037 | [Inventario de ofertas del comerciante](037-inventario-comerciante/spec.md) | Inventario | Nuevo | 2 | Parcial | 034, 033, 010 |
| 038 | [Favoritos de ofertas](038-favoritos/spec.md) | Recomendaciones | Nuevo | 5 | Pendiente | 032, 042 |
| 039 | [Me gusta en publicaciones](039-me-gusta-publicaciones/spec.md) | Contenido | Nuevo | 3 | Pendiente | 024, 013 |
| 040 | [Reportes de contenido](040-reportes-contenido/spec.md) | Contenido | Nuevo | 3 | Pendiente | 024, 025, 017, 018 |
| 041 | [Búsqueda de ofertas](041-busqueda-inteligente/spec.md) | Recomendaciones | Nuevo | 5 | Pendiente | 007, 010, 011 |
| 042 | [Recomendaciones personalizadas](042-recomendaciones/spec.md) | Recomendaciones | Nuevo | 5 | Pendiente | 007, 038, 039, 041 |
| 043 | [Alternativas a una oferta](043-alternativas-oferta/spec.md) | Recomendaciones | Nuevo | 5 | Pendiente | 032, 041 |
| 044 | [Ajustes de notificaciones](044-ajustes-notificaciones/spec.md) | Integraciones | Nuevo | 4 | Parcial | 003, 027, 034 |
| 045 | [Infraestructura Serverless como Código (AWS CDK)](045-infraestructura-serverless/spec.md) | Plataforma | Nuevo | 7 | Implementado | 009, 012, 014, 015, 016 |
| 046 | [Asistente agéntico Sensi IA (7 capas)](046-asistente-sensi-ia/spec.md) | Asistente | Nuevo | 5 | Parcial | 002, 004, 007, 013, 017, 018, 030, 033, 041, 047, 048 |
| 047 | [Pipeline CI-CD y calidad DevSecOps](047-ci-cd-pipeline/spec.md) | Plataforma | Nuevo | 7 | Implementado | 009, 010, 011, 012, 013 |
| 048 | [Caché distribuido con Redis](048-cache-distribuido-redis/spec.md) | Plataforma | Nuevo | 7 | Parcial | 010, 024, 026, 032 |
| 049 | [Contadores de presupuesto de Sensi IA en DynamoDB](049-contadores-presupuesto-dynamodb/spec.md) | Plataforma | Nuevo | 7 | Parcial | 045, 046, 048 |
| 051 | [Pausa nocturna de la infraestructura](051-pausa-nocturna-infraestructura/spec.md) | Plataforma | Nuevo | 7 | Implementado | 015, 045, 048 |
| 052 | [Sesión de invitado](052-sesion-invitado/spec.md) | Autenticación | Retro-spec | 1 | Parcial (retro-spec) | 002, 013 |
| 053 | [Comercios cercanos](053-comercios-cercanos/spec.md) | Comercios | Retro-spec | 2 | Parcial (retro-spec) | 005, 010, 013, 052 |
| 050 | [Red de amigos y solicitudes por QR o invitación](050-amigos-y-solicitudes/spec.md) | Social | Retro-spec | 5 | Parcial | 001, 002, 027 |
| 056 | [Reseñas y calificaciones de comercios (`StoreReview`)](056-resenas-comercios/spec.md) | Social | Retro-spec | 3 | Implementado (retro-spec) | 001, 005, 006, 017, 052, 059 |
| 057 | [Me gusta en ofertas (`OfferLike`)](057-me-gusta-ofertas/spec.md) | Ofertas | Retro-spec | 3 | Implementado (retro-spec) | 001, 007, 032, 034, 052 |
| 058 | [Actividad de amigos en comunidad (`CommunityEndpoints`)](058-actividad-amigos-comunidad/spec.md) | Social | Retro-spec | 5 | Implementado (retro-spec) | 050, 056, 057 |
| 059 | [Filtro de groserías y moderación textual fail-closed](059-moderacion-texto-filtro-groserias/spec.md) | Moderación | Retro-spec | 4 | Parcial | 017, 018, 046, 056 |

## Estados

| Estado | Significado |
|---|---|
| Pendiente | Sin trabajo iniciado |
| Parcial | Existe implementación sin spec o con brechas |
| Implementado (retro-spec) | Funcionalidad existente documentada a posteriori |
| Backlog | Capacidad fuera del catálogo vigente, sin compromiso de entrega |

## Reglas

- Un spec nuevo se crea con el siguiente número libre y una carpeta `NNN-slug`.
- No se implementa nada que no esté en un spec con Constitution Check evaluado.
- Todo cambio de alcance se refleja primero en `spec.md`.
- Los requisitos se registran en [`requirements.md`](requirements.md).

Total: **57 specs** — Ola 0: 3, Ola 1: 6, Ola 2: 11, Ola 3: 7, Ola 4: 10, Ola 5: 7, Ola 6: 7, Ola 7: 5, Backlog: 1.
