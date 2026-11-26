#!/usr/bin/env node
/**
 * Prueba de humo posterior al despliegue (Spec 045, FR-045-16).
 *
 * Comprueba que la API desplegada arranca y enruta: /health/live y /health/ready deben responder 200 y una ruta
 * protegida sin token debe responder 401. Reintenta el conjunto completo para tolerar el arranque en frío.
 * Solo registra rutas y códigos HTTP; nunca cuerpos de respuesta.
 *
 * Uso:
 *   node .github/scripts/post-deploy-smoke.mjs --base-url https://<api>.execute-api.<región>.amazonaws.com
 *     [--attempts 6] [--delay-ms 10000] [--timeout-ms 10000]
 *
 * Sale con código 0 si todas las comprobaciones pasan en algún intento y con 1 en caso contrario.
 */

import fs from 'node:fs';

const SMOKE_CHECKS = [
  { path: '/health/live', expected: [200], description: 'La API arranca (liveness)' },
  { path: '/health/ready', expected: [200], description: 'Dependencias listas (readiness)' },
  { path: '/api/v1/offers/nearby', expected: [401], description: 'Ruta protegida exige autenticación' }
];

function getArg(args, flag, defaultValue) {
  const idx = args.indexOf(flag);
  return idx !== -1 && args[idx + 1] ? args[idx + 1] : defaultValue;
}

function parsePositiveInt(value, name, min, max) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < min || parsed > max) {
    throw new Error(`${name} debe ser un entero entre ${min} y ${max}.`);
  }
  return parsed;
}

/**
 * Solo HTTPS. HTTP únicamente hacia localhost/127.0.0.1 con SMOKE_ALLOW_LOCAL=true (pruebas del script).
 */
function resolveBaseUrl(rawUrl, allowLocal) {
  if (!rawUrl || !rawUrl.trim()) {
    throw new Error('--base-url es obligatorio.');
  }
  let parsed;
  try {
    parsed = new URL(rawUrl.trim());
  } catch {
    throw new Error('--base-url no tiene un formato de URL válido.');
  }
  if (parsed.username || parsed.password || parsed.search || parsed.hash) {
    throw new Error('--base-url no puede incluir credenciales, query strings ni fragmentos.');
  }
  const isLocal = parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1';
  if (parsed.protocol === 'https:' || (allowLocal && isLocal && parsed.protocol === 'http:')) {
    return parsed.toString().replace(/\/+$/, '');
  }
  throw new Error('--base-url debe usar HTTPS.');
}

async function runCheck(baseUrl, check, timeoutMs) {
  try {
    const response = await fetch(`${baseUrl}${check.path}`, {
      method: 'GET',
      redirect: 'manual',
      signal: AbortSignal.timeout(timeoutMs)
    });
    // Se descarta el cuerpo sin leerlo ni registrarlo.
    await response.body?.cancel();
    return { ...check, status: response.status, passed: check.expected.includes(response.status) };
  } catch (err) {
    const reason = err?.name === 'TimeoutError' ? 'timeout' : 'error de red';
    return { ...check, status: null, passed: false, error: reason };
  }
}

async function runSmokeTest({ baseUrl, attempts, delayMs, timeoutMs, log = console.log }) {
  let lastResults = [];
  for (let attempt = 1; attempt <= attempts; attempt++) {
    lastResults = [];
    for (const check of SMOKE_CHECKS) {
      lastResults.push(await runCheck(baseUrl, check, timeoutMs));
    }
    const summary = lastResults
      .map((r) => `${r.path}=${r.status ?? r.error}${r.passed ? '' : ` (esperado ${r.expected.join('/')})`}`)
      .join(' | ');
    log(`Intento ${attempt}/${attempts}: ${summary}`);

    if (lastResults.every((r) => r.passed)) {
      return { passed: true, attempts: attempt, results: lastResults };
    }
    if (attempt < attempts) {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
  }
  return { passed: false, attempts, results: lastResults };
}

function writeStepSummary(outcome, baseHost) {
  if (!process.env.GITHUB_STEP_SUMMARY) return;
  const rows = outcome.results
    .map(
      (r) =>
        `| \`${r.path}\` | ${r.description} | ${r.expected.join(' / ')} | ${r.status ?? r.error} | ${r.passed ? 'PASS' : 'FAIL'} |`
    )
    .join('\n');
  const md = `### Prueba de humo posterior al despliegue (FR-045-16)

- **Host:** \`${baseHost}\`
- **Resultado:** ${outcome.passed ? 'APROBADA' : 'FALLIDA'} tras ${outcome.attempts} intento(s)

| Ruta | Comprobación | Esperado | Obtenido | Estado |
|---|---|---|---|---|
${rows}
`;
  fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, md, 'utf8');
}

async function main() {
  const args = process.argv.slice(2);
  let baseUrl;
  let attempts;
  let delayMs;
  let timeoutMs;
  try {
    baseUrl = resolveBaseUrl(getArg(args, '--base-url', ''), process.env.SMOKE_ALLOW_LOCAL === 'true');
    attempts = parsePositiveInt(getArg(args, '--attempts', '6'), '--attempts', 1, 20);
    delayMs = parsePositiveInt(getArg(args, '--delay-ms', '10000'), '--delay-ms', 0, 60000);
    timeoutMs = parsePositiveInt(getArg(args, '--timeout-ms', '10000'), '--timeout-ms', 100, 29000);
  } catch (err) {
    console.error(`ERROR: ${err.message}`);
    process.exit(1);
  }

  const baseHost = new URL(baseUrl).host;
  console.log(`Prueba de humo contra ${baseHost} (${attempts} intentos, espera ${delayMs} ms)`);
  const outcome = await runSmokeTest({ baseUrl, attempts, delayMs, timeoutMs });
  writeStepSummary(outcome, baseHost);

  if (!outcome.passed) {
    console.error('PRUEBA DE HUMO FALLIDA: la API desplegada no responde como se espera.');
    process.exit(1);
  }
  console.log('PRUEBA DE HUMO APROBADA.');
}

await main();
