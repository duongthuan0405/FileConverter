// Run: node test-three-conversions.cjs
// Optional: node test-three-conversions.cjs "D:/path/file.docx" "http://localhost"
// Sends 3 real conversions concurrently. No retries; PDF responses are not saved.
const http = require('node:http');
const https = require('node:https');
const fs = require('node:fs/promises');
const path = require('node:path');
const { performance } = require('node:perf_hooks');

const defaultFile = String.raw`D:\UIT\25_26_HK1\SE100_OOAD\DoAn\23520303_DoMinhDuc_23521036_BuiBachNguyen_23521148_NguyenTruongTienPhat_23521473_NguyenDuyTuongThi_23521548_DuongQuocThuan.docx`;
const runId = `three-${Date.now()}`;
const started = Date.now();
const active = new Set();
const log = (message) => console.log(`[${((Date.now() - started) / 1000).toFixed(2)}s] ${message}`);

function send(number, target, body, boundary) {
  return new Promise((resolve) => {
    const url = new URL('/forms/libreoffice/convert', target);
    url.searchParams.set('capacity_test', `${runId}-R${number}`);
    const transport = url.protocol === 'https:' ? https : http;
    const sentAt = performance.now();
    let settled = false;
    let responseStarted = false;
    let status;
    let contentType = '';
    let received = 0;
    let errorBytes = 0;
    const errorChunks = [];

    const finish = (error) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      active.delete(request);
      const seconds = ((performance.now() - sentAt) / 1000).toFixed(2);
      log(`R${number}: ${status ? `HTTP ${status}` : 'NO RESPONSE'}, ${seconds}s, received=${received} bytes${error ? `, error=${error}` : ''}`);
      if (errorChunks.length) console.log(`R${number} error body (max 4096 bytes):\n${Buffer.concat(errorChunks).toString('utf8')}`);
      const httpError = status >= 400 ? `HTTP ${status}: ${http.STATUS_CODES[status] || 'Unknown error'}` : '';
      resolve({ index: number, Status: status ?? '-', Error: error || httpError,
        'Time (s)': seconds });
      request.destroy();
    };

    const request = transport.request(url, {
      method: 'POST', agent: false,
      headers: {
        'Content-Type': `multipart/form-data; boundary=${boundary}`,
        'Content-Length': body.length,
        Expect: '100-continue',
        Connection: 'close',
      },
    }, (response) => {
      responseStarted = true;
      status = response.statusCode;
      contentType = response.headers['content-type'] ?? '';
      log(`R${number}: received headers HTTP ${status}, Content-Type=${contentType}`);
      response.on('data', (chunk) => {
        received += chunk.length;
        // Drain binary responses without printing or retaining the PDF in RAM.
        if (status >= 400 && errorBytes < 4096) {
          const portion = chunk.subarray(0, 4096 - errorBytes);
          errorChunks.push(portion);
          errorBytes += portion.length;
        }
      });
      response.on('end', () => finish());
      response.on('aborted', () => finish('Response interrupted'));
      response.on('error', (error) => finish(error.message));
    });
    const timer = setTimeout(() => {
      finish('Client safety timeout after 210s');
    }, 210000);
    active.add(request);
    request.on('error', (error) => {
      // nginx can reject a request before the full upload has been written.
      // If headers arrived, let the response stream supply the final result.
      if (!responseStarted) finish(error.message);
    });
    request.on('close', () => {
      if (!settled && !responseStarted) finish('Connection closed before receiving HTTP response');
    });
    request.on('continue', () => {
      if (settled || responseStarted) return;
      log(`R${number}: received 100 Continue; sending file.`);
      request.end(body);
    });
    request.on('finish', () => log(`R${number}: complete body sent by client; waiting for result.`));
    log(`R${number}: POST ${url.pathname}${url.search}`);
    // Give nginx a chance to reject the request before sending a large body.
    request.flushHeaders();
  });
}

async function main() {
  const filePath = process.argv[2] || defaultFile;
  const target = new URL(process.argv[3] || 'http://localhost');
  if (!['http:', 'https:'].includes(target.protocol)) throw new Error('URL must use http or https');
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
  log(`Target: ${target.origin}. Sending 3 requests concurrently; no retries.`);
  const requestsStarted = performance.now();
  const results = await Promise.all([1, 2, 3].map((number) => send(number, target, body, boundary)));
  const totalSeconds = ((performance.now() - requestsStarted) / 1000).toFixed(2);
  // console.table supplies the index column from these 1-based object keys.
  console.table(Object.fromEntries(results.map(({ index, ...result }) => [index, result])));
  console.log(`Total time for 3 concurrent requests: ${totalSeconds}s`);
  console.log('With 2 healthy idle replicas and max_conns=1: normally two 200 responses and one 502.');
  console.log('Actual results depend on backend readiness, conversion errors and timeouts.');
  console.log(`Find this marker in nginx logs: ${runId}`);
  console.log('Logs: docker compose logs --since 5m nginx gotenberg');
  if (results.some((result) => result.Error)) process.exitCode = 1;
}

process.once('SIGINT', () => {
  for (const request of active) request.destroy();
  process.exit(130);
});
main().catch((error) => { console.error(error.message); process.exitCode = 1; });
