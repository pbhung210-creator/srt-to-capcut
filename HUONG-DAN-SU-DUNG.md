# HƯỚNG DẪN SỬ DỤNG

## Chuẩn bị

1. File phụ đề **`.srt`** (UTF-8).
2. Thư mục ảnh **PNG/JPG/WEBP**, đặt tên có số thứ tự:
   `1.png, 2.png, ...` hoặc `001.png...` hoặc `PROMPT001.png...`
3. **Dự án CapCut mẫu** đã tạo theo `HUONG-DAN-TEMPLATE.md`
   (nhớ **đóng CapCut** trước khi dùng tool).
4. File `SRT_to_CapCut.exe` (lấy từ GitHub Actions, xem `README.md`).

## Các bước

### Bước 1 — Chọn dữ liệu đầu vào

Mở EXE, chọn 4 mục:

| Mục | Chọn gì |
|---|---|
| File SRT | File `.srt` của bạn |
| Thư mục ảnh | Thư mục chứa các ảnh |
| Dự án CapCut mẫu | Thư mục copy của dự án mẫu (chứa `draft_content.json`) |
| Thư mục xuất | Nơi lưu kết quả (ví dụ `D:\KetQua\`) |

### Bước 2 — Chọn tùy chọn

- **Tỉ lệ khung hình**: 16:9 hoặc 9:16 (nên trùng với dự án mẫu).
- **FPS**: 24 / 25 / 30 / 60 (nên trùng với dự án mẫu).
- **Sao chép ảnh vào thư mục Assets**: nên để bật (khuyến nghị).

### Bước 3 — Nhấn KIỂM TRA SRT VÀ ẢNH

Đọc kỹ:

- **Thống kê**: tổng đoạn SRT, tổng ảnh, ảnh thiếu, ảnh dư, tổng thời lượng,
  đoạn SRT lỗi.
- **Bảng xem trước**: từng đoạn SRT ↔ ảnh, thời gian, trạng thái.
- **Nhật ký**: chi tiết lỗi, cảnh báo, tên các ảnh dư.

Xử lý trước khi tiếp tục:

- **Ảnh thiếu** → bổ sung ảnh cho đủ (tool không tự tạo clip thay thế).
- **Ảnh dư** → không sao, tool chỉ báo cáo, không tạo clip cho ảnh dư.
  Nếu muốn dùng, hãy đổi tên/sắp xếp lại ảnh rồi kiểm tra lại.
- **Đoạn SRT lỗi** (timecode sai, trùng thời gian) → sửa file SRT rồi kiểm tra lại.

Chỉ khi nhật ký báo **"DỮ LIỆU HỢP LỆ"** thì nút **TẠO DỰ ÁN CAPCUT** mới sáng.

### Bước 4 — Nhấn TẠO DỰ ÁN CAPCUT

- Chờ thanh tiến trình chạy xong (vài giây đến vài phút tùy số lượng ảnh).
- Nếu thành công: hộp thoại báo số clip và thư mục kết quả.
- Nhấn **MỞ THƯ MỤC KẾT QUẢ** để xem.

### Bước 5 — Đưa dự án vào CapCut

Đọc file **`README.txt`** trong thư mục kết quả và làm theo:

1. Đóng CapCut hoàn toàn.
2. Vào CapCut → Cài đặt → xem **thư mục drafts** trên máy bạn.
3. Đổi tên thư mục `Project` thành **Draft ID** ghi trong `README.txt`
   (cũng có trong `validation_report.json`).
4. Copy thư mục vừa đổi tên vào thư mục drafts của CapCut.
5. Mở lại CapCut → dự án xuất hiện trong danh sách → mở ra kiểm tra.
6. Kiểm tra từng clip ảnh trên timeline, rồi **lưu lại (Ctrl+S)**,
   đóng và **mở lại** để chắc chắn.

### Bước 6 — Chỉnh sửa tự do

Mỗi ảnh là **một clip độc lập** trên timeline: bạn có thể kéo, cắt,
thêm hiệu ứng, chuyển cảnh, chữ, nhạc... tùy ý.

## Các file trong thư mục kết quả

- `Project/` — dự án CapCut (đổi tên thành Draft ID rồi copy vào drafts).
- `Assets/` — bản sao các ảnh (nếu bật tùy chọn sao chép).
- `SRT_IMAGE_MAP.csv` — bảng đối chiếu đoạn SRT ↔ ảnh (mở bằng Excel).
- `validation_report.json` — kết quả kiểm tra toàn vẹn + Draft ID.
- `README.txt` — hướng dẫn mở bằng CapCut.
