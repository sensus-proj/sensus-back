#!/usr/bin/env node
/**
 * Prueba de carga baja controlada para validar NFR de latencia en producción diurna (G-13 / D4).
 *
 * Restricciones obligatorias:
 * - Tasa máxima estricta: 5 rps (--rps entre 1 y 5).
 * - Nunca invoca rutas /api/v1/ai/* (protección de presupuesto OpenAI).
 * - Solo HTTPS (HTTP en localhost/127.0.0.1 solo con LOAD_ALLOW_LOCAL=true en pruebas).
 * - Nunca imprime tokens, cabeceras de autorización ni cuerpos de respuesta HTTP.
 *
 * Uso (en horario diurno 07:00-22:30 America/Mexico_City):
 *   node .github/scripts/load-smoke.mjs --base-url https://zqqdwovx81.execute-api.us-east-2.amazonaws.com \
 *     --rps 5 --duration-sec 120 --max-p95-ms 700
 */

import fs from 'node:fs';
import { performance } from 'node:perf_hooks';

const DEFAULT_PATHS = [
  { path: '/health/ready', auth: false },
  { path: '/api/v1/offers/nearby?lat=19.4326&lng=-99.1332&radiusKm=2', auth: true },
  { path: '/api/v1/feed/nearby?lat=19.4326&lng=-99.1332&radiusKm=2', auth: true }
];

const MAX_ALLOWED_RPS = 5;
const FORBIDDEN_AI_PREFIX = '/api/v1/ai/';

function getArg(args, flag, defaultValue) {
  const idx = args.indexOf(flag);
  return idx !== -1 && args[idx + 1] !== undefined ? args[idx + 1] : defaultValue;
}

function parseIntInRange(raw, name, min, max) {
  const val = Number(raw);
  if (!Number.isInteger(val) || val < min || val > max) {
    throw new Error(`${name} debe ser un entero entre ${min} y ${max} (recibido: ${raw}).`);
  }
  return val;
}

function parseNumberInRange(raw, name, min, max) {
  const val = Number(raw);
  if (!Number.isFinite(val) || val < min || val > max) {
    throw new Error(`${name} debe ser un número entre ${min} y ${max} (recibido: ${raw}).`);
  }
  return val;
}

export function resolveBaseUrl(rawUrl, allowLocal) {
  if (!rawUrl || !rawUrl.trim()) {
    throw new Error('--base-url es obligatorio.');
  }
  let parsed;
  try {
    parsed = new URL(rawUrl.trim());
  } catch {
    throw new Error('--base-url no tiene formato de URL válido.');
  }
  if (parsed.username || parsed.password || parsed.search || parsed.hash) {
    throw new Error('--base-url no puede contener credenciales, query ni fragmentos.');
  }
  const isLocal = parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1';
  if (parsed.protocol === 'https:' || (allowLocal && isLocal && parsed.protocol === 'http:')) {
    return parsed.toString().replace(/\/+$/, '');
  }
  throw new Error('--base-url debe usar HTTPS.');
}

export function resolveRoutes(rawPaths) {
  if (!rawPaths) {
    return DEFAULT_PATHS;
  }
  const items = rawPaths
    .split(',')
    .map((p) => p.trim())
    .filter(Boolean);

  if (items.length === 0) {
    throw new Error('--paths no puede estar vacío.');
  }

  return items.map((routePath) => {
    const normalized = routePath.startsWith('/') ? routePath : `/${routePath}`;
    if (
      normalized.toLowerCase().includes(FORBIDDEN_AI_PREFIX) ||
      normalized.toLowerCase() === '/api/v1/ai'
    ) {
      throw new Error(`Ruta prohibida en carga (${FORBIDDEN_AI_PREFIX}*): ${normalized}`);
    }
    return {
      path: normalized,
      auth: !normalized.startsWith('/health')
    };
  });
}

export function percentile(sortedValues, pct) {
  if (sortedValues.length === 0) {
    return 0;
  }
  const rank = Math.ceil((pct / 100) * sortedValues.length);
  const index = Math.min(Math.max(rank - 1, 0), sortedValues.length - 1);
  return sortedValues[index];
}

async function fetchGuestToken(baseUrl, timeoutMs) {
  const response = await fetch(`${baseUrl}/api/v1/auth/guest`, {
    method: 'POST',
    redirect: 'manual',
    signal: AbortSignal.timeout(timeoutMs)
  });
  if (!response.ok) {
    await response.body?.cancel();
    throw new Error(`No se pudo obtener token de invitado en /api/v1/auth/guest (HTTP ${response.status}).`);
  }
  const payload = await response.json();
  if (!payload || typeof payload.token !== 'string' || !payload.token.trim()) {
    throw new Error('La respuesta de /api/v1/auth/guest no devolvió un token válido.');
  }
  return payload.token;
}

function summarizeSamples(samples) {
  const durations = samples.map((s) => s.durationMs).sort((a, b) => a - b);
  const total = samples.length;
  const okCount = samples.filter((s) => s.ok).length;
  const rateLimited = samples.filter((s) => s.status === 429).length;
  const errorCount = total - okCount;
  const errorPct = total === 0 ? 100 : (errorCount / total) * 100;

  return {
    total,
    okCount,
    errorCount,
    rateLimited,
    errorPct,
    minMs: durations[0] ?? 0,
    p50Ms: percentile(durations, 50),
    p95Ms: percentile(durations, 95),
    p99Ms: percentile(durations, 99),
    maxMs: durations[durations.length - 1] ?? 0
  };
}

async function main() {
  const args = process.argv.slice(2);
  const allowLocal = process.env.LOAD_ALLOW_LOCAL === 'true';

  const baseUrl = resolveBaseUrl(getArg(args, '--base-url', ''), allowLocal);
  const rps = parseIntInRange(getArg(args, '--rps', '5'), '--rps', 1, MAX_ALLOWED_RPS);
  const durationSec = parseIntInRange(getArg(args, '--duration-sec', '120'), '--duration-sec', 1, 300);
  const timeoutMs = parseIntInRange(getArg(args, '--timeout-ms', '5000'), '--timeout-ms', 200, 30000);
  const maxP95Ms = parseNumberInRange(getArg(args, '--max-p95-ms', '700'), '--max-p95-ms', 1, 30000);
  const maxErrorPct = parseNumberInRange(getArg(args, '--max-error-pct', '1'), '--max-error-pct', 0, 100);
  const routes = resolveRoutes(getArg(args, '--paths', ''));
  const outPath = getArg(args, '--out', '');

  let bearerToken = process.env.SENSUS_BEARER_TOKEN?.trim() || null;
  if (!bearerToken && routes.some((r) => r.auth)) {
    bearerToken = await fetchGuestToken(baseUrl, timeoutMs);
  }

  const totalRequests = rps * durationSec;
  const intervalMs = 1000 / rps;
  const startTime = performance.now();
  const samples = [];

  for (let i = 0; i < totalRequests; i++) {
    const scheduledMs = startTime + i * intervalMs;
    const waitMs = scheduledMs - performance.now();
    if (waitMs > 1) {
      await new Promise((resolve) => setTimeout(resolve, waitMs));
    }

    const route = routes[i % routes.length];
    const headers = route.auth && bearerToken ? { Authorization: `Bearer ${bearerToken}` } : undefined;
    const reqStart = performance.now();
    try {
      const response = await fetch(`${baseUrl}${route.path}`, {
        method: 'GET',
        headers,
        redirect: 'manual',
        signal: AbortSignal.timeout(timeoutMs)
      });
      await response.body?.cancel();
      const durationMs = performance.now() - reqStart;
      samples.push({
        path: route.path,
        status: response.status,
        ok: response.status >= 200 && response.status < 300,
        durationMs
      });
    } catch {
      const durationMs = performance.now() - reqStart;
      samples.push({
        path: route.path,
        status: 0,
        ok: false,
        durationMs
      });
    }
  }

  const globalSummary = summarizeSamples(samples);
  const lines = [
    `# Resultados de carga controlada (rps=${rps}, duracion=${durationSec}s)`,
    `Global: total=${globalSummary.total} ok=${globalSummary.okCount} errors=${globalSummary.errorCount} (errPct=${globalSummary.errorPct.toFixed(2)}%) 429=${globalSummary.rateLimited} p50=${globalSummary.p50Ms.toFixed(1)}ms p95=${globalSummary.p95Ms.toFixed(1)}ms p99=${globalSummary.p99Ms.toFixed(1)}ms`
  ];

  for (const route of routes) {
    const perRoute = summarizeSamples(samples.filter((s) => s.path === route.path));
    lines.push(
      `Ruta ${route.path}: total=${perRoute.total} ok=${perRoute.okCount} errors=${perRoute.errorCount} 429=${perRoute.rateLimited} p50=${perRoute.p50Ms.toFixed(1)}ms p95=${perRoute.p95Ms.toFixed(1)}ms p99=${perRoute.p99Ms.toFixed(1)}ms`
    );
  }

  console.log(lines.join('\n'));
  if (outPath) {
    fs.writeFileSync(outPath, `${lines.join('\n')}\n`, 'utf8');
  }

  const passed = globalSummary.errorPct <= maxErrorPct && globalSummary.p95Ms <= maxP95Ms;
  if (!passed) {
    console.error(
      `UMBRAL INCUMPLIDO: p95=${globalSummary.p95Ms.toFixed(1)}ms (max ${maxP95Ms}ms), error=${globalSummary.errorPct.toFixed(2)}% (max ${maxErrorPct}%).`
    );
    process.exit(1);
  }
}

const isMain = process.argv[1] && import.meta.url === new URL(`file://${process.argv[1].replace(/\\/g, '/')}`).href;
if (isMain) {
  main().catch((err) => {
    console.error(err.message);
    process.exit(1);
  });
}
