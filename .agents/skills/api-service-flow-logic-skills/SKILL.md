---
name: api-service-flow-logic-skills
description: >-
  Tra cứu sơ đồ luồng nghiệp vụ chi tiết (Business Flow Logic) cho từng tính năng trong dự án FileConverterService.
  BẮT BUỘC sử dụng khi coder yêu cầu triển khai một tính năng nghiệp vụ cụ thể (ví dụ: chuyển đổi Office sang PDF,
  HTML sang PDF, Đăng nhập/Xác thực, kiểm tra Readiness) để tra cứu sơ đồ tuần tự (Sequence Diagram), DTO, và quy tắc xử lý trong references/flows/.
---

# Trung Tâm Tra Cứu Luồng Nghiệp Vụ Chi Tiết (Business Flow Catalog)

Skill này là **Bản đồ chỉ dẫn và danh mục sơ đồ luồng nghiệp vụ chi tiết** của hệ thống. 

Khi coder yêu cầu triển khai bất kỳ tính năng nghiệp vụ nào, Agent **BẮT BUỘC** phải tra cứu bảng bên dưới để mở tệp đặc tả luồng chi tiết tương ứng trong thư mục `references/flows/`, đọc kỹ sơ đồ tuần tự (Sequence Diagram) và các ràng buộc dữ liệu trước khi viết code.

---

## 1. Bảng Tra Cứu Luồng Nghiệp Vụ (Flow Registry Table)

| Mã Luồng | Nghiệp Vụ Cần Triển Khai | Tệp Sơ Đồ & Đặc Tả Chi Tiết | Mô Tả & Công Nghệ |
| :--- | :--- | :--- | :--- |
| **`FLOW-DOC-01`** | **Toàn bộ luồng hệ thống** | [all-flow.md](./references/flows/all-flow.md) | Toàn bộ luồng. |

---
