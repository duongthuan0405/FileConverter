---
name: gotenberg-capacity-dispatch
description: Thiết kế và tra cứu cơ chế giữ chỗ trước dequeue, điều phối nginx và phục hồi lỗi để mỗi container Gotenberg chỉ nhận một conversion tại một thời điểm trong FileConverterService.
---

# Điều phối Gotenberg theo container rảnh

Đọc [kiến trúc và luồng xử lý](references/architecture.md) khi thiết kế hoặc triển khai admission, Worker, queue, retry hoặc nginx cho cụm Gotenberg.

Đây là **đề xuất kiến trúc**, chưa được triển khai. Việc tạo tài liệu này không cho phép tự động sửa code hoặc triển khai hạ tầng; làm theo phạm vi yêu cầu trong phiên làm việc tiếp theo.

Các ràng buộc phải giữ khi triển khai:

- Mỗi container có tối đa một conversion đang chạy, tính chung mọi Worker và mọi replica, kể cả luồng sync nếu có.
- Worker chỉ claim job khi đã giữ được một container cụ thể. Hết capacity: job vẫn ở pending queue.
- Nginx là đầu mối chọn container và chuyển request. Admission và routing phải dùng cùng reservation; không chọn lại bằng `least_conn` sau khi giữ chỗ.
- `least_conn`, semaphore trong một process, health check và TTL lock riêng lẻ không chứng minh được container rảnh.
- Timeout, hủy request, mất heartbeat hoặc Redis mất trạng thái không tự động giải phóng container đang chạy. Cách ly đến khi xác nhận kết thúc hoặc dừng được generation cũ.
- Tài liệu hiện tại trong `api-service-flow-logic-skills/references/flows/all-flow.md` là mô hình cũ; khi xử lý capacity, dùng đặc tả này để bổ sung và sửa các giả định cũ.

Khi thay đổi phương án, nêu rõ cách bảo đảm giữ chỗ nguyên tử, token dùng một lần, routing đến đúng node, chặn request trùng và phục hồi conversion chưa biết kết quả. Không tuyên bố nginx thuần đáp ứng toàn bộ các yêu cầu đó chỉ bằng cấu hình upstream.
