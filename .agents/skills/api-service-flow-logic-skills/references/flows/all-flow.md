# MÔ HÌNH TỔNG QUÁT HỆ THỐNG WORD-TO-PDF CONVERSION GATEWAY
## (.NET 10 & Gotenberg Cluster Architecture)

---

## 1. Sơ Đồ Kiến Trúc Tổng Thể (System Architecture Topology)

Sơ đồ thể hiện toàn bộ các tầng từ **Phía Backend Khách (BE Host)**, qua **Gateway .NET 10**, đến **Hàng đợi Valkey** và cụm **Gotenberg Cluster**:

```mermaid
flowchart TD
    subgraph BE_ZONE["1. PHÍA ỨNG DỤNG DOANH NGHIỆP (BE HOST)"]
        BE["Hệ thống Backend (BE)"]
        HOST_DISK[("Ổ cứng vật lý máy Host BE\n(Lưu trữ PDF vĩnh viễn)")]
    end

    subgraph GW_ZONE["2. WORD-TO-PDF GATEWAY (.NET 10) - Port 8080"]
        API["API Controller\n(REST API /api/v1/conversions)"]
        
        subgraph CLEAN_VSA["Kiến Trúc Clean Architecture + Vertical Slice"]
            SYNC_SLICE["Luồng Ký Số (Sync)\n- ConvertSync Handler\n- Semaphore Limiter (Max 4)"]
            ASYNC_SLICE["Luồng Bất Đồng Bộ (Async)\n- SubmitConversion\n- GetStatus / Download / Delete"]
            WORKERS["Background Workers\n- ConversionWorker (4 threads)\n- CleanupWorker (TTL 60 min)"]
        end

        TEMP_DISK[("Volume đĩa tạm Gateway\n(/app/storage)\n(Chỉ giữ tạm file cho Async)")]
    end

    subgraph QUEUE_ZONE["3. HÀNG ĐỢI & TRẠNG THÁI (Mạng Internal)"]
        VALKEY[("Valkey 8 / Redis\n- queue:pending\n- queue:processing\n- job:metadata")]
    end

    subgraph ENGINE_ZONE["4. CỤM CÔNG CỤ CHUYỂN ĐỔI (GOTENBERG CLUSTER)"]
        LB["Nginx Load Balancer (Port 3000)\n- Thuật toán: least_conn\n- Zero-buffering streaming"]
        
        subgraph WORKER_POOL["Gotenberg Workers (4 Containers độc lập)"]
            W1["Gotenberg 1\n(LibreOffice 24.8)\n1 vCPU / 1GB RAM"]
            W2["Gotenberg 2\n(LibreOffice 24.8)\n1 vCPU / 1GB RAM"]
            W3["Gotenberg 3\n(LibreOffice 24.8)\n1 vCPU / 1GB RAM"]
            W4["Gotenberg 4\n(LibreOffice 24.8)\n1 vCPU / 1GB RAM"]
        end
    end

    %% Luồng Ký số (Sync)
    BE -->|"1. POST /conversions/sync\n(Gửi file Office: docx, xlsx...)"| API
    API -->|"2a. Chuyển đổi trực tiếp"| SYNC_SLICE
    SYNC_SLICE -->|"3a. Stream file HTTP POST"| LB
    LB -->|"4a. least_conn"| WORKER_POOL
    WORKER_POOL -->|"5a. Trả về stream PDF"| LB
    LB -->|"6a. Stream binary PDF"| SYNC_SLICE
    SYNC_SLICE -->|"7a. HTTP 200 + File PDF ngay (140-500ms)"| BE
    BE -.->|"8a. Tự lưu PDF vào ổ đĩa Host"| HOST_DISK

    %% Luồng Upload Hàng đợi (Async)
    BE -->|"1b. POST /conversions\n(Upload tài liệu lớn / nền)"| API
    API -->|"2b. Đẩy việc vào Queue"| ASYNC_SLICE
    ASYNC_SLICE -->|"3b. Lưu file tạm"| TEMP_DISK
    ASYNC_SLICE -->|"4b. Enqueue JobId"| VALKEY
    ASYNC_SLICE -->|"5b. HTTP 202 Accepted + JobId"| BE
    
    VALKEY -->|"6b. RPOPLPUSH bốc Job"| WORKERS
    WORKERS -->|"7b. Đọc file tạm"| TEMP_DISK
    WORKERS -->|"8b. Gọi Nginx chuyển đổi"| LB
    WORKERS -->|"9b. Ghi kết quả PDF tạm"| TEMP_DISK
    WORKERS -->|"10b. Đánh dấu Completed"| VALKEY

    BE -->|"11b. GET /conversions/{id} (Polling)"| API
    BE -->|"12b. GET /conversions/{id}/file"| API
    API -->|"13b. Đọc file PDF trả về BE"| TEMP_DISK
    BE -->|"14b. DELETE /conversions/{id}\n(Xóa file tạm Gateway)"| API
    BE -.->|"15b. Tự lưu PDF vào ổ đĩa Host"| HOST_DISK
    WORKERS -.->|"Dọn dẹp file rác > 60 phút"| TEMP_DISK
```

---

## 2. Chi Tiết Hai Luồng Nghiệp Vụ Cốt Lõi

### 2.1. Luồng 1: Ký Số Trực Tiếp (Synchronous Flow - Ký Tức Thì)
Dành cho người dùng đang thao tác trên giao diện, cần file PDF trong vòng **vài trăm mili-giây** để ký token / hiển thị xem trước.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng / BE Client
    participant GW as Gateway (.NET 10)
    participant LB as Nginx Load Balancer
    participant W as Gotenberg Worker (LibreOffice)
    actor HostDisk as Ổ đĩa Host BE

    User->>GW: POST /api/v1/conversions/sync (kèm file .docx)
    Note over GW: Semaphore kiểm tra slot (Tối đa 4 kết nối đồng thời)
    GW->>LB: Stream multipart/form-data qua HTTP POST
    LB->>W: Điều phối least_conn đến worker đang rảnh nhất
    Note over W: LibreOffice convert file sang PDF (~140ms - 500ms)
    W-->>LB: Trả về binary PDF
    LB-->>GW: Stream nhị phân trực tiếp (Zero-buffering)
    GW-->>User: HTTP 200 OK + File PDF binary
    User->>HostDisk: BE tự lưu file PDF vào thư mục ổ cứng máy Host
```

> **Đặc điểm**:
> - Không ghi ổ cứng tạm, không đẩy qua Redis Queue.
> - Dữ liệu truyền dạng Stream từ đầu đến cuối, Gateway không bị phình RAM (Zero LOH fragmentation).

---

### 2.2. Luồng 2: Upload Hàng Đợi (Asynchronous Flow - Xử Lý Nền)
Dành cho upload tài liệu lớn (10MB - 100MB), tài liệu nhiều trang hoặc xử lý lô (batch).

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng / BE Client
    participant GW as Gateway (.NET 10)
    participant Redis as Valkey (Queue & State)
    participant Disk as Đĩa tạm Gateway (/app/storage)
    participant Worker as ConversionWorker (Background Pool)
    participant Gotenberg as Cụm Gotenberg
    actor HostDisk as Ổ đĩa Host BE

    User->>GW: POST /api/v1/conversions (Upload file)
    GW->>Disk: Lưu file tạm đầu vào
    GW->>Redis: Lưu Job State (Pending) & Push JobId vào pending_queue
    GW-->>User: HTTP 202 Accepted + JobId (kèm Header Location)

    loop Worker Loop (4 threads song song)
        Worker->>Redis: RPOPLPUSH bốc JobId (Reliable Queue)
        Worker->>Disk: Đọc file tạm
        Worker->>Gotenberg: Gửi chuyển đổi sang Gotenberg
        Gotenberg-->>Worker: Trả về PDF kết quả
        Worker->>Disk: Lưu file PDF kết quả tạm
        Worker->>Redis: Cập nhật Job State = Completed & Acknowledge
    end

    User->>GW: GET /api/v1/conversions/{id} (Poll trạng thái)
    GW-->>User: HTTP 200 OK (Status = Completed)

    User->>GW: GET /api/v1/conversions/{id}/file
    GW->>Disk: Đọc file PDF
    GW-->>User: HTTP 200 OK + Tải file PDF về máy

    User->>HostDisk: BE tự lưu file PDF vào máy Host của BE
    User->>GW: DELETE /api/v1/conversions/{id}
    GW->>Disk: Xóa sạch file tạm đầu vào và file PDF trên Gateway
    GW-->>User: HTTP 200 OK (Cleaned up)
```

---

## 3. Kiến Trúc Mã Nguồn Clean Architecture & VSA (.NET 10)

Mã nguồn được phân tách tuyệt đối thành 4 project:

```
src/
├── ConverterService.Domain/                     # [Tầng 1: Độc lập 100%, chứa Rule nghiệp vụ]
│   ├── Common/                                 # Result<T>, Error
│   └── Conversions/                            # ConversionJob, Value Objects, Domain Errors
│
├── ConverterService.Application/                # [Tầng 2: Vertical Slice Architecture]
│   ├── Abstractions/                           # Interface trừu tượng (IConversionQueue, IDocumentConverter...)
│   └── Features/Conversions/                   # Mỗi chức năng là 1 Vertical Slice khép kín
│       ├── ConvertSync/                        # Slice Ký số (Request, Response, Handler, Validator)
│       ├── SubmitConversion/                   # Slice Tạo Job Async
│       ├── GetConversion/                      # Slice Xem trạng thái Job
│       ├── DownloadConversionResult/           # Slice Tải file PDF
│       ├── DeleteConversion/                   # Slice Xóa dọn dẹp file
│       ├── ProcessConversion/                  # Slice Worker xử lý chuyển đổi
│       └── CleanupOrphans/                     # Slice Thu hồi job mồ côi
│
├── ConverterService.Infrastructure/             # [Tầng 3: Hiện thực kỹ thuật & Hạ tầng ngoài]
│   ├── Gotenberg/                              # GotenbergDocumentConverter, GotenbergHealthCheck
│   ├── Persistence/                            # Redis (Valkey) Queue & Repository, LocalDiskStorage
│   ├── Conversion/                             # SemaphoreConversionSlotLimiter (Rate limiting)
│   └── BackgroundJobs/                         # ConversionWorker (4 pool threads), CleanupWorker (TTL 60m)
│
└── ConverterService.Api/                        # [Tầng 4: Điểm tiếp nhận request REST API]
    ├── Controllers/                            # ConversionsController
    ├── Common/                                 # ApiResponse<T> chuẩn hóa toàn hệ thống
    └── Middleware/                             # GlobalExceptionHandler, ProblemDetails
```

---

## 4. Chú Thích Các Thành Phần Kỹ Thuật

1. **Backend Host (Máy chủ ứng dụng)**:
   - Thay vì nạp DLL Word nặng nề chạy ngốn RAM trên máy chính, BE chỉ gửi 1 request REST API sang Gateway.
   - Khi có kết quả, BE lưu file PDF trực tiếp vào thư mục đĩa cứng của BE. Không cần cài đặt MinIO/S3.
2. **Gateway (.NET 10)**:
   - Điểm trung chuyển duy nhất, expose cổng `8080`.
   - Sử dụng mô hình bộ nhớ Streaming, chỉ tốn **~72 - 89 MB RAM** cho tải hàng trăm MB file nhị phân.
3. **Nginx Load Balancer (`least_conn`)**:
   - Điều phối yêu cầu sang container nào đang ít kết nối nhất. Ngăn chặn triệt để tình trạng một file 15MB làm nghẽn các file nhỏ 50KB.
4. **Cụm 4 Worker Gotenberg (LibreOffice 24.8)**:
   - Tách thành 4 container độc lập tương ứng 4 tiến trình LibreOffice riêng biệt.
   - Cơ chế tự động giải phóng RAM sau mỗi 10 tài liệu, không bao giờ lo LibreOffice rò rỉ bộ nhớ.
5. **Valkey 8 (Redis tương thích)**:
   - Lưu trữ hàng đợi chuyển đổi tin cậy với cơ chế `RPOPLPUSH`. Kể cả khi Gateway bị tắt đột ngột, job không bao giờ bị mất (sẽ được tự động khôi phục khi Gateway bật lại).
