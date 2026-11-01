// Run from this directory: node test-client-abort.cjs
// Optional: node test-client-abort.cjs "D:/path/document.docx" 2000
// Second argument: milliseconds to wait after both uploads finish before abort.
// Run on an idle two-replica cluster. No PDF output is saved.
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');

const filePath = process.argv[2] || String.raw`D:\UIT\25_26_HK1\SE100_OOAD\DoAn\23520303_DoMinhDuc_23521036_BuiBachNguyen_23521148_NguyenTruongTienPhat_23521473_NguyenDuyTuongThi_23521548_DuongQuocThuan.docx`;
const abortDelay = Number(process.argv[3] || 1000);
const started = Date.now();
const requests = [];
const log = (message) => console.log(`[${((Date.now() - started) / 1000).toFixed(2)}s] ${message}`);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function conversion(id, body, boundary) {
  const state = { done: false, aborted: false };
  state.uploaded = new Promise((resolve, reject) => {
    const req = http.request({
      hostname: '127.0.0.1', port: 80,
      path: '/forms/libreoffice/convert', method: 'POST', agent: false,
      headers: {
        'Content-Type': `multipart/form-data; boundary=${boundary}`,
        'Content-Length': body.length,
        'X-Abort-Test': `${started}-${id}`,
        Connection: 'close',
      },
    });
    state.request = req;
    requests.push(req);
    req.setTimeout(180000, () => req.destroy(new Error('timeout')));
    req.on('finish', () => {
      log(`Conversion ${id}: da gui het body tai client.`);
      resolve();
    });
    req.on('response', (res) => {
      state.done = true;
      state.status = res.statusCode;
      log(`Conversion ${id}: nhan response HTTP ${res.statusCode}.`);
      res.resume();
      res.on('error', (error) => log(`Response ${id}: ${error.message}`));
    });
    req.on('error', (error) => {
      state.done = true;
      if (!state.aborted) log(`Conversion ${id}: ${error.message}`);
      reject(error);
    });
    req.end(body);
  });
  return state;
}

function probe(label) {
  return new Promise((resolve, reject) => {
    const req = http.get('http://127.0.0.1/health', { agent: false }, (res) => {
      log(`${label}: HTTP ${res.statusCode}`);
      res.resume();
      res.on('end', () => resolve(res.statusCode));
      res.on('error', reject);
      res.on('aborted', () => reject(new Error('Probe response interrupted')));
    });
    requests.push(req);
    req.setTimeout(5000, () => req.destroy(new Error('Probe timeout')));
    req.on('error', reject);
  });
}

async function main() {
  if (!Number.isFinite(abortDelay) || abortDelay < 0) throw new Error('Invalid abort delay');
  const file = await fs.readFile(filePath);
  const boundary = `abort-test-${started}`;
  const filename = path.basename(filePath).replace(/["\r\n]/g, '_');
  const body = Buffer.concat([
    Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="files"; filename="${filename}"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n`),
    file,
    Buffer.from(`\r\n--${boundary}--\r\n`),
  ]);
  log(`File: ${filePath} (${file.length} bytes)`);
  const a = conversion(1, body, boundary);
  const b = conversion(2, body, boundary);
  await Promise.all([a.uploaded, b.uploaded]);
  await sleep(abortDelay);
  const before = await probe('Truoc khi ngat client');
  if (a.done || b.done || before !== 502) {
    log('KHONG KET LUAN: chua giu duoc hai conversion dang cho response. Thu lai voi delay nho hon hoac file nang hon.');
    process.exitCode = 2;
    return;
  }
  log('Ngat client cua conversion 1; conversion 2 van giu ket noi.');
  a.aborted = true;
  a.request.destroy();
  await sleep(250);
  const after = await probe('Sau khi ngat client');
  if (b.done) {
    log('KHONG KET LUAN: conversion 2 da tra response trong luc probe.');
  } else if (after === 502) {
    log('Phu hop voi viec nginx van giu hai slot. Can doi chieu log de xac nhan.');
  } else {
    log('Co slot kha dung: co the conversion 1 da xong, hoac ket noi upstream da dong. Khong the ket luan chi tu HTTP nay.');
  }
  log('Theo doi slot toi da 150 giay; khong retry conversion.');
  const deadline = Date.now() + 150000;
  while (Date.now() < deadline) {
    await sleep(1000);
    if (await probe('Kiem tra slot') === 200) {
      log('Da co it nhat mot slot trong.');
      break;
    }
  }
  log('Xem log: docker compose logs --since 5m nginx gotenberg');
  log('Hai POST can den hai IP khac nhau; doi chieu thoi diem upstream ket thuc voi probe.');
  log('finish tai client khong chung minh engine da bat dau; log Gotenberg moi giup xac nhan giai doan xu ly.');
}

function cleanup() {
  for (const req of requests) req.destroy();
}
process.once('SIGINT', () => { cleanup(); process.exit(130); });
main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
}).finally(cleanup);
