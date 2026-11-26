import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const SCRIPT_PATH = path.resolve('.github/scripts/post-deploy-smoke.mjs');

async function runScript(args, envOverrides = {}) {
  const env = { ...process.env, SMOKE_ALLOW_LOCAL: 'true', ...envOverrides };
  delete env.GITHUB_STEP_SUMMARY;
  if (envOverrides.GITHUB_STEP_SUMMARY) env.GITHUB_STEP_SUMMARY = envOverrides.GITHUB_STEP_SUMMARY;
  try {
    const res = await execFileAsync(process.execPath, [SCRIPT_PATH, ...args], { env });
    return { code: 0, stdout: res.stdout, stderr: res.stderr };
  } catch (err) {
    return { code: err.code ?? 1, stdout: err.stdout ?? '', stderr: err.stderr ?? '' };
  }
}

/**
 * Servidor que imita la API. `statusFor(path, attempt)` decide el código; `attempt` cuenta las llamadas a /health/live.
 */
async function startApi(statusFor) {
  let liveCalls = 0;
  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/health/live') liveCalls++;
    const status = statusFor(url.pathname, liveCalls);
    res.writeHead(status, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ secret: 'no-debe-aparecer-en-logs' }));
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return {
    baseUrl: `http://127.0.0.1:${server.address().port}`,
    getLiveCalls: () => liveCalls,
    close: () => new Promise((resolve) => server.close(resolve))
  };
}

const healthy = (p) => (p.startsWith('/health') ? 200 : 401);
const FAST = ['--attempts', '3', '--delay-ms', '0', '--timeout-ms', '2000'];

test('1. API sana: 200 en health y 401 en ruta protegida aprueba en el primer intento', async () => {
  const api = await startApi(healthy);
  try {
    const result = await runScript(['--base-url', api.baseUrl, ...FAST]);
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /Intento 1\/3/);
    assert.match(result.stdout, /PRUEBA DE HUMO APROBADA/);
    assert.equal(api.getLiveCalls(), 1);
  } finally {
    await api.close();
  }
});

test('2. Arranque en frío: falla dos intentos y aprueba en el tercero', async () => {
  const api = await startApi((p, attempt) => (attempt < 3 ? 503 : healthy(p)));
  try {
    const result = await runScript(['--base-url', api.baseUrl, ...FAST]);
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /Intento 3\/3/);
  } finally {
    await api.close();
  }
});

test('3. Incidente del 2026-11-25: la API responde 500 en todo y la prueba falla tras agotar intentos', async () => {
  const api = await startApi(() => 500);
  try {
    const result = await runScript(['--base-url', api.baseUrl, ...FAST]);
    assert.notEqual(result.code, 0);
    assert.match(result.stderr, /PRUEBA DE HUMO FALLIDA/);
    assert.equal(api.getLiveCalls(), 3);
  } finally {
    await api.close();
  }
});

test('4. La ruta protegida respondiendo 200 sin token reprueba (autenticación rota)', async () => {
  const api = await startApi(() => 200);
  try {
    const result = await runScript(['--base-url', api.baseUrl, ...FAST]);
    assert.notEqual(result.code, 0);
    assert.match(result.stdout, /\/api\/v1\/offers\/nearby=200 \(esperado 401\)/);
  } finally {
    await api.close();
  }
});

test('5. Readiness en 503 (base de datos caída) reprueba aunque liveness responda 200', async () => {
  const api = await startApi((p) => (p === '/health/ready' ? 503 : healthy(p)));
  try {
    const result = await runScript(['--base-url', api.baseUrl, ...FAST]);
    assert.notEqual(result.code, 0);
    assert.match(result.stdout, /\/health\/ready=503/);
  } finally {
    await api.close();
  }
});

test('6. Nunca registra cuerpos de respuesta y escribe el resumen del job', async () => {
  const api = await startApi(healthy);
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'smoke-summary-'));
  const summaryPath = path.join(dir, 'summary.md');
  try {
    const result = await runScript(['--base-url', api.baseUrl, ...FAST], { GITHUB_STEP_SUMMARY: summaryPath });
    assert.equal(result.code, 0, result.stderr);
    assert.doesNotMatch(result.stdout + result.stderr, /no-debe-aparecer-en-logs/);
    const summary = fs.readFileSync(summaryPath, 'utf8');
    assert.match(summary, /Prueba de humo posterior al despliegue \(FR-045-16\)/);
    assert.match(summary, /APROBADA/);
    assert.doesNotMatch(summary, /no-debe-aparecer-en-logs/);
  } finally {
    await api.close();
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('7. Rechaza URL sin HTTPS, con credenciales o vacía sin hacer solicitudes', async () => {
  const api = await startApi(healthy);
  try {
    const plainHttp = await runScript(['--base-url', 'http://api.example.com', ...FAST]);
    assert.notEqual(plainHttp.code, 0);
    assert.match(plainHttp.stderr, /HTTPS/);

    const localWithoutFlag = await runScript(['--base-url', api.baseUrl, ...FAST], { SMOKE_ALLOW_LOCAL: 'false' });
    assert.notEqual(localWithoutFlag.code, 0);
    assert.equal(api.getLiveCalls(), 0);

    const withCredentials = await runScript(['--base-url', 'https://user:pass@api.example.com', ...FAST]);
    assert.notEqual(withCredentials.code, 0);
    assert.match(withCredentials.stderr, /credenciales/);

    const empty = await runScript([...FAST]);
    assert.notEqual(empty.code, 0);
    assert.match(empty.stderr, /--base-url es obligatorio/);
  } finally {
    await api.close();
  }
});

test('8. Servidor que no responde: el tiempo límite cuenta como fallo', async () => {
  const server = http.createServer(() => {
    /* nunca responde */
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  try {
    const baseUrl = `http://127.0.0.1:${server.address().port}`;
    const result = await runScript(['--base-url', baseUrl, '--attempts', '1', '--delay-ms', '0', '--timeout-ms', '300']);
    assert.notEqual(result.code, 0);
    assert.match(result.stdout, /\/health\/live=timeout/);
  } finally {
    server.closeAllConnections();
    await new Promise((resolve) => server.close(resolve));
  }
});
