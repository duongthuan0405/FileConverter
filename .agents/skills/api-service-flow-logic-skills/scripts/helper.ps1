param (
    [string]$Action = "test-validation",
    [string]$BaseUrl = "http://localhost:5156"
)

switch ($Action.ToLower()) {
    "test-health" {
        Write-Host "Kiểm tra Health Endpoint tại $BaseUrl/check/health..." -ForegroundColor Cyan
        try {
            $res = Invoke-RestMethod -Uri "$BaseUrl/check/health" -Method Get
            $res | ConvertTo-Json
        } catch {
            Write-Error "Không thể kết nối đến $BaseUrl. Đảm bảo API đang chạy."
        }
    }
    "test-validation" {
        Write-Host "Kiểm thử luồng Validation với file rỗng..." -ForegroundColor Cyan
        try {
            # Giả lập gửi request không có file
            $res = Invoke-WebRequest -Uri "$BaseUrl/api/converter/convert" -Method Post -SkipHttpErrorCheck
            Write-Host "Mã phản hồi HTTP: $($res.StatusCode)" -ForegroundColor Yellow
            $res.Content
        } catch {
            Write-Host "Lỗi kết nối hoặc API chưa khởi chạy." -ForegroundColor Red
        }
    }
    default {
        Write-Host "Cách dùng: ./helper.ps1 -Action [test-health | test-validation]" -ForegroundColor Gray
    }
}
