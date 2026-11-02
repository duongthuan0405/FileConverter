const http = require('node:http');

const uploads = [];
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
let cleaningUp = false;

function closeUploads() {
  cleaningUp = true;
  for (const request of uploads) request.destroy();
}

function openUpload(number) {
  return new Promise((resolve, reject) => {
    const request = http.request({
      hostname: '127.0.0.1',
      port: 80,
      path: '/forms/libreoffice/convert',
      method: 'POST',
      agent: false,
      headers: {
        'Content-Type': 'multipart/form-data; boundary=capacityprobe',
        'Content-Length': 1048576,
        Connection: 'close',
      },
    });

    uploads.push(request);
    request.on('error', (error) => {
      if (!cleaningUp) {
        console.error(`Upload ${number}: ${error.message}`);
        process.exitCode = 1;
      }
      reject(error);
    });
    request.on('response', (response) => {
      console.error(`Upload ${number} ket thuc som: HTTP ${response.statusCode}`);
      process.exitCode = 1;
      response.resume();
    });
    request.setTimeout(10000, () => {
      request.destroy(new Error(`Upload ${number} timeout`));
    });

    request.write(
      '--capacityprobe\r\n' +
        'Content-Disposition: form-data; name="files"; filename="D:\\UIT\\25_26_HK1\\SE100_OOAD\\DoAn\\23520303_DoMinhDuc_23521036_BuiBachNguyen_23521148_NguyenTruongTienPhat_23521473_NguyenDuyTuongThi_23521548_DuongQuocThuan.docx"\r\n' +
        'Content-Type: \r\n\r\n' +
        'probe',
      (error) => {
        if (error) return reject(error);
        console.log(`Da mo upload ${number}, chua gui het body.`);
        resolve();
      },
    );
    // Deliberately do not call end(): hold the upload connection open.
  });
}

function probeCapacity() {
  return new Promise((resolve, reject) => {
    const request = http.get(
      'http://127.0.0.1/health',
      { agent: false },
      (response) => {
        console.log(`Request thu ba: HTTP ${response.statusCode}`);
        if (response.statusCode !== 502) {
          console.error('Khong nhan 502 nhu mong doi; kiem tra replica va log nginx.');
          process.exitCode = 1;
        }
        response.resume();
        response.on('end', resolve);
        response.on('error', reject);
        response.on('aborted', () => reject(new Error('Response bi ngat')));
      },
    );
    request.setTimeout(5000, () => {
      request.destroy(new Error('Request thu ba timeout'));
    });
    request.on('error', reject);
  });
}

async function main() {
  try {
    await Promise.all([openUpload(1), openUpload(2)]);
    await sleep(2000);
    if (process.exitCode) throw new Error('Khong giu duoc hai upload.');
    await probeCapacity();
    console.log('Doi chieu log nginx: hai upload phai den hai IP backend khac nhau.');
  } finally {
    closeUploads();
    console.log('Da dong hai ket noi upload.');
  }
}

process.once('SIGINT', () => {
  closeUploads();
  process.exit(130);
});

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
