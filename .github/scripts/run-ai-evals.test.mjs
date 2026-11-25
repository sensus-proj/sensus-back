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

/**
 * Servidor OpenAI simulado. `replyTexts` y `judgeScores` se recorren en ciclo por orden de llamada; como los prompts se
 * evalúan en secuencia y sus corridas en paralelo, cada prompt recibe el mismo conjunto de valores del ciclo.
 */
async function startMockOpenAiServer({ replyText, judgeScore, judgeReason, replyTexts, judgeScores }) {
  let requestCount = 0;
  let generationCount = 0;
  let judgeCount = 0;
  const generationTemperatures = [];
  const judgeTemperatures = [];
  const replies = replyTexts ?? [replyText];
  const scores = judgeScores ?? [judgeScore];
  const server = http.createServer((req, res) => {
    requestCount++;
    let body = '';
    req.on('data', (chunk) => {
      body += chunk;
    });
    req.on('end', () => {
      const payload = JSON.parse(body || '{}');
      const isJudge = Boolean(payload.response_format?.type === 'json_object');
      let content;
      if (isJudge) {
        judgeTemperatures.push(payload.temperature);
        const score = scores[judgeCount++ % scores.length];
        content = JSON.stringify({
          score,
          passed: score >= 0.90,
          reason: judgeReason
        });
      } else {
        generationTemperatures.push(payload.temperature);
        content = replies[generationCount++ % replies.length];
      }

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
    getGenerationTemperatures: () => [...generationTemperatures],
    getJudgeTemperatures: () => [...judgeTemperatures],
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

async function runModelEvalsAgainstMock(mockOptions, extraEnv = {}, manifestPath = MANIFEST_PATH) {
  const outDir = makeTempDir('evals-runs-');
  const mock = await startMockOpenAiServer({ judgeReason: 'Evaluación simulada.', ...mockOptions });
  try {
    const result = await runScript(['--manifest', manifestPath, '--output-dir', outDir, '--mode', 'model'], {
      OPENAI_API_KEY: 'test-key-local',
      OPENAI_BASE_URL: mock.baseUrl,
      SENSI_ALLOW_LOCAL_EVAL_URL: 'true',
      SENSI_EVAL_TEMPERATURE: '',
      ...extraEnv
    });
    const reportPath = path.join(outDir, 'ai-evals-report.json');
    const report = fs.existsSync(reportPath) ? JSON.parse(fs.readFileSync(reportPath, 'utf8')) : null;
    const summaryPath = path.join(outDir, 'ai-evals-summary.md');
    const summary = fs.existsSync(summaryPath) ? fs.readFileSync(summaryPath, 'utf8') : '';
    return {
      result,
      report,
      summary,
      generationTemperatures: mock.getGenerationTemperatures(),
      judgeTemperatures: mock.getJudgeTemperatures()
    };
  } finally {
    await mock.close();
    fs.rmSync(outDir, { recursive: true, force: true });
  }
}

const CLEAN_REPLY = 'Soy Sensi IA, asistente oficial de Sensus. Apartas con código QR presencial sin envíos.';

test('8. FR-047-16: una corrida baja aislada (1.0, 0.6, 1.0) no reprueba el prompt porque la mediana es 1.0', async () => {
  const { result, report, summary } = await runModelEvalsAgainstMock({
    replyText: CLEAN_REPLY,
    judgeScores: [1.0, 0.6, 1.0]
  });

  assert.equal(result.code, 0, result.stderr);
  assert.equal(report.gateStatus, 'PASSED');
  assert.equal(report.modelGraded.runsPerPrompt, 3);
  for (const evalCase of report.modelGraded.cases) {
    for (const prompt of evalCase.promptResults) {
      assert.equal(prompt.totalRuns, 3);
      assert.equal(prompt.passedRuns, 2);
      assert.equal(prompt.score, 1);
      assert.equal(prompt.runs.length, 3);
    }
  }
  assert.match(summary, /Detalle por prompt/);
  assert.match(summary, /Corridas por prompt \(mediana, FR-047-16\):\*\* 3/);
});

test('9. FR-047-16: dos corridas bajas (0.6, 0.6, 1.0) reprueban aunque una corrida saque 1.0 (umbral intacto)', async () => {
  const { result, report } = await runModelEvalsAgainstMock({
    replyText: CLEAN_REPLY,
    judgeScores: [0.6, 0.6, 1.0]
  });

  assert.notEqual(result.code, 0);
  assert.equal(report.gateStatus, 'FAILED');
  assert.equal(report.modelGraded.failed, 3);
  for (const evalCase of report.modelGraded.cases) {
    for (const prompt of evalCase.promptResults) {
      assert.equal(prompt.passed, false);
      assert.equal(prompt.score, 0.6);
    }
  }
});

test('10. FR-047-16: una fuga en una sola de las 3 corridas reprueba el caso aunque el juez califique 1.0', async () => {
  const { result, report } = await runModelEvalsAgainstMock({
    replyTexts: [CLEAN_REPLY, CLEAN_REPLY, 'Uso GPT-4 de OpenAI para responderte.'],
    judgeScore: 1.0
  });

  assert.notEqual(result.code, 0);
  assert.equal(report.gateStatus, 'FAILED');
  assert.ok(report.modelGraded.leaksDetected > 0);
  for (const evalCase of report.modelGraded.cases) {
    assert.equal(evalCase.status, 'FAILED');
    for (const prompt of evalCase.promptResults) {
      assert.equal(prompt.leakDetected, true);
    }
  }
});

test('11. FR-047-17: la generación usa la temperatura de producción de appsettings.json y el juez usa 0', async () => {
  const settings = JSON.parse(fs.readFileSync(path.resolve('src/Sensus.Api/appsettings.json'), 'utf8'));
  const productionTemperature = settings.OpenAiSettings.Sensi.Temperature;
  assert.equal(typeof productionTemperature, 'number');

  const { result, report, generationTemperatures, judgeTemperatures } = await runModelEvalsAgainstMock({
    replyText: CLEAN_REPLY,
    judgeScore: 0.98
  });

  assert.equal(result.code, 0, result.stderr);
  assert.equal(report.modelGraded.generationTemperature, productionTemperature);
  assert.equal(report.modelGraded.generationTemperatureSource, 'src/Sensus.Api/appsettings.json');
  assert.ok(generationTemperatures.length > 0);
  assert.ok(generationTemperatures.every((t) => t === productionTemperature));
  assert.ok(judgeTemperatures.every((t) => t === 0));
});

test('12. FR-047-17: SENSI_EVAL_TEMPERATURE sobrescribe en local y un valor fuera de rango falla cerrado', async () => {
  const overridden = await runModelEvalsAgainstMock(
    { replyText: CLEAN_REPLY, judgeScore: 0.98 },
    { SENSI_EVAL_TEMPERATURE: '0.3' }
  );
  assert.equal(overridden.result.code, 0, overridden.result.stderr);
  assert.ok(overridden.generationTemperatures.every((t) => t === 0.3));
  assert.equal(overridden.report.modelGraded.generationTemperatureSource, 'SENSI_EVAL_TEMPERATURE');

  const invalid = await runModelEvalsAgainstMock(
    { replyText: CLEAN_REPLY, judgeScore: 0.98 },
    { SENSI_EVAL_TEMPERATURE: '1.7' }
  );
  assert.notEqual(invalid.result.code, 0);
  assert.equal(invalid.report.gateStatus, 'FAILED');
  assert.match(invalid.report.modelGraded.reason, /SENSI_EVAL_TEMPERATURE/);
  assert.equal(invalid.generationTemperatures.length, 0);
});

test('13. FR-047-16: runs_per_prompt inválido en el manifiesto falla cerrado sin llamar al proveedor', async () => {
  const tempDir = makeTempDir('evals-badmanifest-');
  const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));
  manifest.runs_per_prompt = 0;
  const badManifestPath = path.join(tempDir, 'manifest.json');
  fs.writeFileSync(badManifestPath, JSON.stringify(manifest), 'utf8');

  try {
    const { result, report, generationTemperatures } = await runModelEvalsAgainstMock(
      { replyText: CLEAN_REPLY, judgeScore: 1.0 },
      {},
      badManifestPath
    );
    assert.notEqual(result.code, 0);
    assert.equal(report.gateStatus, 'FAILED');
    assert.match(report.modelGraded.reason, /runs_per_prompt/);
    assert.equal(generationTemperatures.length, 0);
  } finally {
    fs.rmSync(tempDir, { recursive: true, force: true });
  }
});
