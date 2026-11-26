import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const SCRIPT_PATH = path.resolve('.github/scripts/load-smoke.mjs');

async function runLoadScript(args, envOverrides = {}) {
  const env = { ...process.env, LOAD_ALLOW_LOCAL: 'true', ...envOverrides };
  try {
    const res = await execFileAsync(process.execPath, [SCRIPT_PATH, ...args], { env });
    return { code: 0, stdout: res.stdout, stderr: res.stderr };
  } catch (err) {
    return { code: err.code ?? 1, stdout: err.stdout ?? '', stderr: err.stderr ?? '' };
  }
}

async function startMockApi(handler) {
  let requests = 0;
  let guestRequests = 0;
  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    requests++;
    if (url.pathname === '/api/v1/auth/guest' && req.method === 'POST') {
      guestRequests++;
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ token: 'guest-token', tokenType: 'Bearer' }));
      return;
    }
    const status = handler ? handler(url.pathname) : 200;
    res.writeHead(status, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ ok: status === 200 }));
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return {
    baseUrl: `http://127.0.0.1:${server.address().port}`,
    getRequests: () => requests,
    getGuestRequests: () => guestRequests,
    close: () => new Promise((resolve) => server.close(resolve))
  };
}

test('1. Rechaza explícitamente rutas /api/v1/ai/* sin enviar peticiones (protección OpenAI)', async () => {
  const api = await startMockApi();
  try {
    const res = await runLoadScript([
      '--base-url', api.baseUrl,
      '--rps', '2',
      '--duration-sec', '1',
      '--paths', '/api/v1/ai/conversations'
    ]);
    assert.notEqual(res.code, 0);
    assert.match(res.stderr, /\/api\/v1\/ai\//);
    assert.equal(api.getRequests(), 0);
  } finally {
    await api.close();
  }
});

test('2. Rechaza rps mayor a 5 sin enviar peticiones', async () => {
  const api = await startMockApi();
  try {
    const res = await runLoadScript([
      '--base-url', api.baseUrl,
      '--rps', '6',
      '--duration-sec', '1'
    ]);
    assert.notEqual(res.code, 0);
    assert.match(res.stderr, /entre 1 y 5/);
    assert.equal(api.getRequests(), 0);
  } finally {
    await api.close();
  }
});

test('3. Ejecuta humo de carga controlada <= 5 rps y reporta p50, p95 y p99 sin errores', async () => {
  const api = await startMockApi();
  try {
    const res = await runLoadScript([
      '--base-url', api.baseUrl,
      '--rps', '5',
      '--duration-sec', '1',
      '--max-p95-ms', '2000'
    ]);
    assert.equal(res.code, 0, res.stderr);
    assert.equal(api.getGuestRequests(), 1);
    assert.match(res.stdout, /p50=/);
    assert.match(res.stdout, /p95=/);
    assert.match(res.stdout, /p99=/);
  } finally {
    await api.close();
  }
});

test('4. Falla cuando la tasa de error excede el umbral permitido', async () => {
  const api = await startMockApi(() => 500);
  try {
    const res = await runLoadScript([
      '--base-url', api.baseUrl,
      '--rps', '2',
      '--duration-sec', '1',
      '--paths', '/health/ready',
      '--max-error-pct', '0'
    ]);
    assert.notEqual(res.code, 0);
    assert.match(res.stderr, /UMBRAL INCUMPLIDO/);
  } finally {
    await api.close();
  }
});

test('5. Rechaza HTTP en producción si LOAD_ALLOW_LOCAL no está habilitado', async () => {
  const res = await runLoadScript(
    ['--base-url', 'http://example.com', '--rps', '2', '--duration-sec', '1'],
    { LOAD_ALLOW_LOCAL: 'false' }
  );
  assert.notEqual(res.code, 0);
  assert.match(res.stderr, /HTTPS/);
});
