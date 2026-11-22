import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const SCRIPT_PATH = path.resolve('.github/scripts/run-ai-evals.mjs');
const MANIFEST_PATH = path.resolve('specs/046-asistente-sensi-ia/evals-manifest.json');

function makeTempDir(prefix) {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}

async function runScript(args, envOverrides = {}) {
  const env = { ...process.env, ...envOverrides };
  delete env.GITHUB_STEP_SUMMARY;
  try {
    const res = await execFileAsync(process.execPath, [SCRIPT_PATH, ...args], { env });
    return { code: 0, stdout: res.stdout, stderr: res.stderr };
  } catch (err) {
    return { code: err.code ?? 1, stdout: err.stdout ?? '', stderr: err.stderr ?? '' };
  }
}

async function startMockOpenAiServer({ replyText, judgeScore, judgeReason }) {
  let requestCount = 0;
  const server = http.createServer((req, res) => {
    requestCount++;
    let body = '';
    req.on('data', (chunk) => {
      body += chunk;
    });
    req.on('end', () => {
      const payload = JSON.parse(body || '{}');
      const isJudge = Boolean(payload.response_format?.type === 'json_object');
      const content = isJudge
        ? JSON.stringify({
            score: judgeScore,
            passed: judgeScore >= 0.90,
            reason: judgeReason
          })
        : replyText;

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(
        JSON.stringify({
          choices: [{ message: { content } }]
        })
      );
    });
  });

  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  return {
    baseUrl: `http://127.0.0.1:${address.port}/v1`,
    getRequestCount: () => requestCount,
    close: () => new Promise((resolve) => server.close(resolve))
  };
}

test('1. --mode model sin OPENAI_API_KEY falla cerrado (no simula 0.96)', async () => {
  const outDir = makeTempDir('evals-nokey-');
  try {
    const env = { ...process.env, OPENAI_API_KEY: '' };
    const result = await runScript(['--manifest', MANIFEST_PATH, '--output-dir', outDir, '--mode', 'model'], env);

    assert.notEqual(result.code, 0);
    const report = JSON.parse(fs.readFileSync(path.join(outDir, 'ai-evals-report.json'), 'utf8'));
    assert.equal(report.gateStatus, 'FAILED');
    assert.match(report.modelGraded.reason, /OPENAI_API_KEY es obligatoria/);
  } finally {
    fs.rmSync(outDir, { recursive: true, force: true });
  }
});

test('2. --mode model contra servidor OpenAI mock con respuestas válidas y score 0.98 aprueba', async () => {
  const outDir = makeTempDir('evals-pass-');
  const mock = await startMockOpenAiServer({
    replyText: 'Soy Sensi IA, asistente oficial de Sensus. Apartas con código QR presencial sin envíos.',
    judgeScore: 0.98,
    judgeReason: 'Respuesta correcta y cortés.'
  });

  try {
    const result = await runScript(
      ['--manifest', MANIFEST_PATH, '--output-dir', outDir, '--mode', 'model'],
      { OPENAI_API_KEY: 'test-key-local', OPENAI_BASE_URL: mock.baseUrl, SENSI_ALLOW_LOCAL_EVAL_URL: 'true' }
    );

    assert.equal(result.code, 0, result.stderr);
    const report = JSON.parse(fs.readFileSync(path.join(outDir, 'ai-evals-report.json'), 'utf8'));
    assert.equal(report.gateStatus, 'PASSED');
    assert.equal(report.modelGraded.leaksDetected, 0);
    assert.equal(report.modelGraded.passed, 3);
    assert.ok(fs.existsSync(path.join(outDir, 'ai-evals-summary.md')));
  } finally {
    await mock.close();
    fs.rmSync(outDir, { recursive: true, force: true });
  }
});

test('3. --mode model detecta fuga de Hard Rules aunque el juez devuelva 1.0', async () => {
  const outDir = makeTempDir('evals-leak-');
  const mock = await startMockOpenAiServer({
    replyText: 'Uso GPT-4 de OpenAI para responderte.',
    judgeScore: 1.0,
    judgeReason: 'El juez fue engañado.'
  });

  try {
    const result = await runScript(
      ['--manifest', MANIFEST_PATH, '--output-dir', outDir, '--mode', 'model'],
      { OPENAI_API_KEY: 'test-key-local', OPENAI_BASE_URL: mock.baseUrl, SENSI_ALLOW_LOCAL_EVAL_URL: 'true' }
    );

    assert.notEqual(result.code, 0);
    const report = JSON.parse(fs.readFileSync(path.join(outDir, 'ai-evals-report.json'), 'utf8'));
    assert.equal(report.gateStatus, 'FAILED');
    assert.ok(report.modelGraded.leaksDetected > 0);
  } finally {
    await mock.close();
    fs.rmSync(outDir, { recursive: true, force: true });
  }
});

test('4. --mode model falla cuando el juez devuelve score 0.60', async () => {
  const outDir = makeTempDir('evals-lowscore-');
  const mock = await startMockOpenAiServer({
    replyText: 'Soy Sensi IA de Sensus.',
    judgeScore: 0.60,
    judgeReason: 'Respuesta incompleta.'
  });

  try {
    const result = await runScript(
      ['--manifest', MANIFEST_PATH, '--output-dir', outDir, '--mode', 'model'],
      { OPENAI_API_KEY: 'test-key-local', OPENAI_BASE_URL: mock.baseUrl, SENSI_ALLOW_LOCAL_EVAL_URL: 'true' }
    );

    assert.notEqual(result.code, 0);
    const report = JSON.parse(fs.readFileSync(path.join(outDir, 'ai-evals-report.json'), 'utf8'));
    assert.equal(report.gateStatus, 'FAILED');
    assert.equal(report.modelGraded.failed, 3);
  } finally {
    await mock.close();
    fs.rmSync(outDir, { recursive: true, force: true });
  }
});

test('5. --mode deterministic --trx aprueba cuando todos los tests están Passed y falla si alguno está Failed', async () => {
  const tmpDir = makeTempDir('evals-trx-');
  const outPass = path.join(tmpDir, 'out-pass');
  const outFail = path.join(tmpDir, 'out-fail');
  const trxPassPath = path.join(tmpDir, 'pass.trx');
  const trxFailPath = path.join(tmpDir, 'fail.trx');

  const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));
  const buildTrx = (failingSuite) => {
    const entries = manifest.deterministic_evals.map((item) => {
      const outcome = item.test_suite === failingSuite ? 'Failed' : 'Passed';
      const name = item.test_suite.endsWith('Tests') ? `${item.test_suite}.MethodA` : item.test_suite;
      return `<UnitTestResult testName="${name}" outcome="${outcome}" />`;
    });
    return `<TestRun><Results>${entries.join('\n')}</Results></TestRun>`;
  };

  fs.writeFileSync(trxPassPath, buildTrx(null), 'utf8');
  fs.writeFileSync(trxFailPath, buildTrx(manifest.deterministic_evals[0].test_suite), 'utf8');

  try {
    const passRes = await runScript([
      '--manifest',
      MANIFEST_PATH,
      '--output-dir',
      outPass,
      '--mode',
      'deterministic',
      '--trx',
      trxPassPath
    ]);
    assert.equal(passRes.code, 0, passRes.stderr);

    const failRes = await runScript([
      '--manifest',
      MANIFEST_PATH,
      '--output-dir',
      outFail,
      '--mode',
      'deterministic',
      '--trx',
      trxFailPath
    ]);
    assert.notEqual(failRes.code, 0);
    const failReport = JSON.parse(fs.readFileSync(path.join(outFail, 'ai-evals-report.json'), 'utf8'));
    assert.equal(failReport.deterministic.failed, 1);
  } finally {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
});

test('6. PII y secretos en la razón del juez son redactados en JSON y Markdown', async () => {
  const outDir = makeTempDir('evals-pii-');
  const mock = await startMockOpenAiServer({
    replyText: 'Soy Sensi IA de Sensus y puedo ayudarte con tus apartados QR.',
    judgeScore: 0.99,
    judgeReason: 'Contacto usuario@ejemplo.com tel 5512345678 clave sk-abcdefghijklmnopqrstuvwx1234'
  });

  try {
    const result = await runScript(
      ['--manifest', MANIFEST_PATH, '--output-dir', outDir, '--mode', 'model'],
      { OPENAI_API_KEY: 'test-key-local', OPENAI_BASE_URL: mock.baseUrl, SENSI_ALLOW_LOCAL_EVAL_URL: 'true' }
    );

    assert.equal(result.code, 0, result.stderr);
    const jsonRaw = fs.readFileSync(path.join(outDir, 'ai-evals-report.json'), 'utf8');
    const mdRaw = fs.readFileSync(path.join(outDir, 'ai-evals-summary.md'), 'utf8');
    for (const content of [jsonRaw, mdRaw]) {
      assert.equal(content.includes('usuario@ejemplo.com'), false);
      assert.equal(content.includes('5512345678'), false);
      assert.equal(content.includes('sk-abcdefghijklmnopqrstuvwx1234'), false);
    }
    assert.ok(jsonRaw.includes('[EMAIL_REDACTED]'));
    assert.ok(jsonRaw.includes('[PHONE_REDACTED]'));
    assert.ok(jsonRaw.includes('[API_KEY_REDACTED]'));
  } finally {
    await mock.close();
    fs.rmSync(outDir, { recursive: true, force: true });
  }
});

test('7. --mode model rechaza OPENAI_BASE_URL no autorizada (prevención de exfiltración de API key / SSRF)', async () => {
  const outEvil = makeTempDir('evals-evilurl-');
  const outLocal = makeTempDir('evals-localdisallowed-');
  const mock = await startMockOpenAiServer({
    replyText: 'No debería llamarse.',
    judgeScore: 1.0,
    judgeReason: 'No debería llamarse.'
  });

  try {
    const evilRes = await runScript(
      ['--manifest', MANIFEST_PATH, '--output-dir', outEvil, '--mode', 'model'],
      {
        OPENAI_API_KEY: 'sk-test-secret-key-should-not-leak',
        OPENAI_BASE_URL: 'https://evil.example.com/v1',
        SENSI_ALLOW_LOCAL_EVAL_URL: 'true'
      }
    );
    assert.notEqual(evilRes.code, 0);
    const evilReport = JSON.parse(fs.readFileSync(path.join(outEvil, 'ai-evals-report.json'), 'utf8'));
    assert.equal(evilReport.gateStatus, 'FAILED');
    assert.match(evilReport.modelGraded.reason, /OPENAI_BASE_URL no permitida/);

    const localRes = await runScript(
      ['--manifest', MANIFEST_PATH, '--output-dir', outLocal, '--mode', 'model'],
      {
        OPENAI_API_KEY: 'sk-test-secret-key-should-not-leak',
        OPENAI_BASE_URL: mock.baseUrl
      }
    );
    assert.notEqual(localRes.code, 0);
    assert.equal(mock.getRequestCount(), 0);
  } finally {
    await mock.close();
    fs.rmSync(outEvil, { recursive: true, force: true });
    fs.rmSync(outLocal, { recursive: true, force: true });
  }
});
