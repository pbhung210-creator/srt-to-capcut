# KHẮC PHỤC LỖI THƯỜNG GẶP

## Lỗi khi kiểm tra

| Thông báo | Nguyên nhân | Cách xử lý |
|---|---|---|
| `Thiếu N ảnh (đoạn SRT số ...)` | Số ảnh ít hơn số đoạn SRT | Bổ sung ảnh cho đủ; đặt tên có số thứ tự đúng |
| `Ảnh dư (không tạo clip)` | Số ảnh nhiều hơn số đoạn SRT | Không sao — chỉ là báo cáo. Muốn dùng thì sắp xếp lại ảnh |
| `Timecode không hợp lệ` | Sai định dạng `HH:MM:SS,mmm` | Sửa file SRT, đúng mẫu `00:00:04,500 --> 00:00:09,200` |
| `Trùng thời gian với đoạn ...` | Đoạn sau bắt đầu trước khi đoạn trước kết thúc | Sửa timecode cho các đoạn không chồng nhau |
| `Không tìm thấy 'draft_content.json'` | Chọn sai thư mục mẫu | Chọn thư mục chứa `draft_content.json` (xem `HUONG-DAN-TEMPLATE.md`) |
| `dự án mẫu phải chứa đúng 1 ảnh duy nhất` | Timeline mẫu có 0 hoặc nhiều hơn 1 ảnh | Mở mẫu trong CapCut, chỉ chừa 1 ảnh, lưu, đóng CapCut |
| `cấu trúc chưa được hỗ trợ` | Phiên bản CapCut khác cấu trúc đã biết | Gửi file `draft_content.json` của mẫu để được hỗ trợ; không tự sửa JSON |

## Lỗi khi tạo dự án

| Thông báo | Nguyên nhân | Cách xử lý |
|---|---|---|
| `Thư mục đầu ra đã tồn tại` | Chạy 2 lần cùng tên dự án | Đổi tên hoặc xóa thư mục kết quả cũ |
| `KHÔNG vượt qua kiểm tra toàn vẹn` | Dự án sinh ra có vấn đề nội bộ | Đọc `validation_report.json` xem hạng mục nào FAIL rồi báo lại |
| Tool đứng lâu ở "Đang tạo clip" | Hàng trăm ảnh, máy chậm | Chờ thêm; không tắt giữa chừng |

## Lỗi khi mở bằng CapCut

| Hiện tượng | Cách xử lý |
|---|---|
| Không thấy dự án trong CapCut | Chắc chắn đã: đổi tên thư mục `Project` thành **Draft ID**, copy vào **đúng thư mục drafts** (xem trong Cài đặt CapCut), rồi **mở lại CapCut** |
| CapCut báo thiếu media (Media Missing) | Đừng đổi tên/di chuyển thư mục `Assets`; nếu tắt tùy chọn sao chép ảnh thì đừng di chuyển ảnh gốc |
| Clip ảnh bị sai thời gian | Kiểm tra `SRT_IMAGE_MAP.csv` và `validation_report.json`; báo lại kèm 2 file này |
| CapCut báo dự án lỗi/không mở được | Gửi `validation_report.json` + `draft_content.json` của dự án mẫu để được hỗ trợ |

## Lưu ý quan trọng

- Luôn **đóng CapCut** trước khi chọn dự án mẫu và trước khi copy dự án kết quả.
- Đừng sửa tay `draft_content.json` trừ khi bạn hiểu rõ cấu trúc.
- `validation_report.json` ghi trung thực `capcut_9x_verified: false` cho đến khi
  bạn tự mở bằng CapCut 9.x, kiểm tra hết clip, lưu và mở lại thành công.
