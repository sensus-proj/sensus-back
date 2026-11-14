# Memoria del Proyecto — Sensus Backend

**Version:** 3.0.0 · **Actualizado:** 2026-11-13 · **Alineado a:** `constitution.md` v1.3.0 · `AGENTS.md` v1.3.0

> Estado vivo del proyecto para reanudar trabajo sin perder contexto. No es normativo: si contradice a
> `constitution.md`, prevalece la constitución y este archivo se corrige.

| Campo | Valor |
|---|---|
| **Última actualización** | 2026-11-13 |
| **Fase SDD** | `implement` en curso para specs 007, 032 y 033 (ofertas cercanas, detalle con vistas y apartado con QR) en la rama `sprint3_oscar` (PR #14); y `specify → clarify → plan → tasks → checklist → implement` completado para Spec 018 (moderación multimodal con bandas y umbrales) en rama principal |
| **Rama de trabajo** | `sprint3_oscar` (hereda `Sprint1`); sin commits ni push de esta sesión; Spec 018 cambios fusionados desde main |
| **Agente** | OpenCode — DeepSeek V4.1 Flash |
| **Constitución vigente** | v1.3.0 (enmendada 2026-11-08) |
| **AGENTS.md vigente** | v1.3.0 |
| **Solución .NET** | `sensus-back.sln` (9 proyectos: `src` + TestKit, UnitTests, IntegrationTests, E2ETests, ArchitectureTests); `sensus-back.slnx` retirado |
| **Última tarea completada** | Specs 007, 032 y 033 implementadas y verificadas (337 pruebas unitarias y 78 de integración en verde). Spec 018 implementada con 226 pruebas superadas. Specs 022 (media upload), 027 (device-token push) y 044 (notification settings) implementadas y verificadas. Endpoints: POST /api/v1/media/upload, PUT /api/v1/users/device-token, GET+PUT /api/v1/users/settings. specs/README.md y requirements.md actualizados. `var` reemplazado por tipos explícitos en todos los archivos nuevos (Domain, Application, Infrastructure, Api). Cobertura corregida: Sensus.Domain 87.62%, Sensus.Application 91.79% (ambas ≥ 80% umbral). 471 pruebas unitarias en verde, 4 de arquitectura en verde, `dotnet build -warnaserror` y `dotnet format --verify-no-changes` sin cambios. No hay secretos hardcodeados. |
| **Restricción activa** | Git autorizado **solo** para la rama `sprint3_oscar`; commit y push requieren confirmación explícita del responsable |