---
name: api-service-architecture-skills
description: >-
  Áp dụng khi thiết kế, triển khai tính năng mới, tạo controller, use case, domain entity, hoặc service tích hợp trong dự án FileConverterService (.NET 10) tuân thủ nghiêm ngặt chuẩn Clean Architecture và ApiResponseModels.
---

# Kỹ Năng Phát Triển Theo Kiến Trúc FileConverterService

Tài liệu này định nghĩa quy trình chuẩn khi phát triển mã nguồn trong dự án **FileConverterService** (.NET 10), đảm bảo tuân thủ mô hình **Clean Architecture (Onion Architecture)** và chuẩn hóa phản hồi API.

---

## 1. Bản Đồ Phụ Thuộc (Dependency Rules)

```
[Api] ---> [Application] <--- [Infrastructure]
               |
               v
           [Domain]
```

- **Domain (`FileConverterService.Domain`)**: Core nghiệp vụ, độc lập hoàn toàn, **không** phụ thuộc vào bất kỳ project nào.
- **Application (`FileConverterService.Application`)**: Chứa Use Cases, DTOs, Interfaces. Chỉ phụ thuộc vào `Domain`.
- **Infrastructure (`FileConverterService.Infrastructure`)**: Hiện thực hóa các interface (I/O, Gotenberg Client, File System, Cloud Storage). Phụ thuộc vào `Application` và `Domain`.
- **Api (`FileConverterService.Api`)**: Presentation layer, tiếp nhận HTTP Request. Phụ thuộc vào `Application` và `Infrastructure` (để cấu hình Dependency Injection).

> [!IMPORTANT]
> **Quy tắc bất biến:** Không bao giờ gọi trực tiếp tầng `Infrastructure` hoặc các chi tiết công nghệ (I/O, HttpClient) từ tầng `Domain` hoặc `Application`. Mọi phụ thuộc phải đảo ngược qua Interface (Dependency Inversion Principle).

---

## 2. Quy Chuẩn Phản Hồi API (API Response Standard)

Mọi Endpoint trong `Api.Controllers` bắt buộc phải trả về kiểu dữ liệu đóng gói trong `ApiResponseModels`:

- **Thành công:** Trả về `ApiSuccessResponse<T>`:
  ```csharp
  var result = await _converterService.ConvertAsync(request, cancellationToken);
  return Ok(new ApiSuccessResponse<ConvertResultDto>(result, "Chuyển đổi tệp thành công"));
  ```
- **Thất bại / Lỗi nghiệp vụ:** Trả về `ApiErrorResponse`:
  ```csharp
  return BadRequest(new ApiErrorResponse("Định dạng tệp không được hỗ trợ", new Dictionary<string, List<string>>
  {
      { "FileExtension", new List<string> { "Chỉ chấp nhận các định dạng .docx, .xlsx, .pptx" } }
  }));
  ```

---

## 3. Quy Trình Triển Khai Tính Năng Mới (Step-by-Step Workflow)

Khi người dùng yêu cầu tạo tính năng mới (ví dụ: Chuyển đổi tài liệu sang PDF, Lưu trữ tệp, Quản lý tác vụ):

### Bước 1: Khởi tạo Core tại Domain (nếu có nghiệp vụ mới)
- Thư mục: `FileConverterService.Domain/`
- Tạo các Enums (loại định dạng, trạng thái), Value Objects hoặc Entities.
- Đảm bảo không sử dụng các namespace liên quan đến ASP.NET Core hay Entity Framework Core tại đây.

### Bước 2: Thiết kế Hợp đồng tại Application
- Thư mục: `FileConverterService.Application/`
- Định nghĩa DTOs cho Request và Response.
- Định nghĩa Interface cho nghiệp vụ (ví dụ: `IFileConverterService`, `IGotenbergClient`).
- Xây dựng Use Cases / MediatR Handlers (theo chuẩn Clean Architecture / CQRS).
- **Quy tắc Validation:** Mọi Validator kế thừa `AbstractValidator<T>` **bắt buộc** phải sử dụng chuỗi thông báo lỗi tập trung tại `FileConverterService.Application.Constants.BussinessRuleMessage` (không hardcode chuỗi thông báo trong validator).

### Bước 3: Hiện thực hóa tại Infrastructure
- Thư mục: `FileConverterService.Infrastructure/`
- Tạo Class hiện thực hóa Interface (ví dụ: `GotenbergConverterService : IFileConverterService`).
- Xử lý các logic kỹ thuật: HTTP call sang Gotenberg (port 3000), Stream handling, Disk I/O.
- Đăng ký DI thông qua Extension Method:
  ```csharp
  public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
  ```

### Bước 4: Cung cấp API tại tầng Api
- Thư mục: `FileConverterService.Api/Controllers/`
- Khởi tạo Controller kế thừa `ControllerBase` với route chuẩn RESTful.
- Inject Interface từ tầng `Application` (không inject trực tiếp class cụ thể từ `Infrastructure`).
- Chuẩn hóa đầu ra với `ApiSuccessResponse<T>` hoặc `ApiErrorResponse`.
- Đăng ký service vào `Program.cs` nếu cần.

### Bước 5: Kiểm tra và Xác minh (Validation)
1. Chạy script kiểm tra build toàn bộ solution:
   ```powershell
   ./scripts/helper.ps1 -Action build
   ```
2. Kiểm tra format và chạy thử endpoint bằng tệp HTTP hoặc curl:
   - [FileConverterService.Api.http](file:///d:/Projects/FileConverter/FileConverterService/FileConverterService.Api/FileConverterService.Api.http)

---

## 4. Tài Liệu Tham Khảo Thêm
- Tham khảo code mẫu chi tiết từng tầng: [example_usage.md](./examples/example_usage.md)
- Đặc tả kỹ thuật và quy ước đặt tên: [api_reference.md](./references/api_reference.md)
- Cấu hình mẫu cho Gotenberg & Service: [sample_config.json](./resources/sample_config.json)
