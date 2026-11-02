// Run: node test-upstream-timeout.cjs ["D:/path/document.docx"]
// Requires an idle two-replica test cluster and proxy_read_timeout 2s.
// Sends four real conversions; does not save PDF responses or modify nginx.
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');

const filePath = process.argv[2] || String.raw`D:\UIT\25_26_HK1\SE100_OOAD\DoAn\23520303_DoMinhDuc_23521036_BuiBachNguyen_23521148_NguyenTruongTienPhat_23521473_NguyenDuyTuongThi_23521548_DuongQuocThuan.docx`;
const runId = `timeout-${Date.now()}`;
const started = Date.now();
const active = new Set();
const log = (message) => console.log(`[${((Date.now() - started) / 1000).toFixed(2)}s] ${message}`);

function send(id, body, boundary) {
  return new Promise((resolve) => {
    const requestStarted = Date.now();
    let settled = false;
    const finish = (result) => {
      if (settled) return;
      settled = true;
      active.delete(req);
      const seconds = ((Date.now() - requestStarted) / 1000).toFixed(2);
      log(`${id}: ${result.status ? `HTTP ${result.status}` : result.error}, ${seconds}s`);
      resolve({ id, ...result });
    };
    // Query marker lets nginx's existing $request log identify each attempt.
    const req = http.request({
      hostname: '127.0.0.1', port: 80, agent: false,
      path: `/forms/libreoffice/convert?capacity_test=${runId}-${id}`,
      method: 'POST',
      headers: {
        'Content-Type': `multipart/form-data; boundary=${boundary}`,
        'Content-Length': body.length,
        Connection: 'close',
      },
    }, (res) => {
      res.resume();
      res.on('end', () => finish({ status: res.statusCode }));
      res.on('error', (error) => finish({ error: error.message }));
      res.on('aborted', () => finish({ error: 'Response interrupted' }));
    });
    active.add(req);
    // Wall-clock safety limit, separate from nginx's read inactivity timeout.
    const timer = setTimeout(() => req.destroy(new Error('Client safety timeout: 180s')), 180000);
    req.on('close', () => clearTimeout(timer));
    req.on('error', (error) => finish({ error: error.message }));
    req.on('finish', () => log(`${id}: body sent by client; engine start not confirmed.`));
    log(`${id}: starting conversion`);
    req.end(body);
  });
}

async function main() {
  const file = await fs.readFile(filePath);
  const boundary = `boundary-${runId}`;
  const filename = path.basename(filePath).replace(/["\r\n]/g, '_');
  const body = Buffer.concat([
    Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="files"; filename="${filename}"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n`),
    file,
    Buffer.from(`\r\n--${boundary}--\r\n`),
  ]);
  log(`Run marker: ${runId}`);
  log(`File: ${filePath} (${file.length} bytes)`);
  log('Wave A: two simultaneous conversions. Expected HTTP 504 with nginx read timeout 2s.');
  const first = await Promise.all([send('A1', body, boundary), send('A2', body, boundary)]);
  if (!first.every((result) => result.status === 504)) {
    log('INCONCLUSIVE: wave A did not produce two 504 responses. Wave B skipped.');
    log('Check nginx reload, backend health, and whether conversion finished before timeout.');
    process.exitCode = 2;
    return;
  }
  log('Wave B: immediately send two more conversions after both wave A timeouts.');
  const second = await Promise.all([send('B1', body, boundary), send('B2', body, boundary)]);
  if (second.some((result) => !result.status)) process.exitCode = 1;
  log('HTTP results alone do NOT prove overlapping engine execution.');
  log('Compare A/B backend IPs and request timing in nginx and Gotenberg logs.');
  log('504: upstream timed out again; 502: upstream unavailable/capacity or connection error; 200: response completed.');
}

process.once('SIGINT', () => {
  for (const req of active) req.destroy();
  process.exit(130);
});
main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
}).finally(() => {
  log('Collect logs after engine work has settled: docker compose logs --since 5m nginx gotenberg');
  log(`Find marker in nginx logs: ${runId}`);
  log('Restore proxy_read_timeout 150s, then nginx -t and nginx -s reload.');
});
