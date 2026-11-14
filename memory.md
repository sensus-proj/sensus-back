# Memoria del Proyecto — Sensus Backend

**Version:** 3.0.1 · **Actualizado:** 2026-11-14 · **Alineado a:** `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0

> Estado vivo del proyecto para reanudar trabajo sin perder contexto. No es normativo: si contradice a
> `constitution.md`, prevalece la constitución y este archivo se corrige.

| Campo | Valor |
|---|---|
| **Última actualización** | 2026-11-14 |
| **Fase SDD** | `implement` en curso para specs 007, 032 y 033 (ofertas cercanas, detalle con vistas y apartado con QR) en la rama `sprint3_oscar` (PR #14); y `specify → clarify → plan → tasks → checklist → implement` completado para Spec 018 (moderación multimodal con bandas y umbrales) en rama principal |
| **Rama de trabajo** | `sprint3_oscar` (hereda `Sprint1`); sin commits ni push de esta sesión; Spec 018 cambios fusionados desde main |
| **Agente** | OpenCode — DeepSeek V4.1 Flash |
| **Constitución vigente** | v1.3.0 (enmendada 2026-11-08) |
| **AGENTS.md vigente** | v1.3.0 |
| **Solución .NET** | `sensus-back.sln` (9 proyectos: `src` + TestKit, UnitTests, IntegrationTests, E2ETests, ArchitectureTests); `sensus-back.slnx` retirado |
| **Última tarea completada** | 2026-11-14, rama `sprint3_oscar_parciales` (sobre `origin/sprint3_oscar`, sin commit nuevo ni push): verificado y completado el commit `4f18b1d`. 008, 015, 016 y 019 quedan Implementado; 029 y 048 siguen Parcial. Hecho: fábrica de pruebas sin MongoDB real, `ComercioEndpoints.cs` muerto eliminado, pruebas de invalidación de caché de ofertas, métricas EMF `Sensus/Api` + alarma de throttling + panel CloudWatch (015), `POST /api/v1/media/{id}/release` solo Administrador con contrato OpenAPI (019), pruebas de `/auth/recover/confirm` (029). 613 pruebas en verde; cobertura Domain 90,2 % y Application 97,4 %; síntesis CDK correcta. Abierto: `cdk diff`; correlation-id (014); 029 caducidad 30 min, SES, invalidar tokens previos y límite por correo; 048 feed; rate limiting fijo en código (013); `POST /media/upload` devuelve siempre 201 aunque calcula 202; CI/CD 047. Siguiente paso: revisar el diff y decidir commit y PR hacia `sprint3_oscar`. — Anterior: Specs 007, 032 y 033 implementadas y verificadas (337 pruebas unitarias y 78 de integración en verde). Spec 018 implementada con 226 pruebas superadas. Specs 022 (media upload), 027 (device-token push) y 044 (notification settings) implementadas y verificadas. Endpoints: POST /api/v1/media/upload, PUT /api/v1/users/device-token, GET+PUT /api/v1/users/settings. specs/README.md y requirements.md actualizados. `var` reemplazado por tipos explícitos en todos los archivos nuevos (Domain, Application, Infrastructure, Api). Cobertura corregida: Sensus.Domain 87.62%, Sensus.Application 91.79% (ambas ≥ 80% umbral). 471 pruebas unitarias en verde, 4 de arquitectura en verde, `dotnet build -warnaserror` y `dotnet format --verify-no-changes` sin cambios. No hay secretos hardcodeados. |
| **Restricción activa** | Git autorizado **solo** para la rama `sprint3_oscar`; commit y push requieren confirmación explícita del responsable |