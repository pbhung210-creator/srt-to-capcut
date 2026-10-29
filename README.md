# SRT TO CAPCUT AUTO TIMELINE

Phần mềm Windows giúp bạn biến **1 file phụ đề `.srt`** + **1 thư mục ảnh**
thành **dự án CapCut Desktop có timeline gồm các clip ảnh độc lập** —
mỗi ảnh hiển thị đúng mốc thời gian của đoạn SRT tương ứng, mở bằng CapCut
để tiếp tục chỉnh sửa tự do.

> **Không** xuất video MP4 ghép sẵn. Kết quả là dự án CapCut thật
> (`draft_content.json` + ảnh), chỉnh sửa được từng clip.

## Tính năng

- Đọc SRT chính xác đến mili giây (số nguyên, không sai số số thực);
  hỗ trợ UTF-8/BOM, CRLF/LF, nội dung nhiều dòng, tiếng Việt/Nhật/Hàn.
- Phát hiện timecode lỗi, đoạn trùng thời gian, khoảng trống (giữ nguyên, không tự lấp).
- Quét ảnh PNG/JPG/WEBP, sắp xếp theo số thứ tự tự nhiên
  (`1.png, 2.png, 10.png` — không xếp kiểu `1, 10, 2`).
- Quy tắc **1 đoạn SRT = 1 ảnh**; ảnh thiếu/dư được báo cáo rõ tên file,
  không tự thêm/xóa/ghép/đổi thứ tự.
- **Template-based CapCut generator**: bạn tạo 1 dự án mẫu có 1 ảnh trên
  timeline, tool tự phân tích cấu trúc JSON thật của máy bạn rồi nhân bản
  thành hàng trăm clip — không hard-code schema CapCut.
- Mỗi clip được cấp ID mới không trùng; cập nhật duration dự án, FPS,
  đường dẫn ảnh (hỗ trợ Unicode, copy vào thư mục `Assets`).
- Làm tròn thời gian theo frame (24/25/30/60 fps) bằng số nguyên,
  không sai số tích lũy; báo cáo độ lệch làm tròn từng clip.
- Kiểm tra toàn vẹn tự động trước khi báo thành công
  (ID trùng, tham chiếu material, file ảnh tồn tại, thời gian khớp SRT...).
- Xuất `SRT_IMAGE_MAP.csv`, `validation_report.json`, `README.txt`.

## Lấy file EXE (không cần cài gì thêm)

1. Đẩy source này lên GitHub (nhánh `main`).
2. Vào tab **Actions** → chọn workflow **Build Windows EXE** → chờ chạy xong.
3. Tải file trong mục **Artifacts**: `SRT_to_CapCut-win-x64` → được `SRT_to_CapCut.exe`.
4. Chép EXE về máy Windows 10/11 và chạy trực tiếp (portable, self-contained).

> Có thể chạy thủ công: tab Actions → Build Windows EXE → **Run workflow**.

## Cách dùng nhanh

1. **Tạo dự án mẫu** theo `HUONG-DAN-TEMPLATE.md`, rồi **đóng CapCut**.
2. Mở `SRT_to_CapCut.exe`.
3. Chọn: file SRT → thư mục ảnh → thư mục dự án mẫu → thư mục xuất.
4. Chọn tỉ lệ khung hình (16:9/9:16) và FPS (24/25/30/60).
5. Nhấn **KIỂM TRA SRT VÀ ẢNH** → đọc kỹ thống kê và nhật ký.
6. Nếu hợp lệ, nhấn **TẠO DỰ ÁN CAPCUT**.
7. Mở thư mục kết quả, đọc `README.txt` để biết cách đưa dự án vào CapCut.

Chi tiết xem `HUONG-DAN-SU-DUNG.md`. Lỗi thường gặp xem `KHAC-PHUC-LOI.md`.

## Cấu trúc thư mục đầu ra

```text
SRT_CapCut_20250101_120000/
├── Project/                 # bản sao dự án mẫu đã được chỉnh sửa
│   ├── draft_content.json   # timeline N clip ảnh độc lập
│   └── draft_meta_info.json
├── Assets/                  # bản sao các ảnh (nếu bật tùy chọn)
├── SRT_IMAGE_MAP.csv        # bảng đối chiếu đoạn SRT <-> ảnh
├── validation_report.json   # kết quả kiểm tra toàn vẹn
└── README.txt               # hướng dẫn mở bằng CapCut
```

## Cấu trúc source

```text
src/SrtCapcut.Core/    # thư viện lõi (SRT, ảnh, mapping, CapCut, báo cáo)
src/SrtCapcut.App/     # giao diện WPF tiếng Việt (build trên Windows)
tests/SrtCapcut.Tests/ # unit tests (xUnit)
.github/workflows/     # build EXE tự động trên Windows
```

## Build thủ công trên Windows

Cần cài .NET 8 SDK, rồi chạy:

```powershell
dotnet test SrtCapcut.sln -c Release
dotnet publish src/SrtCapcut.App/SrtCapcut.App.csproj -c Release -r win-x64 `
  --self-contained true /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Trạng thái xác minh CapCut

- [x] Unit tests cho parser, scanner, mapping, analyzer, generator, validator.
- [ ] **Mở dự án thật bằng CapCut Desktop 9.x, kiểm tra tất cả clip, lưu và mở lại.**
  Chỉ sau bước này mới được ghi "Tương thích CapCut 9.x đã xác minh"
  (`validation_report.json` hiện ghi `capcut_9x_verified: false` một cách trung thực).
