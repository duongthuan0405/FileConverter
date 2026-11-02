const fs = require("node:fs");
const path = require("node:path");

const URL = "http://localhost:3000/forms/libreoffice/convert";
const FILE_PATH = "test-doc/inp/test.docx";
const CANCEL_AFTER_MS = null;

async function test()
{
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
      console.log(`After ${CANCEL_AFTER_MS} ms, request is being cancelling ...`);
      controller.abort();
    }, CANCEL_AFTER_MS)
    : null;


  try {
    // Gửi request
    console.log("Sending request...");
    const response = await fetch(URL, {
      method: "POST",
      body: form,
      signal: controller.signal
    });

    // Đọc response
    console.log("HTTP status:", response.status);

    if (!response.ok) {
        console.log(await response.text());
        return;
    }

    const pdf = Buffer.from(await response.arrayBuffer());
    fs.writeFileSync("test-doc/oup/output.pdf", pdf);
    console.log("Conversion completed");
  } 
  catch (error) {
    if (error.name === "AbortError") {
        console.log("Request CANCELLED");
    } else {
        console.error("Request FAILED:", error);
    }
  } finally {
    clearTimeout(timer);
  }
  const end = new Date();

  console.log("Start at:", start);
  console.log("End at:", end);
  console.log("Total:", (end - start) / 1000, "ms");
}



test().catch(error => console.log(error));