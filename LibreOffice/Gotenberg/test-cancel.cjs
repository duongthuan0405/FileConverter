
const fs = require("node:fs");
const http = require("node:http");
const path = require("node:path");
const { pipeline } = require("node:stream/promises");
const { performance } = require("node:perf_hooks");

const GOTENBERG_URL = "http://localhost:5156/api/conversions/";

const DEFAULT_FILE_PATH = String.raw`D:\UIT\25_26_HK1\SE100_OOAD\DoAn\23520303_DoMinhDuc_23521036_BuiBachNguyen_23521148_NguyenTruongTienPhat_23521473_NguyenDuyTuongThi_23521548_DuongQuocThuan.docx`;

function formatMs(ms) {
  return `${(ms / 1000).toFixed(3)}s (${Math.round(ms)}ms)`;
}

/**
 * Core: gửi DOCX đến Gotenberg.
 *
 * @param {string} filePath
 * @param {number|null} cancelledAt - ms, null nghĩa là không cancel
 */
async function sendConversionRequest(filePath, cancelledAt = null) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`File not found: ${filePath}`);
  }

  const fileName = path.basename(filePath);
  const fileSize = fs.statSync(filePath).size;
  const boundary = `----GotenbergTest${Date.now()}`;

  const header = Buffer.from(
    `--${boundary}\r\n` +
    `Content-Disposition: form-data; name="files"; filename="${fileName}"\r\n` +
    `Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n`
  );

  const footer = Buffer.from(`\r\n--${boundary}--\r\n`);
  const contentLength = header.length + fileSize + footer.length;

  const outputPath = path.join(
    process.cwd(),
    `${path.parse(fileName).name}.pdf`
  );

  const startTime = performance.now();
  const traceId = `test-${Date.now()}`;

  console.log("\n========== GOTENBERG TEST ==========");
  console.log("File:", filePath);
  console.log("File size:", (fileSize / 1024 / 1024).toFixed(2), "MB");
  console.log("Mode:", cancelledAt === null ? "NORMAL" : "CANCEL");
  console.log("Cancel after:", cancelledAt === null ? "Never" : `${cancelledAt}ms`);
  console.log("Trace ID:", traceId);

  return new Promise((resolve, reject) => {
    let cancelled = false;
    let finished = false;
    let timer = null;
    let fileStream = null;
    let outputStream = null;
    let responseStartedAt = null;
    let uploadedAt = null;
    let statusCode = null;
    let receivedBytes = 0;

    const elapsed = () => performance.now() - startTime;

    const printResult = (status, extra = {}) => {
      console.log("\n========== RESULT ==========");
      console.log("Result:", status);
      console.log("HTTP status:", statusCode ?? "N/A");
      console.log("Upload duration:", uploadedAt === null ? "Not completed" : formatMs(uploadedAt));
      console.log("Response start:", responseStartedAt === null ? "N/A" : formatMs(responseStartedAt));
      console.log("Total duration:", formatMs(elapsed()));
      console.log("Received bytes:", receivedBytes);
      if (extra.outputPath) console.log("Output:", extra.outputPath);
      if (extra.error) console.log("Error:", extra.error);
      console.log("============================\n");
    };

    const finish = (status, extra = {}) => {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      printResult(status, extra);
      resolve({ status, statusCode, durationMs: elapsed(), ...extra });
    };

    const req = http.request(GOTENBERG_URL, {
      method: "POST",
      headers: {
        "Content-Type": `multipart/form-data; boundary=${boundary}`,
        "Content-Length": contentLength,
        "Gotenberg-Trace": traceId,
      },
    });

    req.on("response", async (res) => {
      statusCode = res.statusCode;
      responseStartedAt = elapsed();

      console.log(
        `[${formatMs(elapsed())}] HTTP response: ${statusCode}`
      );

      res.on("data", (chunk) => {
        receivedBytes += chunk.length;
      });

      if (statusCode !== 200) {
        const chunks = [];

        try {
          for await (const chunk of res) {
            chunks.push(chunk);
          }

          finish("HTTP_ERROR", {
            error: Buffer.concat(chunks).toString("utf8"),
          });
        } catch (err) {
          if (!cancelled) finish("ERROR", { error: err.message });
        }

        return;
      }

      outputStream = fs.createWriteStream(outputPath);

      try {
        await pipeline(res, outputStream);

        if (!cancelled) {
          finish("SUCCESS", { outputPath });
        }
      } catch (err) {
        if (!cancelled) {
          finish("ERROR", { error: err.message });
        }
      }
    });

    req.on("error", (err) => {
      if (!cancelled) {
        finish("ERROR", { error: err.message });
      }
    });

    req.on("close", () => {
      if (cancelled) {
        finish("CANCELLED");
      }
    });

    // Timer tính từ lúc bắt đầu gửi request.
    if (cancelledAt !== null) {
      timer = setTimeout(() => {
        if (finished) return;

        cancelled = true;

        console.log(
          `[${formatMs(elapsed())}] Cancelling HTTP request...`
        );

        fileStream?.destroy();
        outputStream?.destroy();
        req.destroy(new Error("Client cancelled request"));
      }, cancelledAt);
    }

    req.write(header);

    fileStream = fs.createReadStream(filePath);

    fileStream.on("error", (err) => {
      if (!cancelled) req.destroy(err);
    });

    fileStream.on("end", () => {
      if (cancelled) return;

      req.end(footer);
    });

    req.on("finish", () => {
      uploadedAt = elapsed();
      console.log(`[${formatMs(uploadedAt)}] Upload completed`);
    });

    fileStream.pipe(req, { end: false });
  });
}

/**
 * Method 1: Convert bình thường.
 */
async function convert(filePath) {
  return sendConversionRequest(filePath);
}

/**
 * Method 2: Convert và cancel giữa chừng.
 *
 * @param {string} filePath
 * @param {number} cancelledAt - milliseconds
 */
async function convertWithCancel(filePath, cancelledAt) {
  if (!Number.isFinite(cancelledAt) || cancelledAt <= 0) {
    throw new Error("cancelledAt must be a positive number (ms)");
  }

  return sendConversionRequest(filePath, cancelledAt);
}

// ==================== MAIN ====================

async function main() {
  const filePath = process.argv[3] || DEFAULT_FILE_PATH;
  const cancelledAt = Number(process.argv[4] || 5000);
  const lightFilePath = "C:\\Users\\ASUS\\Downloads\\test.docx";

  let start = new Date();
  await convert(filePath);
  await convert(filePath);
  await convert(filePath);
  let end = new Date();
  console.log("Total time:", end - start);
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exitCode = 1;
});
