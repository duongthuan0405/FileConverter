# Cleanup tài nguyên job bằng Redis Sorted Set

CleanupWorker đọc các job tới hạn từ `REDIS:CleanupKey`, mặc định `fileconverter:cleanup`. Không quét metadata bằng SCAN.

## Cấu hình

```json
"CLEANUP": {
  "RetentionMinutes": 10,
  "IntervalSeconds": 30,
  "BatchSize": 100
}
```

Ghi đè bằng biến môi trường `CLEANUP__RetentionMinutes`, `CLEANUP__IntervalSeconds`, `CLEANUP__BatchSize`, `REDIS__CleanupKey`. Cấu hình được validate khi khởi động.

- RetentionMinutes: thời gian giữ input/PDF/metadata sau CompletedAt.
- IntervalSeconds: khoảng chờ sau mỗi batch, không bao gồm thời gian xử lý batch.
- BatchSize: số job tối đa lấy trong mỗi vòng; job còn lại được xử lý ở các vòng tiếp theo.

## Luồng

1. Conversion worker gọi SaveTerminalAsync: cùng một Lua script ghi metadata Completed/Failed và ZADD vào cleanup set, sau đó worker ACK riêng.
2. Member là jobId dạng N; score là `(CompletedAt + RetentionMinutes).ToUnixTimeMilliseconds()`.
3. CleanupWorker đọc tối đa BatchSize phần tử có score <= hiện tại, theo thứ tự score tăng dần, offset luôn là 0. Thao tác đọc không claim hoặc xóa member.
4. JobCleanupService kiểm tra metadata và trạng thái Completed/Failed. Hạn xóa được quyết định bởi score đã lưu; không tính lại từ CompletedAt. Pending/Processing được giữ nguyên.
5. Xóa PDF, xóa input, rồi gọi DeleteJobAsync. Lua kiểm tra trạng thái, ZREM member và DEL metadata trong cùng script.
6. API DELETE dùng chung service và bước xóa Redis này, nhưng không phải đợi TTL. Job không tồn tại trả 404, active job trả 409.
7. Nếu xóa file lỗi, giữ metadata và member để vòng sau thử lại. Nếu metadata đã mất, cleanup tự động bỏ member tồn đọng.

Lua chạy không bị client khác xen ngang, nhưng không tự rollback mọi thay đổi nếu gặp lỗi runtime. Không có transaction xuyên Redis và filesystem. File phải được xóa trước khi xóa thông tin Redis.

## Phạm vi và vận hành

- Job kết thúc trước khi có cơ chế lập lịch, nhưng chưa có member trong set, không tự được tìm lại. Chưa triển khai backfill; có thể dọn bằng API DELETE.
- Các instance có thể đọc cùng batch, không có distributed cleanup lock. Thao tác xóa chịu được chạy lặp, dựa trên việc terminal job không thay đổi file key và không tái sử dụng job ID. Các instance dùng chung namespace Redis cần storage dùng chung.
- Job lỗi hoặc bị bỏ qua được dời score tới thời điểm hiện tại + 2 × IntervalSeconds (mặc định 60 giây), nhường vòng kế tiếp cho job khác. ZADD XX GT chỉ dời member đang tồn tại về sau, không tạo lại member đã bị xóa. Chưa có claim độc quyền hoặc dead-letter cho cleanup.
- Thay đổi retention cần restart; score đã lưu không tự được tính lại. Retention mới chỉ dùng khi ghi lịch mới, không thay đổi hạn xóa của các member đã có.
- File temp/orphan không có metadata, Pending/Processing bị treo và receipt tồn đọng chưa thuộc phạm vi này.
- Client cần tải và lưu PDF trước khi hết hạn hoặc gọi DELETE ngay sau khi tải xong. Không kéo dài TTL theo download. Nếu hệ điều hành từ chối xóa file đang mở, cleanup giữ dữ liệu để thử lại.
- Các Lua script nhiều key yêu cầu cùng hash slot khi dùng Redis Cluster, tương tự submit hiện tại.

## Kiểm chứng

Các project kiểm thử .NET đã được gỡ. Các script thử nghiệm Gotenberg trong LibreOffice vẫn được giữ.

Retry cleanup: ExecuteAsync trả false hoặc ném exception thì worker hoãn member còn tồn tại thêm hai chu kỳ. Metadata không còn và member đã được dọn thì thao tác hoãn không tạo lại member. Nếu Redis lỗi khiến hoãn thất bại, worker log lỗi và tiếp tục các job khác; việc dời lịch cần Redis hoạt động.
