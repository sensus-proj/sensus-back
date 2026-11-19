# Spec 003 — Perfil del usuario autenticado

| Campo | Valor |
|---|---|
| ID | 003 |
| Slug | `perfil-usuario` |
| Estado | Implementado (retro-spec) |
| Ola | 1 |
| Tipo | Retro-spec |
| Dueño | Por asignar |
| Dependencias | 002, 022 |
| Versión | 1.1.0 |
| Alineado a | `constitution.md` v2.4.0 · `AGENTS.md` v2.4.0 |

---

## 1. Contexto y problema

Permitir que el usuario autenticado consulte y actualice su propio perfil —nombre y foto— sin poder alterar correo ni roles.

## 2. Alcance

**Incluye**

- Consulta del perfil con token válido
- Actualización de nombre completo y avatar
- Exposición de datos públicos únicamente
- Validación del avatar contra la spec 022

**No incluye**

- Cambio de correo
- Cambio de contraseña
- Consulta de perfiles ajenos
- Edición de datos comerciales (spec 031)

## 3. Actores

- Usuario autenticado

## 4. Requisitos funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| FR-003-01 | Devolver el perfil del usuario autenticado | Must |
| FR-003-02 | Excluir passwordHash y datos sensibles de la respuesta | Must |
| FR-003-03 | Responder 401 sin token o con token inválido | Must |
| FR-003-04 | Actualizar nombre completo y avatar del propio usuario | Must |
| FR-003-05 | Rechazar cambios de correo o roles desde este endpoint | Must |
| FR-003-06 | Aceptar solo avatares subidos por el propio usuario y aprobados | Must |
| FR-003-07 | Emitir parámetros firmados (`POST /users/photo-signature`) para subir la foto de perfil a Cloudinary, limitados a la carpeta `sensus/profiles/{userId}`; solo usuarios registrados | Must |
| FR-003-08 | Moderar la foto de perfil antes de publicarla o guardarla en el perfil (Principio IV); hoy la app sube directo a Cloudinary con la firma y nada la modera | Must |
| FR-003-09 | Limitar la tasa de emisión de firmas (`sensitive-write`) | Should |

## 5. Requisitos no funcionales

| ID | Requisito | Métrica objetivo |
|---|---|---|
| NFR-003-01 | Latencia de consulta | p95 < 300 ms |
| NFR-003-02 | Latencia de actualización | p95 < 500 ms |

## 6. Criterios de aceptación

### CA-003-01

- **Dado** token válido
- **Cuando** GET /api/v1/users/profile
- **Entonces** 200 con perfil y roles

### CA-003-02

- **Dado** sin token o token expirado
- **Cuando** GET /api/v1/users/profile
- **Entonces** 401

### CA-003-03

- **Dado** nombre y avatar válidos
- **Cuando** PUT /api/v1/users/profile
- **Entonces** 200 con el perfil actualizado

### CA-003-04

- **Dado** intento de cambiar el correo
- **Cuando** PUT /api/v1/users/profile
- **Entonces** 400 o campo ignorado explícitamente

### CA-003-05

- **Dado** avatar de otro usuario
- **Cuando** PUT /api/v1/users/profile
- **Entonces** 403

### CA-003-06

- **Dado** un usuario registrado
- **Cuando** llama a `POST /api/v1/users/photo-signature`
- **Entonces** recibe `200` con `cloudName`, `apiKey`, `timestamp`, `folder = sensus/profiles/{userId}` y `signature`, sin el secreto de Cloudinary

### CA-003-07

- **Dado** un invitado o una solicitud sin token
- **Cuando** llama al endpoint
- **Entonces** recibe `403` o `401`

### CA-003-08 (pendiente, FR-003-06 y FR-003-08)

- **Dado** una `fotoPerfilUrl` de otro dominio (`https://malcloudinary.com/...`), de otra cuenta de Cloudinary o de otra carpeta
- **Cuando** se actualiza el perfil
- **Entonces** recibe `400`; y una imagen inapropiada nunca queda como foto de perfil

## 7. Contratos expuestos

| Método | Ruta | Autorización |
|---|---|---|
| GET | `/api/v1/users/profile` | JWT |
| PUT | `/api/v1/users/profile` | JWT |
| POST | `/api/v1/users/photo-signature` | JWT de usuario registrado |

## 8. Dependencias y supuestos

- Depende de: 002, 022
- El comportamiento descrito aplica a la versión v1 de la API.
- Los secretos y cadenas de conexión se resuelven por configuración de entorno, nunca en código.

## 9. Riesgos

- El DTO podría divergir del documento si se expone la entidad directamente

## 10. Preguntas abiertas

- Ninguna.

## 11. Trazabilidad

- Requisitos de este spec: `FR-003-01`, `FR-003-02`, `FR-003-03`, `FR-003-04`, `FR-003-05`, `FR-003-06`, `FR-003-07`, `FR-003-08`, `FR-003-09`
- Plan: [`plan.md`](plan.md)
- Tareas: [`tasks.md`](tasks.md)
- Checklist: [`checklist.md`](checklist.md)
