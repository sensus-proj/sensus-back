# ADR 0014 — Redis (Valkey Serverless) en VPC y pausa nocturna de la infraestructura

| Campo | Valor |
|---|---|
| Estado | Aceptada |
| Fecha | 2026-11-19 |
| Decisor | Rodrigo Sánchez (responsable del proyecto) |
| Specs | 048 (FR-048-14), 051 |
| Reemplaza parcialmente | ADR 0013 (en lo que respecta a mantener Redis apagado) |

## Contexto

Redis estaba apagado en producción porque ElastiCache exige VPC y la Lambda necesita salir a internet (MongoDB
Atlas, Cloudinary, OpenAI, Firebase), lo que obliga a un NAT con costo por hora. La caché en memoria (FR-048-13)
mitiga, pero no se comparte entre instancias. El responsable pidió activar Redis y aprobó el costo con los créditos
de AWS disponibles, y pidió pausar la infraestructura de 22:30 a 07:00, cuando no hay tráfico.

## Decisión

1. **ElastiCache Serverless con motor Valkey 8** (compatible con el protocolo de Redis, sin cambios en
   `StackExchange.Redis`). Su mínimo facturable es 100 MB, frente a 1 GB de Redis OSS Serverless. Límites de
   1 GB y 5000 ECPU/s para acotar el costo. TLS obligatorio; acceso solo desde el grupo de seguridad de la API.
2. **VPC de dos zonas sin NAT fijo**: subredes públicas y privadas aisladas; endpoint gratuito de DynamoDB.
3. **NAT Gateway administrado** (opción A, frente a una instancia NAT más barata que habría que parchear y que
   sería un punto único de falla, en contra del Principio VIII).
4. **Pausa nocturna** con EventBridge Scheduler y una función Python: de noche se corta el tráfico de la API, se
   silencian las alarmas y se borran NAT e IP elástica; de día se recrean. La caché Valkey no se pausa (pausarla
   implica borrarla y cambiar su endpoint).
5. La caché en memoria (FR-048-13) sigue como respaldo si Valkey no responde.

## Alternativas descartadas

| Alternativa | Motivo |
|---|---|
| Redis OSS Serverless | Mínimo de 1 GB, ≈ 90 USD/mes |
| `cache.t4g.micro` | Costo similar y capacidad fija; Serverless escala y se acota con límites |
| Instancia NAT `t4g.nano` | Más barata, pero la mantiene el equipo y es un punto único de falla |
| Redis de un proveedor externo | Viola la regla de una sola nube (§2.2) |
| Mantener Redis apagado | El responsable pidió activarlo |

## Consecuencias

- Costo aproximado de 29 a 32 USD/mes con la pausa (≈ 43 USD sin ella), con créditos de AWS. Excepción EX-003.
- La API no está disponible de 22:30 a 07:00.
- La IP de salida cambia cada día: MongoDB Atlas no puede filtrar por una IP fija.
- Valkey Serverless no usa autenticación de usuarios en esta versión: el aislamiento depende de la red y de TLS
  (brecha G-14 de la constitución).
- El despliegue debe hacerse por CI: un `cdk deploy` local borraría los secretos de la Lambda.
