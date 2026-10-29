# HƯỚNG DẪN TẠO DỰ ÁN CAPCUT MẪU

Tool **không đoán** cấu trúc CapCut mà đọc từ dự án mẫu **do chính bạn tạo**
trên máy bạn. Dự án mẫu càng "sạch" thì kết quả càng chuẩn.

## Các bước

### 1. Tạo dự án mới trong CapCut Desktop

- Mở **CapCut Desktop 9.x** → tạo **dự án mới**.
- Đặt **tỉ lệ khung hình** đúng ý muốn: **16:9** hoặc **9:16**
  (tool chỉ tự đổi được nếu phát hiện trường canvas; tốt nhất đặt ngay từ đầu).
- Đặt **FPS** đúng ý muốn: 24 / 25 / 30 / 60.

### 2. Đặt đúng 1 ảnh lên timeline

- Import **1 ảnh bất kỳ** (PNG/JPG đều được).
- Kéo ảnh xuống **track video** trên timeline.
- Đặt thời lượng hiển thị của ảnh, ví dụ **3 giây** (kéo dãn clip).

### 3. Giữ dự án thật "sạch"

- **Không** thêm nhạc, lời thoại, chữ (text), sticker.
- **Không** thêm hiệu ứng (effect), chuyển cảnh (transition), animation/keyframe.
- Chỉ để **đúng 1 ảnh duy nhất** trên timeline.

### 4. Lưu và ĐÓNG CapCut

- Nhấn **Ctrl+S** để lưu.
- **Đóng hoàn toàn CapCut** (thoát hẳn, không để chạy nền) để tránh xung đột
  khi tool đọc file dự án.

### 5. Tìm thư mục dự án mẫu

- Mở lại CapCut → **Cài đặt (Settings)** → tìm mục **vị trí lưu bản nháp/draft**
  (mỗi máy có thể khác nhau, hãy xem đúng trên máy bạn).
- Mở thư mục đó, tìm **thư mục dự án vừa tạo** — là thư mục chứa file
  **`draft_content.json`**.
- **Copy** (không cắt) thư mục dự án này ra một nơi khác, ví dụ
  `D:\CapCut_Template_Mau\`. Thư mục copy này chính là **"dự án CapCut mẫu"**
  để chọn trong tool.

## Kiểm tra nhanh

Mở thư mục mẫu, bạn phải thấy ít nhất:

```text
TenDuAnMau/
├── draft_content.json
└── draft_meta_info.json
```

## Lưu ý

- Mỗi lần đổi ý về tỉ lệ khung hình / FPS → tạo lại dự án mẫu mới.
- Không dùng dự án mẫu đang mở trong CapCut (file có thể bị khóa).
- Nếu tool báo "dự án mẫu phải chứa đúng 1 ảnh duy nhất": mở lại dự án mẫu
  trong CapCut, xóa bớt chỉ chừa 1 ảnh, lưu, đóng CapCut, rồi chọn lại.
- Nếu tool báo "cấu trúc chưa được hỗ trợ": gửi file `draft_content.json`
  của dự án mẫu để được hỗ trợ (không gửi ảnh hay thông tin riêng tư khác).
