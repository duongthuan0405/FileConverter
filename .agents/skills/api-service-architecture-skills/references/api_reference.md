# Đặc Tả Kiến Trúc & Quy Ước Mã Nguồn (API Reference & Conventions)

Tài liệu này cung cấp các nguyên tắc chi tiết khi viết mã cho hệ thống `FileConverterService`.

---

## 1. Nguyên Tắc Thiết Kế (Design Principles)

1. **Dependency Inversion Principle (DIP):**
   - Các module cấp cao (Application, Api) không được phụ thuộc trực tiếp vào các module cấp thấp (Infrastructure). Cả hai phải phụ thuộc vào abstraction (Interfaces).
2. **Single Responsibility Principle (SRP):**
   - Mỗi Controller chỉ xử lý HTTP request/response routing.
   - Mỗi Service chỉ đảm nhận một nhóm nghiệp vụ logic hoặc một tích hợp duy nhất.
3. **Explicit Error Handling:**
   - Không throw exception không kiểm soát ra ngoài client.
   - Các lỗi validation hoặc logic được trả về qua `ApiErrorResponse`.

---

## 2. Quy Ước Đặt Tên (Naming Conventions)

| Đối tượng | Quy ước | Ví dụ |
| :--- | :--- | :--- |
| **Interfaces** | Tiền tố `I` + PascalCase | `IFileConverterService`, `IGotenbergClient` |
| **DTOs** | PascalCase + hậu tố `RequestDto` hoặc `ResultDto` / `ResponseDto` | `ConvertFileRequestDto`, `ConvertResultDto` |
| **Controllers** | PascalCase + hậu tố `Controller` | `ConvertController`, `CheckController` |
| **Services (Implementation)**| PascalCase + mô tả công nghệ/nghiệp vụ | `GotenbergConverterService`, `LocalStorageService` |
| **Enums** | Số ít + PascalCase | `DocumentFormat`, `TaskStatus` |

---

## 3. Cấu Trúc Thư Mục Chuẩn Từng Tầng

```text
FileConverterService/
├── FileConverterService.Domain/
│   ├── Entities/                 # Các thực thể nghiệp vụ
│   ├── Enums/                    # Các enum loại file, trạng thái
│   ├── Exceptions/               # Domain-specific exceptions
│   └── ValueObjects/             # Đối tượng giá trị bất biến
│
├── FileConverterService.Application/
│   ├── Common/                   # Tiện ích chung, mappings
│   ├── DTOs/                     # Data Transfer Objects
│   ├── Interfaces/               # Interfaces cho Services, Repositories, Clients
│   └── Services/                 # Logic nghiệp vụ ứng dụng
│
├── FileConverterService.Infrastructure/
│   ├── ExternalServices/         # Tích hợp Gotenberg, LibreOffice, v.v.
│   ├── Storage/                  # Lưu trữ file hệ thống hoặc Cloud
│   ├── Services/                 # Hiện thực hóa các interface từ Application
│   └── DependencyInjection.cs    # Phương thức mở rộng AddInfrastructureServices
│
└── FileConverterService.Api/
    ├── ApiResponseModels/        # Mô hình chuẩn hóa ApiResponse, ApiSuccessResponse, ApiErrorResponse
    ├── Controllers/              # RESTful API Controllers
    ├── Middlewares/              # Exception handler, logging middleware
    └── Program.cs                # Entry point & cấu hình pipeline
```

---

## 4. Mô Hình ApiResponse Chuẩn

```csharp
// Chuẩn trả về khi thành công
public class ApiSuccessResponse<T> : ApiResponse<T>
{
    public override bool Success => true;
    public string Message { get; set; }
    public T? Data { get; set; }
}

// Chuẩn trả về khi gặp lỗi
public class ApiErrorResponse : ApiResponse<Dictionary<string, List<string>>>
{
    public override bool Success => false;
    public string Message { get; set; }
    public Dictionary<string, List<string>>? Data { get; set; }
}
```
