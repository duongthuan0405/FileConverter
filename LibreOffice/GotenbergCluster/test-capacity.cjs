const { resolve } = require("node:dns");
const fs = require("node:fs");
const path = require("node:path");
const { default: test } = require("node:test");

const URL = "http://localhost:5156/api/conversions";

async function callApi(index, inputPath, cancel_after_ms = null)
{
  const CANCEL_AFTER_MS = cancel_after_ms;
  const FILE_PATH = inputPath
  const start = new Date();
  // 1. Tạo AbortController
  const controller = new AbortController();

  // 2. Buffer đọc file
  const buffer = fs.readFileSync(FILE_PATH);
  
  const form = new FormData();
  form.append(
    "files",
    new Blob([buffer], {
        type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    }),
    path.basename(FILE_PATH)
  );

  // Đặt thời gian để abort
    const timer = CANCEL_AFTER_MS != null ? 
    setTimeout(() => {
      console.log(`After ${CANCEL_AFTER_MS} ms, request ${index} is being cancelling ...`);
      controller.abort();
    }, CANCEL_AFTER_MS)
    : null;


  try {
    // Gửi request
    console.log(`Sending request ${index}...`);
    const response = await fetch(URL, {
      method: "POST",
      body: form,
      signal: controller.signal
    });

    // Đọc response
    console.log(`HTTP status request ${index}:`, response.status);

    if (!response.ok) {
        console.log(await response.text());
        return;
    }

    const pdf = Buffer.from(await response.arrayBuffer());
    fs.writeFileSync(`test-doc/oup/output_${index}.pdf`, pdf);
    console.log(`Conversion ${index} completed`);
  } 
  catch (error) {
    if (error.name === "AbortError") {
        console.log(`Request ${index} CANCELLED`);
    } else {
        console.error(`Request ${index} FAILED:`, error);
    }
  } finally {
    clearTimeout(timer);
  }
  const end = new Date();

  console.log(`Request ${index}: Start at: ${start}; End at: ${end}; Total: ${(end - start)/1000}ms`);
}

async function main() {

  outputDir = "test-doc/oup";
  long = "test-doc/inp/long.docx";
  short = "test-doc/inp/short.docx";
  fs.rmSync(outputDir, { recursive: true, force: true });

  // Tạo lại thư mục
  fs.mkdirSync(outputDir, { recursive: true });
  const start = new Date();
  
  const promises = [
    callApi(1, short), 
    callApi(2, long), 
    callApi(3, short), 
    callApi(4, long), 
    callApi(5, short), 
    callApi(6, short), 
    callApi(7, long),
    callApi(8, long),
    callApi(9, long), 
    callApi(10, long), 
    callApi(11, long), 
    callApi(12, long), 
    callApi(13, long), 
    callApi(14, long), 
    callApi(15, long),   
    callApi(16, short),
    callApi(17, short),
    callApi(18, short),
    callApi(19, short),
    callApi(20, short)
  ];

  await Promise.all(promises);
  const end = new Date();
  console.log("TOTAL:", (end - start) / 1000, "s");
}

main().catch(e => console.error(e));