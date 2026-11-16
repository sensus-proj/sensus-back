# Manual de Conocimiento y Políticas Oficiales de Sensus (v1.0.0)

## 1. Identidad, Misión y Propósito de Sensus
Sensus es una plataforma móvil y comunitaria mexicana diseñada para conectar a personas consumidoras locales con micronegocios de proximidad (tiendas de barrio, misceláneas, panaderías, fruterías, carnicerías, papelerías, farmacias, ferreterías y pequeños supermercados locales).

En México, los micronegocios representan el 95.4 % de las unidades económicas (INEGI), y solo el 4.4 % vende en línea. Sensus resuelve esta brecha facilitando la digitalización hiperlocal de inventarios y ofertas para:
1. Reducir el desperdicio y mermas de productos perecederos o en liquidación en buen estado.
2. Aliviar la economía familiar mexicana ofreciendo promociones reales a corta distancia.
3. Fomentar el consumo local y el tejido social de los barrios y colonias mexicanas.

---

## 2. Cobertura Geográfica y Radio de Proximidad
- **Radio de descubrimiento estándar:** de 1 a 5 kilómetros a la redonda de la ubicación actual del usuario.
- **Ubicación GPS:** se solicita consentimiento explícito al usuario conforme a la ley mexicana. Las coordenadas se utilizan exclusivamente para filtrar comercios cercanos y calcular distancias estimadas.
- **Modo Invitado:** permite explorar ofertas en un radio acotado estricto de máximo 2 kilómetros en modo solo lectura. Los usuarios invitados no pueden apartar ofertas ni interactuar en la capa social ni usar a Sensi IA.
- **Zona prototipo inicial:** Tula de Allende, Hidalgo, expandible a cualquier municipio de la República Mexicana.

---

## 3. Modelo Operativo: Click & Collect con Código QR
El modelo operativo de Sensus es estrictamente **apartado presencial (*Click & Collect*)**.

### Flujo paso a paso del apartado:
1. **Descubrimiento:** El consumidor localiza una oferta activa con existencias disponibles en un comercio que se encuentre "Abierto ahora".
2. **Apartado:** El consumidor presiona "Apartar oferta". La aplicación reserva una unidad del inventario del comercio y genera un código QR único de un solo uso (conforme al estándar ISO/IEC 18004).
3. **Vigencia del apartado:** El apartado tiene una vigencia estándar de 24 horas (o hasta el horario de cierre del comercio si es menor). Si no se redime en ese lapso, expira automáticamente y la existencia regresa al catálogo del comercio.
4. **Visita y redención en tienda:** El consumidor acude físicamente al establecimiento, muestra el código QR desde su pantalla y el comerciante lo escanea con la cámara de su aplicación para verificar y redimir la oferta.
5. **Pago directo en establecimiento:** Sensus **NO** procesa pagos en línea, **NO** es pasarela de pago y **NO** cobra comisiones sobre las ventas. El pago se realiza directamente en caja del comercio mediante los métodos que este acepte (efectivo, tarjeta bancaria o transferencia electrónica).
6. **Sin envíos a domicilio:** Sensus **NO** cuenta con logística de repartidores ni realiza entregas a domicilio. Toda transacción es presencial entre vecino y comerciante.

---

## 4. Estado de Comercios y Distintivos
- **Abierto ahora / Cerrado ahora:** se calcula automáticamente en tiempo real comparando la hora local con el horario registrado por el comerciante para el día en curso.
- **Distintivo "Verificado":** acredita que la existencia física del comercio, su dirección y su giro comercial han sido validados por el equipo operativo de Sensus.

---

## 5. Modelo de Cuenta Unificada y Roles
Sensus opera con una cuenta única por persona:
- **Consumidor:** rol predeterminado al registrarse con correo electrónico y contraseña segura. Puede buscar ofertas, apartar con código QR, guardar favoritos y participar en la comunidad.
- **Activación de Comerciante ("Mi Tienda"):** cualquier consumidor registrado puede activar su perfil comercial desde la sección "Soy comerciante" o "Mi Tienda", ingresando el nombre comercial, dirección física, código postal, horario de atención y métodos de pago aceptados. Sensus no exige la carga de credenciales INE ni trámites burocráticos para iniciar.
- **Seguridad de cuentas:** autenticación mediante tokens JWT sin estado, cifrado de contraseñas con algoritmo BCrypt y revocación verificable de sesión.

---

## 6. Capa Social Comunitaria
- **Vinculación entre vecinos:** dos personas usuarias pueden vincularse escaneando mutuamente sus códigos de usuario QR sin revelar sus domicilios ni números telefónicos exactos.
- **Feed y recomendaciones:** permite a vecinos vinculados compartir ofertas encontradas, publicar recomendaciones de productos locales, dar "me gusta" y comentar publicaciones.
- **Moderación comunitaria:** todo contenido publicado (imágenes y texto) es moderado automáticamente previo a su difusión pública para garantizar un entorno libre de agresiones, armas, sustancias ilícitas o contenido inapropiado.

---

## 7. Políticas de Privacidad y Cumplimiento Legal (LFPDPPP Marzo 2025)
Sensus cumple cabalmente con la Ley Federal de Protección de Datos Personales en Posesión de los Particulares (LFPDPPP) de México:
- **Aviso de Privacidad integral:** visible y aceptado desde el formulario de registro.
- **Minimización de datos:** no se solicitan datos financieros sensibles (números de tarjeta bancaria, CVV ni cuentas).
- **Derechos ARCO y eliminación de cuenta:** todo usuario puede ejercer en cualquier momento sus derechos de Acceso, Rectificación, Cancelación y Oposición, así como solicitar la eliminación definitiva de su cuenta desde Ajustes. La eliminación purga de inmediato sus datos personales, ofertas asociadas, historial de apartados y conversaciones/trazas de Sensi IA.
- **Confidencialidad de trazas de IA:** los registros de conversación de Sensi IA son sometidos a redacción automática de datos personales (PII) y se destruyen al vencer su periodo de retención o al solicitar la baja de la cuenta.

---

## 8. Límites y Reglas Operativas
1. Cada consumidor puede tener un máximo de apartados activos simultáneos para evitar el acaparamiento injustificado de existencias.
2. Cada oferta tiene un límite de existencias definido por el comerciante; cuando llega a cero, la oferta se marca como agotada y no puede ser apartada ni sugerida por Sensi IA.
3. Se prohíbe la publicación de productos regulados, medicamentos controlados sin receta, armas, piratería o sustancias ilícitas.
4. Sensi IA es un orientador amigable y comercial; nunca inventa promociones, no altera precios fijados por los comerciantes y no puede realizar cobros en nombre de nadie.
