# ADR 0015 — Moderación de publicaciones de solo texto

- Fecha y aprobación: 2026-11-22, autorizada por el responsable del proyecto.
- Enmienda: MAJOR, `constitution.md` v3.0.0. Cambia la obligación de entrada del Principio IV para publicaciones sin imagen. Se mantiene la verificación de P-V y el techo de gasto de P-VI.
- Motivación: `CA-024-01` exige permitir publicaciones aptas de solo texto. La redacción anterior del §2.4 exigía una imagen en toda solicitud de moderación.
- Decisión: aplicar la política de Sensus con `gpt-4o-mini` al texto cuando una publicación de la Spec 024 carezca de imagen. Si tiene imagen, enviar ambas modalidades juntas antes de almacenar en Cloudinary o publicar. Un fallo, timeout, cuota agotada o respuesta inválida lleva a cuarentena en ambos casos.
- Impacto: la Spec 017 incorpora la variante de solo texto. La Spec 024 debe integrar ambas entradas y sus pruebas. La Spec 026 consume exclusivamente publicaciones con veredicto favorable y estado publicado. El adaptador de imágenes actual requiere admitir una entrada de solo texto mediante un contrato explícito. El flujo de subida que almacena antes de moderar requiere corrección antes de reutilizarlo.
- Costo: se usa el proveedor existente bajo EX-002, con techo agregado de 1 USD/mes. El costo unitario de la variante de texto se debe medir antes de habilitarla.
- Verificación: probar aprobación de texto apto, envío conjunto con imagen, rechazo conforme a política y cuarentena ante error, timeout, cuota agotada o respuesta malformada. Verificar que ningún fallo provoque publicación ni subida a Cloudinary.
