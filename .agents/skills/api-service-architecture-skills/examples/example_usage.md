# Ví Dụ Mẫu Triển Khai Tính Năng (End-to-End Example)

Tài liệu này minh họa cách thêm tính năng **Chuyển đổi tệp Office sang PDF** qua 4 tầng của kiến trúc Clean Architecture.

---

## 1. Tầng Domain: `FileConverterService.Domain`

### File: `Enums/DocumentFormat.cs`
```csharp
namespace FileConverterService.Domain.Enums
{
    public enum DocumentFormat
    {
        Docx,
        Doc,
        Xlsx,
        Xls,
        Pptx,
        Ppt,
        Pdf
    }
}
```

---

## 2. Tầng Application: `FileConverterService.Application`

### File: `DTOs/ConvertFileRequestDto.cs`
```csharp
namespace FileConverterService.Application.DTOs
{
    public class ConvertFileRequestDto
    {
        public required Stream FileStream { get; set; }
        public required string FileName { get; set; }
        public required string TargetExtension { get; set; } = "pdf";
    }
}
```

### File: `DTOs/ConvertFileResultDto.cs`
```csharp
namespace FileConverterService.Application.DTOs
{
    public class ConvertFileResultDto
    {
        public required byte[] FileBytes { get; set; }
        public required string OutputFileName { get; set; }
        public required string ContentType { get; set; }
    }
}
```

### File: `Interfaces/IFileConverterService.cs`
```csharp
using FileConverterService.Application.DTOs;

namespace FileConverterService.Application.Interfaces
{
    public interface IFileConverterService
    {
        Task<ConvertFileResultDto> ConvertToPdfAsync(ConvertFileRequestDto request, CancellationToken cancellationToken = default);
    }
}
```

---

## 3. Tầng Infrastructure: `FileConverterService.Infrastructure`

### File: `Services/GotenbergConverterService.cs`
```csharp
using System.Net.Http.Headers;
using FileConverterService.Application.DTOs;
using FileConverterService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FileConverterService.Infrastructure.Services
{
    public class GotenbergConverterService : IFileConverterService
    {
        private readonly HttpClient _httpClient;
        private readonly string _gotenbergUrl;

        public GotenbergConverterService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _gotenbergUrl = configuration["Gotenberg:Url"] ?? "http://localhost:3000";
        }

        public async Task<ConvertFileResultDto> ConvertToPdfAsync(ConvertFileRequestDto request, CancellationToken cancellationToken = default)
        {
            using var formData = new MultipartFormDataContent();
            using var fileContent = new StreamContent(request.FileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            formData.Add(fileContent, "files", request.FileName);

            // Gotenberg LibreOffice conversion endpoint
            var endpoint = $"{_gotenbergUrl}/forms/libreoffice/convert";
            var response = await _httpClient.PostAsync(endpoint, formData, cancellationToken);

            response.EnsureSuccessStatusCode();

            var pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var outputName = Path.GetFileNameWithoutExtension(request.FileName) + ".pdf";

            return new ConvertFileResultDto
            {
                FileBytes = pdfBytes,
                OutputFileName = outputName,
                ContentType = "application/pdf"
            };
        }
    }
}
```

### File: `DependencyInjection.cs`
```csharp
using FileConverterService.Application.Interfaces;
using FileConverterService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileConverterService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient<IFileConverterService, GotenbergConverterService>();
            return services;
        }
    }
}
```

---

## 4. Tầng Api: `FileConverterService.Api`

### Đăng ký Service tại `Program.cs`
```csharp
using FileConverterService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
// Đăng ký tầng Infrastructure
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();
```

### File: `Controllers/ConverterController.cs`
```csharp
using FileConverterService.Api.ApiResponseModels;
using FileConverterService.Application.DTOs;
using FileConverterService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FileConverterService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConverterController : ControllerBase
    {
        private readonly IFileConverterService _converterService;

        public ConverterController(IFileConverterService converterService)
        {
            _converterService = converterService;
        }

        [HttpPost("to-pdf")]
        public async Task<IActionResult> ConvertToPdf(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiErrorResponse("Tệp tin tải lên không hợp lệ.", new Dictionary<string, List<string>>
                {
                    { "File", new List<string> { "Vui lòng chọn một tệp hợp lệ để chuyển đổi." } }
                }));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var request = new ConvertFileRequestDto
                {
                    FileStream = stream,
                    FileName = file.FileName,
                    TargetExtension = "pdf"
                };

                var result = await _converterService.ConvertToPdfAsync(request, cancellationToken);
                return File(result.FileBytes, result.ContentType, result.OutputFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiErrorResponse($"Lỗi trong quá trình chuyển đổi: {ex.Message}"));
            }
        }
    }
}
```
