# ADR 0010 — Adopción de Cloudinary para almacenamiento, transformación y entrega CDN de multimedia

| Campo | Valor |
|---|---|
| Estado | Aceptada |
| Fecha | 2026-11-08 |
| Principios afectados | II, IV, VI |
| Specs relacionados | 022, 023 |
| Enmienda constitucional | `constitution.md` v1.3.0 (§2.1, §2.2, P-VI) |

## Contexto

El diseño original contemplaba Amazon S3 con buckets privados y Amazon CloudFront con URLs firmadas para el almacenamiento y distribución de multimedia. Sin embargo:
1. El backend requiere transformaciones dinámicas, generación de firmas de subida segura directa desde el cliente móvil/web, extracción de metadatos y entrega optimizada de formatos de imagen modernos (WebP, AVIF) según el dispositivo solicitante.
2. La implementación en Sprint 1 ya cuenta con adaptadores probados y operativos para Cloudinary (`IProfileImageStorage` / `CloudinaryImageStorage`), firmas criptográficas SHA-256 (`GenerateUploadSignature`) y eliminación remota (`DeleteImageAsync`).
3. El plan gratuito de Cloudinary proporciona 25 créditos mensuales (~25 GB de almacenamiento gestionado o 25 000 transformaciones), lo cual satisface holgadamente el requisito de costo 0 USD sin incurrir en la sobrecarga de configurar y mantener pipelines de procesamiento de imágenes con Lambda + S3 + CloudFront.

## Decisión

- Adoptar oficialmente **Cloudinary** como el servicio de almacenamiento, transformación y CDN de contenidos multimedia para la plataforma Sensus.
- Mantener la abstracción desacoplada en `Sensus.Application` e implementada en `Sensus.Infrastructure` (`CloudinaryImageStorage`), de modo que el dominio y los casos de uso desconozcan los detalles del SDK o API REST de Cloudinary.
- Utilizar firmas SHA-256 en el backend (`api_secret`) para autorizar subidas seguras y restringidas por carpeta/usuario sin exponer credenciales maestras.
- Declarar a Cloudinary como una excepción autorizada a la política de no multi-nube en `constitution.md` §2.1 y §2.2 (junto con OpenAI y FCM/SNS).
- Derogar la necesidad de aprovisionar buckets S3 y distribuciones CloudFront en AWS CDK para multimedia, simplificando la infraestructura como código.

## Consecuencias

**Positivas**
- Transformación, compresión inteligente y miniaturas automáticas sin necesidad de funciones Lambda adicionales.
- Entrega global acelerada por CDN integrada con URLs semánticas y seguras.
- Código ya existente y testeado en Sprint 1 totalmente validado contra el SDD.
- Costo 0 USD dentro del plan gratuito mensual de Cloudinary.

**Negativas**
- Dependencia externa fuera de la nube AWS (mitigada por la abstracción de interfaz `IProfileImageStorage` / `IMediaStorage`).
- Límite de créditos gratuitos que exige monitorización de cuota de uso.

**Mitigación**
- Se mantiene la interfaz abstracta en `Application`; un cambio futuro de proveedor solo requiere un adaptador nuevo en `Infrastructure`.
- Las credenciales (`ApiKey`, `ApiSecret`, `CloudName`) se inyectan estrictamente por configuración segura (`appsettings` / variables de entorno / Secrets Manager) y nunca se versionan en el repositorio.

## Alternativas consideradas

| Alternativa | Motivo del descarte |
|---|---|
| Amazon S3 + CloudFront | Requiere funciones Lambda complejas para redimensionamiento, firma de URLs y mayor configuración de IaC |
| Almacenamiento local en disco | Prohibido en arquitecturas serverless / contenedores efímeros |
| Carga directa al servidor sin CDN | Sobrecarga el ancho de banda del backend y degrada la experiencia móvil |

## Referencias

- `constitution.md` v1.3.0 (§2.1, §2.2, P-VI)
- `docs/architecture/overview.md`
- `specs/022-carga-multimedia/`
- `specs/023-entrega-cdn/`
