# Yêu cầu triển khai tính năng: Chat & Hỏi đáp (RAG System)

## 1. Vai trò (Role)
Bạn là một Senior Backend Developer chuyên gia về C#, ASP.NET Core, Entity Framework Core và hệ thống RAG (Retrieval-Augmented Generation) sử dụng PostgreSQL (pgvector).

## 2. Mục tiêu (Objective)
Triển khai hoàn chỉnh luồng API cho tính năng Chat & Hỏi đáp dựa trên tài liệu đã được nhúng vector. Tính năng cần đáp ứng đúng 4 tiêu chí cốt lõi sau:
1. Lịch sử hội thoại theo phiên.
2. Giới hạn trả lời trong phạm vi tài liệu (Grounded generation).
3. Trích dẫn nguồn tài liệu gốc.
4. Chat tự nhiên theo ngữ cảnh hội thoại.

## 3. Hiện trạng Database (Current Context)
- Hệ thống đang sử dụng PostgreSQL với extension `pgvector`.
- Đã có sẵn bảng `document_chunks` chứa dữ liệu văn bản đã cắt nhỏ, có cột `embedding public.vector(3072)`.
- Đã có sẵn bảng `documents` và `document_chapters`.

## 4. Các công việc yêu cầu (Tasks)

### Task 1: Thiết kế Database cho Lịch sử Chat (Session-based history)
Tạo code cho 2 Entities mới và setup Fluent API (DbContext) cho chúng:
- `ChatSession`: `Id` (Guid), `UserId` (Guid), `Title` (string), `CreatedAt` (DateTime).
- `ChatMessage`: `Id` (Guid), `SessionId` (Guid - FK), `Role` (enum: System, User, Assistant), `Content` (string), `CreatedAt` (DateTime).

### Task 2: Triển khai Vector Retrieval (Tìm kiếm ngữ nghĩa)
Viết hàm tìm kiếm các `document_chunks` liên quan đến câu hỏi của User.
- Input: Câu hỏi của User (đã được embed thành Vector 3072 chiều) + `DocumentId` (nếu có để filter).
- Logic: Dùng EF Core gọi hàm tính toán khoảng cách (Cosine Similarity) của pgvector để lấy ra Top K (ví dụ: 5) chunks sát nghĩa nhất.
- Yêu cầu: Include thông tin của `Document` và `DocumentChapter` (để lấy metadata làm trích dẫn).

### Task 3: Xây dựng Prompt Engineering (RAG System Prompt)
Viết hàm tạo System Prompt chặt chẽ để đạt 2 tiêu chí: "Giới hạn trong tài liệu" và "Trích dẫn nguồn".
- **Cấu trúc Prompt yêu cầu:**
  - Định dạng Agent là người hỗ trợ thông tin nội bộ.
  - LUẬT: CHỈ dùng thông tin trong thẻ [CONTEXT] để trả lời. TUYỆT ĐỐI không bịa thông tin. Nếu không có, trả lời "Không tìm thấy thông tin".
  - LUẬT: Cuối mỗi câu/đoạn cung cấp thông tin, bắt buộc phải trích dẫn theo format: `(Nguồn: [Tên tài liệu] - [Tên chương/Trang])`.
  - [CONTEXT]: Nhúng nội dung Top K chunks lấy được từ Task 2 vào đây.

### Task 4: Xây dựng Chat API (Main Logic)
Tạo API Endpoint (ví dụ: `POST /api/chat/message`) ghép nối toàn bộ luồng:
1. Nhận request gồm: `SessionId` (có thể null nếu tạo mới), `Message` (string), `DocumentId` (optional).
2. Lưu tin nhắn User vào DB.
3. Lấy ra lịch sử 5-10 tin nhắn gần nhất của `SessionId` này (đáp ứng tiêu chí "Chat tự nhiên theo ngữ cảnh").
4. Gọi hàm ở Task 2 để tìm Vector.
5. Gọi hàm ở Task 3 để build System Prompt.
6. Gửi request đến LLM (Gemini/OpenAI) bao gồm: System Prompt + Lịch sử hội thoại + Câu hỏi hiện tại.
7. Nhận response từ LLM, lưu vào DB với role Assistant, và trả về cho Client.

## 5. Yêu cầu về Code (Code Constraints)
- Sử dụng Repository Pattern hoặc Service Pattern đang có của hệ thống.
- Xử lý Asynchronous (`async`/`await`) chuẩn chỉ.
- Phải có Try-Catch và Logging.
- Nếu có thể, hãy đề xuất cách dùng `IAsyncEnumerable` để làm tính năng Stream response (chữ chạy ra từ từ) cho UI.

---
Vui lòng bắt đầu bằng việc cung cấp cấu trúc Entities cho Task 1 và cấu hình DbContext tương ứng.