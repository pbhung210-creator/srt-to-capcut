// Kiểm thử khói: chạy trực tiếp các kịch bản của bộ unit test (xUnit)
// mà không qua VSTest testhost. Trả về exit code 0 nếu tất cả PASS.
using System.Text;
using System.Text.Json.Nodes;
using SrtCapcut.Core.CapCut;
using SrtCapcut.Core.Images;
using SrtCapcut.Core.Mapping;
using SrtCapcut.Core.Srt;

int passed = 0, failed = 0;
var failures = new List<string>();

void Check(bool condition, string name)
{
    if (condition) { passed++; Console.WriteLine($"  PASS: {name}"); }
    else { failed++; failures.Add(name); Console.WriteLine($"  FAIL: {name}"); }
}

void CheckThrows<T>(Action action, string name) where T : Exception
{
    try { action(); Check(false, name + " (không ném lỗi)"); }
    catch (T) { Check(true, name); }
    catch (Exception ex) { Check(false, $"{name} (ném sai loại: {ex.GetType().Name})"); }
}

var tempDirs = new List<string>();
string NewTempDir(string prefix)
{
    string d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    tempDirs.Add(d);
    return d;
}

byte[] MinimalPng() => new byte[]
{
    0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A, 0x00,0x00,0x00,0x0D, 0x49,0x48,0x44,0x52,
    0x00,0x00,0x00,0x01, 0x00,0x00,0x00,0x01, 0x08,0x02,0x00,0x00,0x00, 0x90,0x77,0x53,0xDE,
    0x00,0x00,0x00,0x00, 0x49,0x45,0x4E,0x44, 0xAE,0x42,0x60,0x82
};

string BuildSrt(int count, int segMs = 4500)
{
    var sb = new StringBuilder();
    for (int i = 1; i <= count; i++)
    {
        long s = (long)(i - 1) * segMs, e = (long)i * segMs;
        sb.AppendLine(i.ToString());
        sb.AppendLine($"{Fmt(s)} --> {Fmt(e)}");
        sb.AppendLine($"Nội dung đoạn {i} — 日本語テスト");
        sb.AppendLine();
    }
    return sb.ToString();
}
string Fmt(long ms)
{
    long h = ms / 3_600_000; ms %= 3_600_000;
    long m = ms / 60_000; ms %= 60_000;
    return $"{h:D2}:{m:D2}:{ms / 1000:D2},{ms % 1000:D3}";
}

string CreateImageFolder(int count, string prefix = "PROMPT")
{
    string dir = NewTempDir("imgs_");
    for (int i = 1; i <= count; i++)
        File.WriteAllBytes(Path.Combine(dir, $"{prefix}{i:D3}.png"), MinimalPng());
    return dir;
}

(string Dir, string DraftId, string SegId, string MatId) CreateTemplate(int photoCount = 1)
{
    // ID phụ cho template nhiều ảnh (phải khớp giữa materials và segments).
    string[] extraMatIds = new string[10];
    string[] extraLocalIds = new string[10];
    string dir = NewTempDir("tpl_");
    string draftId = Guid.NewGuid().ToString("D");
    string trackId = Guid.NewGuid().ToString("D");
    string segId = Guid.NewGuid().ToString("D");
    string matId = Guid.NewGuid().ToString("D");
    string localId = Guid.NewGuid().ToString("D");
    var sb = new StringBuilder();
    sb.AppendLine("{ \"canvas_config\": { \"width\": 1920, \"height\": 1080 },");
    sb.AppendLine("  \"duration\": 3000000, \"fps\": 30.0,");
    sb.AppendLine($"  \"id\": \"{draftId}\", \"name\": \"Mau\",");
    sb.AppendLine("  \"materials\": { \"audios\": [], \"videos\": [");
    for (int k = 0; k < photoCount; k++)
    {
        string mid = k == 0 ? matId : Guid.NewGuid().ToString("D");
        string lid = k == 0 ? localId : Guid.NewGuid().ToString("D");
        if (k > 0) { /* lưu id để segment tham chiếu */ extraMatIds[k] = mid; extraLocalIds[k] = lid; }
        sb.AppendLine("    {");
        sb.AppendLine($"      \"id\": \"{mid}\", \"local_material_id\": \"{lid}\", \"type\": \"photo\",");
        sb.AppendLine($"      \"path\": \"C:/TemplateMau/anh_{k}.png\", \"width\": 1920, \"height\": 1080,");
        sb.AppendLine("      \"duration\": 10800000000");
        sb.AppendLine(k == photoCount - 1 ? "    }" : "    },");
    }
    sb.AppendLine("  ] },");
    sb.AppendLine("  \"tracks\": [ {");
    sb.AppendLine($"    \"id\": \"{trackId}\", \"type\": \"video\", \"segments\": [");
    for (int k = 0; k < photoCount; k++)
    {
        string sid = k == 0 ? segId : Guid.NewGuid().ToString("D");
        string mid = k == 0 ? matId : extraMatIds[k];
        string lid = k == 0 ? localId : extraLocalIds[k];
        sb.AppendLine("      {");
        sb.AppendLine($"        \"id\": \"{sid}\", \"material_id\": \"{mid}\", \"local_material_id\": \"{lid}\",");
        sb.AppendLine($"        \"target_timerange\": {{ \"start\": {k * 3000000}, \"duration\": 3000000 }},");
        sb.AppendLine("        \"source_timerange\": { \"start\": 0, \"duration\": 3000000 },");
        sb.AppendLine("        \"speed\": 1.0, \"visible\": true");
        sb.AppendLine(k == photoCount - 1 ? "      }" : "      },");
    }
    sb.AppendLine("    ] } ], \"version\": 360802 }");
    File.WriteAllText(Path.Combine(dir, "draft_content.json"), sb.ToString(), new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(dir, "draft_meta_info.json"),
        $"{{\"draft_id\": \"{draftId}\", \"draft_name\": \"Mau\"}}", new UTF8Encoding(false));
    return (dir, draftId, segId, matId);
}

try
{
    Console.WriteLine("== 1. SRT Parser ==");
    {
        var r = SrtParser.ParseText(
            "1\n00:00:00,000 --> 00:00:04,500\n昭和45年の東京。\n\n" +
            "2\n00:00:04,500 --> 00:00:09,200\n小さな町工場で働く父。\n\n" +
            "3\n00:00:09,200 --> 00:00:14,800\n毎朝早く家を出ていました。\n\n");
        Check(r.Segments.Count == 3 && r.InvalidCount == 0, "Ví dụ spec: 3 đoạn, chính xác ms");
        Check(r.Segments[0].StartMs == 0 && r.Segments[0].EndMs == 4500, "Đoạn 1: 0 -> 4500ms");
        Check(r.Segments[2].StartMs == 9200 && r.Segments[2].EndMs == 14800, "Đoạn 3: 9200 -> 14800ms");
        Check(r.Segments[0].Text.Contains("昭和45年"), "Giữ nội dung tiếng Nhật");
    }
    {
        // BOM được loại bỏ ở tầng đọc file (ParseFile), đúng như thực tế sử dụng.
        string path = Path.Combine(Path.GetTempPath(), "bom_" + Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            File.WriteAllText(path,
                "1\r\n00:00:00,000 --> 00:00:01,500\r\nXin chào\r\n\r\n",
                new UTF8Encoding(true)); // encoder tự ghi BOM; StreamReader sẽ loại bỏ
            var r = SrtParser.ParseFile(path);
            Check(r.Segments.Count == 1 && r.Segments[0].EndMs == 1500, "BOM + CRLF đọc được");
        }
        finally { try { File.Delete(path); } catch { } }
    }
    {
        var r = SrtParser.ParseText("1\n00:00:05,000 --> 00:00:03,000\nX\n\n");
        Check(!r.Segments[0].IsValid && r.InvalidCount == 1, "Kết thúc trước bắt đầu -> lỗi");
    }
    {
        var r = SrtParser.ParseText("1\n99:99:99,999 --> 00:00:03,000\nX\n\n");
        Check(!r.Segments[0].IsValid, "Timecode sai định dạng -> lỗi");
    }
    {
        var r = SrtParser.ParseText(
            "1\n00:00:00,000 --> 00:00:05,000\nA\n\n2\n00:00:04,000 --> 00:00:08,000\nB\n\n");
        Check(r.OverlapCount == 1 && !r.Segments[1].IsValid, "Phát hiện trùng thời gian");
    }
    {
        var r = SrtParser.ParseText(
            "1\n00:00:00,000 --> 00:00:05,000\nA\n\n2\n00:00:07,000 --> 00:00:12,000\nB\n\n");
        Check(r.GapCount == 1 && r.Segments[1].IsValid, "Khoảng trống: cảnh báo, vẫn hợp lệ");
    }

    Console.WriteLine("== 2. Image Scanner ==");
    {
        string dir = NewTempDir("scan_");
        foreach (var n in new[] { "10.png", "2.png", "1.png" })
            File.WriteAllBytes(Path.Combine(dir, n), MinimalPng());
        var files = ImageScanner.Scan(dir);
        Check(files.Select(f => f.FileName).SequenceEqual(new[] { "1.png", "2.png", "10.png" }),
            "Sắp xếp tự nhiên (1,2,10)");
    }
    {
        Check(ImageScanner.ExtractSequenceNumber("PROMPT003") == 3, "Nhận dạng PROMPT003");
        Check(ImageScanner.ExtractSequenceNumber("002") == 2, "Nhận dạng 002");
        Check(ImageScanner.ExtractSequenceNumber("cover") == int.MaxValue, "Không có số -> int.MaxValue");
    }
    {
        string dir = NewTempDir("anh_kiem_tra_日本語_");
        File.WriteAllBytes(Path.Combine(dir, "001.png"), MinimalPng());
        File.WriteAllBytes(Path.Combine(dir, "002.jpg"), MinimalPng());
        var files = ImageScanner.Scan(dir);
        Check(files.Count == 2 && files.All(f => f.Exists), "Thư mục Unicode đọc được");
    }
    {
        string dir = NewTempDir("scan2_");
        File.WriteAllBytes(Path.Combine(dir, "001.png"), MinimalPng());
        File.WriteAllText(Path.Combine(dir, "ghi_chu.txt"), "x");
        var files = ImageScanner.Scan(dir);
        Check(files.Count == 1, "Chỉ nhận định dạng ảnh hỗ trợ");
    }
    CheckThrows<DirectoryNotFoundException>(
        () => ImageScanner.Scan(Path.Combine(Path.GetTempPath(), "khong_co_" + Guid.NewGuid())),
        "Thư mục không tồn tại -> lỗi rõ");

    Console.WriteLine("== 3. Mapping Engine ==");
    MappingResult BuildMap(int srt, int img, int fps = 30)
    {
        var parse = SrtParser.ParseText(BuildSrt(srt));
        var images = ImageScanner.Scan(CreateImageFolder(img));
        return MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = fps });
    }
    {
        var m = BuildMap(163, 163);
        Check(m.Mappings.Count == 163 && m.Mappings.All(x => x.Status == "OK"), "163/163: tất cả OK");
        Check(m.MissingImageCount == 0 && m.ExtraImages.Count == 0, "163/163: không thiếu/dư");
    }
    {
        var m = BuildMap(163, 168);
        Check(m.ExtraImages.Count == 5, "163/168: báo 5 ảnh dư");
        Check(m.Mappings.Count == 163, "163/168: không tự tạo clip cho ảnh dư");
        Check(m.ExtraImages.Any(e => e.FileName == "PROMPT168.png"), "163/168: nêu tên ảnh dư");
    }
    {
        var m = BuildMap(168, 163);
        Check(m.MissingImageCount == 5, "168/163: báo thiếu 5 ảnh");
        Check(m.Mappings.Skip(163).All(x => x.Status == "Thiếu ảnh"), "168/163: đánh dấu Thiếu ảnh");
    }
    {
        var parse = SrtParser.ParseText(
            "1\n00:00:00,000 --> 00:00:05,000\nA\n\n2\n00:00:07,000 --> 00:00:12,000\nB\n\n");
        var images = ImageScanner.Scan(CreateImageFolder(2));
        var m = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        Check(m.Mappings[0].StartUs == 0 && m.Mappings[0].DurationUs == 5_000_000, "Khoảng trống: ảnh 1 giữ 0-5s");
        Check(m.Mappings[1].StartUs == 7_000_000 && m.Mappings[1].DurationUs == 5_000_000, "Khoảng trống: ảnh 2 giữ 7-12s");
    }
    {
        var m = BuildMap(163, 163, 30);
        long expectedFrame = (729000L * 30 + 500) / 1000;
        long expectedUs = (expectedFrame * 1_000_000 + 15) / 30;
        Check(m.Mappings[162].StartUs == expectedUs, "Không sai số tích lũy (đoạn 163)");
    }
    {
        var parse = SrtParser.ParseText("1\n00:00:00,000 --> 00:00:04,500\nX\n\n");
        var images = ImageScanner.Scan(CreateImageFolder(1));
        var m = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        Check(m.Mappings[0].DurationUs == 4_500_000 && m.Mappings[0].RoundingOffsetUs == 0, "4500ms@30fps chính xác");
    }

    Console.WriteLine("== 4. Template Analyzer ==");
    {
        var (dir, _, _, _) = CreateTemplate(1);
        var a = new TemplateAnalyzer().Analyze(dir);
        Check(a.TrackIndex == 0 && a.MaterialCollectionName == "videos", "Phát hiện track/material");
        Check(a.SegmentMaterialIdKey == "material_id" && a.MaterialPathKey == "path", "Phát hiện tên trường");
        Check(a.TemplateSegmentDurationUs == 3_000_000, "Đọc thời lượng clip mẫu");
    }
    {
        string dir = NewTempDir("empty_");
        CheckThrows<TemplateException>(() => new TemplateAnalyzer().Analyze(dir), "Thiếu draft_content.json -> lỗi rõ");
    }
    {
        string dir = NewTempDir("bad_");
        File.WriteAllText(Path.Combine(dir, "draft_content.json"), "{ sai json");
        CheckThrows<TemplateException>(() => new TemplateAnalyzer().Analyze(dir), "JSON hỏng -> lỗi rõ");
    }
    {
        string dir = NewTempDir("nophoto_");
        File.WriteAllText(Path.Combine(dir, "draft_content.json"),
            "{\"tracks\": [{\"type\": \"video\", \"segments\": []}], \"materials\": {\"videos\": []}}");
        CheckThrows<TemplateException>(() => new TemplateAnalyzer().Analyze(dir), "Không có clip ảnh -> lỗi rõ");
    }
    {
        var (dir, _, _, _) = CreateTemplate(2);
        CheckThrows<TemplateException>(() => new TemplateAnalyzer().Analyze(dir), "2 clip ảnh -> yêu cầu đúng 1");
    }

    Console.WriteLine("== 5. CapCut Project Generator ==");
    {
        var (tplDir, _, segId, matId) = CreateTemplate(1);
        string imgDir = CreateImageFolder(5);
        var parse = SrtParser.ParseText(BuildSrt(5));
        var images = ImageScanner.Scan(imgDir);
        var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        var analysis = new TemplateAnalyzer().Analyze(tplDir);
        string outDir = NewTempDir("out_");

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir, OutputDir = outDir, ProjectName = "Test5", Fps = 30, CopyAssets = true,
        });

        Check(result.ClipCount == 5, "Tạo 5 clip");
        Check(File.Exists(result.DraftContentPath), "Có draft_content.json");
        Check(File.Exists(Path.Combine(result.OutputRoot, "SRT_IMAGE_MAP.csv")), "Có SRT_IMAGE_MAP.csv");
        Check(File.Exists(Path.Combine(result.OutputRoot, "validation_report.json")), "Có validation_report.json");
        Check(File.Exists(Path.Combine(result.OutputRoot, "README.txt")), "Có README.txt");
        Check(Directory.GetFiles(result.AssetsDir).Length == 5, "Assets có 5 ảnh");

        var root = JsonNode.Parse(File.ReadAllText(result.DraftContentPath)) as JsonObject;
        var segments = (root!["tracks"] as JsonArray)?[0]?["segments"] as JsonArray;
        var videos = (root["materials"] as JsonObject)?["videos"] as JsonArray;
        Check(segments!.Count == 5, "JSON có 5 segments");

        var segIds = segments.Select(s => (s as JsonObject)!["id"]!.GetValue<string>()).ToList();
        var mIds = videos!.Select(v => (v as JsonObject)!["id"]!.GetValue<string>()).ToList();
        Check(segIds.Distinct().Count() == 5, "Không trùng segment ID");
        Check(mIds.Distinct().Count() == 5, "Không trùng material ID");
        Check(!mIds.Contains(matId) && !segIds.Contains(segId), "Không còn ID của mẫu");

        bool timeOk = true;
        for (int i = 0; i < 5; i++)
        {
            var t = (segments[i] as JsonObject)!["target_timerange"] as JsonObject;
            if (t!["start"]!.GetValue<long>() != mapping.Mappings[i].StartUs ||
                t["duration"]!.GetValue<long>() != mapping.Mappings[i].DurationUs) timeOk = false;
        }
        Check(timeOk, "Thời gian clip khớp SRT");

        var pathById = videos.Select(v => v as JsonObject)
            .ToDictionary(v => v!["id"]!.GetValue<string>(), v => v!["path"]!.GetValue<string>());
        bool refsOk = segments.Cast<JsonObject>().All(s =>
        {
            string mid = s["material_id"]!.GetValue<string>();
            return pathById.ContainsKey(mid) && File.Exists(pathById[mid]);
        });
        Check(refsOk, "Mọi clip trỏ tới ảnh tồn tại");

        long maxEnd = mapping.Mappings.Max(m => m.StartUs + m.DurationUs);
        Check(root!["duration"]!.GetValue<long>() == maxEnd, "Duration dự án đúng");

        var validation = ProjectValidator.Validate(result.DraftContentPath, mapping, matId);
        Check(validation.AllPassed, "Validator độc lập: tất cả kiểm tra PASS");
    }
    {
        // Quy mô thực tế: 163 SRT + 163 ảnh qua toàn bộ pipeline.
        var (tplDir, _, matId, _) = CreateTemplate(1);
        string imgDir = CreateImageFolder(163);
        var parse = SrtParser.ParseText(BuildSrt(163));
        var images = ImageScanner.Scan(imgDir);
        var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        var analysis = new TemplateAnalyzer().Analyze(tplDir);
        string outDir = NewTempDir("out_");

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir, OutputDir = outDir, ProjectName = "Test163", Fps = 30, CopyAssets = true,
        });

        Check(result.ClipCount == 163, "163 clip: tạo đủ");
        var root = JsonNode.Parse(File.ReadAllText(result.DraftContentPath)) as JsonObject;
        var segments = (root!["tracks"] as JsonArray)?[0]?["segments"] as JsonArray;
        Check(segments!.Count == 163, "163 clip: JSON đủ 163 segments");
        var validation = ProjectValidator.Validate(result.DraftContentPath, mapping, matId);
        Check(validation.AllPassed, "163 clip: validator PASS");
        Check(Directory.GetFiles(result.AssetsDir).Length == 163, "163 clip: Assets đủ 163 ảnh");
    }
    {
        // Thiếu ảnh -> từ chối, không tạo dự án nửa vời.
        var (tplDir, _, _, _) = CreateTemplate(1);
        string imgDir = CreateImageFolder(2);
        var parse = SrtParser.ParseText(BuildSrt(3));
        var images = ImageScanner.Scan(imgDir);
        var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        var analysis = new TemplateAnalyzer().Analyze(tplDir);
        string outDir = NewTempDir("out_");
        CheckThrows<InvalidOperationException>(() =>
            new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
            { TemplateDir = tplDir, OutputDir = outDir, ProjectName = "X" }),
            "Thiếu ảnh -> từ chối tạo dự án");
    }
    {
        // SRT lỗi -> từ chối.
        var (tplDir, _, _, _) = CreateTemplate(1);
        string imgDir = CreateImageFolder(2);
        var parse = SrtParser.ParseText("1\n00:00:05,000 --> 00:00:03,000\nX\n\n2\n00:00:03,000 --> 00:00:06,000\nY\n\n");
        var images = ImageScanner.Scan(imgDir);
        var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        var analysis = new TemplateAnalyzer().Analyze(tplDir);
        string outDir = NewTempDir("out_");
        CheckThrows<InvalidOperationException>(() =>
            new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
            { TemplateDir = tplDir, OutputDir = outDir, ProjectName = "Y" }),
            "SRT lỗi -> từ chối tạo dự án");
    }

    Console.WriteLine("== 6. IdGenerator ==");
    {
        string t = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        string a = IdGenerator.NewId(t), b = IdGenerator.NewId(t);
        Check(Guid.TryParse(a, out _) && a != b && a != t, "UUID: định dạng giữ, không trùng");
    }
    {
        string t = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
        string id = IdGenerator.NewId(t);
        Check(id.Length == t.Length, "Hex: giữ độ dài");
    }
    {
        var set = new HashSet<string>();
        for (int i = 0; i < 500; i++) set.Add(IdGenerator.NewId("11111111-2222-3333-4444-555555555555"));
        Check(set.Count == 500, "500 ID duy nhất");
    }
}
catch (Exception ex)
{
    failed++;
    failures.Add("NGOẠI LỆ: " + ex.GetType().Name + ": " + ex.Message);
    Console.WriteLine("NGOẠI LỆ: " + ex);
}
finally
{
    foreach (var d in tempDirs)
        try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
}

Console.WriteLine();
Console.WriteLine($"KẾT QUẢ: {passed} PASS, {failed} FAIL");
if (failures.Count > 0)
{
    Console.WriteLine("Các mục FAIL:");
    foreach (var f in failures) Console.WriteLine(" - " + f);
}
return failed == 0 ? 0 : 1;
