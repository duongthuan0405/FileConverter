---
name: template-skill
description: >-
  Mẫu skill chuẩn cho Agent. Sử dụng làm khuôn mẫu khi tạo mới các kỹ năng chuyên biệt cho dự án.
---

# Tên Kỹ Năng (Skill Name)

Mô tả ngắn gọn mục đích của skill, quy trình xử lý và mục tiêu cần đạt được.

## Khi nào sử dụng (When to use)
- Nêu rõ các trường hợp hoặc câu lệnh của người dùng kích hoạt skill này.
- Ví dụ: Khi người dùng yêu cầu xử lý chuyển đổi tài liệu, chạy pipeline kiểm thử, hoặc triển khai dịch vụ.

## Quy trình thực hiện từng bước (Step-by-Step Workflow)

1. **Bước 1: Chuẩn bị & Kiểm tra tiền điều kiện**
   - Kiểm tra các công cụ, tệp tin hoặc biến môi trường cần thiết.
   - Gọi script chuẩn bị nếu cần: [helper.ps1](./scripts/helper.ps1)

2. **Bước 2: Thực thi tác vụ chính**
   - Hướng dẫn chi tiết cho Agent thực hiện lệnh hoặc thao tác code.
   - Tham khảo các ví dụ mẫu: [example_usage.md](./examples/example_usage.md)

3. **Bước 3: Xác minh kết quả (Validation)**
   - Hướng dẫn Agent cách kiểm tra xem tác vụ đã thành công hay chưa (mã trả về, file kết quả, log hệ thống).

## Tài liệu tham khảo (References)
- Các tài liệu chi tiết, đặc tả kỹ thuật đặt tại: [api_reference.md](./references/api_reference.md)

