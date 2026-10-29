using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SrtCapcut.Core.Images;
using SrtCapcut.Core.Mapping;
using SrtCapcut.Core.Reports;

namespace SrtCapcut.Core.CapCut;

/// <summary>
/// Sinh dự án CapCut từ dự án mẫu bằng cơ chế template-based:
/// sao chép toàn bộ thư mục mẫu, nhân bản clip ảnh mẫu thành N clip
/// (mỗi clip trỏ tới 1 ảnh, thời gian theo SRT), cấp ID mới không trùng,
/// cập nhật duration dự án, rồi kiểm tra toàn vẹn trước khi báo thành công.
///
/// Đơn vị thời gian của CapCut Desktop là micro giây (µs).
/// </summary>
public sealed class CapCutProjectGenerator
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // giữ nguyên tiếng Việt/Nhật/Hàn trong JSON
    };

    public GenerateResult Generate(
        TemplateAnalysis analysis,
        MappingResult mapping,
        GeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(options);

        ValidateInputs(mapping, options);

        var warnings = new List<string>(analysis.Warnings);

        string outputRoot = Path.Combine(options.OutputDir, SanitizeFolderName(options.ProjectName));
        string projectDir = Path.Combine(outputRoot, "Project");
        string assetsDir = Path.Combine(outputRoot, "Assets");

        if (Directory.Exists(outputRoot))
            throw new IOException(
                $"Thư mục đầu ra đã tồn tại:\n{outputRoot}\nHãy đổi tên dự án hoặc xóa thư mục cũ rồi thử lại.");
        Directory.CreateDirectory(projectDir);
        if (options.CopyAssets)
            Directory.CreateDirectory(assetsDir);

        Report(options.Progress, 5, "Đang sao chép dự án mẫu...");
        CopyDirectory(options.TemplateDir, projectDir);

        // Chuẩn bị đường dẫn ảnh (copy vào Assets hoặc giữ nguyên).
        Report(options.Progress, 8, "Đang chuẩn bị ảnh...");
        var capcutPaths = new List<string>(mapping.Mappings.Count);
        int prepared = 0;
        foreach (var m in mapping.Mappings)
        {
            string dest = options.CopyAssets
                ? EnsureUniquePath(Path.Combine(assetsDir, m.Image!.FileName))
                : m.Image!.FullPath;
            if (options.CopyAssets)
                File.Copy(m.Image!.FullPath, dest);
            capcutPaths.Add(ToCapCutPath(Path.GetFullPath(dest), analysis.PathSeparatorStyle));

            prepared++;
            if (prepared % 25 == 0)
                Report(options.Progress, 8 + prepared * 12 / mapping.Mappings.Count,
                    $"Đang chuẩn bị ảnh {prepared}/{mapping.Mappings.Count}...");
        }

        // Đọc bản sao draft_content.json
        string draftPath = Path.Combine(projectDir, "draft_content.json");
        var root = JsonNode.Parse(File.ReadAllText(draftPath)) as JsonObject
            ?? throw new InvalidOperationException("draft_content.json trong bản sao không còn là JSON object.");

        var tracks = root["tracks"] as JsonArray
            ?? throw new InvalidOperationException("Không tìm thấy 'tracks' trong bản sao dự án.");
        var track = tracks[analysis.TrackIndex] as JsonObject
            ?? throw new InvalidOperationException("Không tìm thấy track video trong bản sao dự án.");
        var templateSegment = (track["segments"] as JsonArray)?[analysis.SegmentIndex] as JsonObject
            ?? throw new InvalidOperationException("Không tìm thấy clip ảnh mẫu trong bản sao dự án.");
        var materialsObj = root["materials"] as JsonObject
            ?? throw new InvalidOperationException("Không tìm thấy 'materials' trong bản sao dự án.");
        var collection = materialsObj[analysis.MaterialCollectionName] as JsonArray
            ?? throw new InvalidOperationException($"Không tìm thấy materials.{analysis.MaterialCollectionName} trong bản sao.");
        var templateMaterial = collection[analysis.MaterialIndex] as JsonObject
            ?? throw new InvalidOperationException("Không tìm thấy material ảnh mẫu trong bản sao dự án.");

        // Nhân bản segment + material cho từng cặp SRT/ảnh.
        Report(options.Progress, 22, "Đang tạo các clip ảnh...");
        var newSegments = new JsonArray();
        var newMaterials = new List<JsonObject>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        long maxEndUs = 0;

        for (int n = 0; n < mapping.Mappings.Count; n++)
        {
            var m = mapping.Mappings[n];

            string segId = UniqueId(analysis.TemplateSegmentId, usedIds);
            string matId = UniqueId(analysis.TemplateMaterialId, usedIds);
            string localId = string.IsNullOrEmpty(analysis.TemplateMaterialLocalId)
                ? matId
                : UniqueId(analysis.TemplateMaterialLocalId, usedIds);

            var seg = templateSegment.DeepClone() as JsonObject
                ?? throw new InvalidOperationException("Lỗi khi nhân bản clip mẫu.");
            SetString(seg, "id", segId);
            SetString(seg, analysis.SegmentMaterialIdKey, matId);
            if (seg[analysis.SegmentLocalMaterialIdKey] is JsonValue)
                SetString(seg, analysis.SegmentLocalMaterialIdKey, localId);

            var target = seg["target_timerange"] as JsonObject
                ?? throw new InvalidOperationException("Clip mẫu thiếu 'target_timerange'.");
            target["start"] = m.StartUs;
            target["duration"] = m.DurationUs;

            var source = seg["source_timerange"] as JsonObject
                ?? throw new InvalidOperationException("Clip mẫu thiếu 'source_timerange'.");
            source["start"] = 0;
            // Ảnh tĩnh: source duration = thời lượng hiển thị (theo đúng mẫu ảnh tĩnh của CapCut).
            source["duration"] = m.DurationUs;

            newSegments.Add(seg);

            var mat = templateMaterial.DeepClone() as JsonObject
                ?? throw new InvalidOperationException("Lỗi khi nhân bản material mẫu.");
            SetString(mat, "id", matId);
            if (mat["local_material_id"] is JsonValue)
                SetString(mat, "local_material_id", localId);
            SetString(mat, analysis.MaterialPathKey, capcutPaths[n]);

            if (ImageDimensionReader.TryRead(m.Image!.FullPath, out int w, out int h))
            {
                if (mat["width"] is JsonValue) mat["width"] = w;
                if (mat["height"] is JsonValue) mat["height"] = h;
            }

            newMaterials.Add(mat);
            maxEndUs = Math.Max(maxEndUs, m.StartUs + m.DurationUs);

            if ((n + 1) % 25 == 0 || n + 1 == mapping.Mappings.Count)
                Report(options.Progress, 22 + (n + 1) * 55 / mapping.Mappings.Count,
                    $"Đang tạo clip {n + 1}/{mapping.Mappings.Count}...");
        }

        // Thay thế segments của track; thay material mẫu bằng materials mới.
        track["segments"] = newSegments;
        collection.RemoveAt(analysis.MaterialIndex);
        foreach (var mat in newMaterials)
            collection.Add(mat);

        // Cập nhật duration + fps của dự án.
        root["duration"] = maxEndUs;
        if (root["fps"] is JsonValue)
            root["fps"] = options.Fps;

        ApplyAspectRatio(root, options.AspectRatio, warnings);

        // ID dự án mới để không trùng với dự án mẫu.
        string draftId = IdGenerator.NewId(TemplateAnalyzer.GetString(root, "id"));
        if (TemplateAnalyzer.GetString(root, "id") is not null)
            SetString(root, "id", draftId);

        Report(options.Progress, 80, "Đang ghi draft_content.json...");
        File.WriteAllText(draftPath, root.ToJsonString(WriteOptions), new UTF8Encoding(false));

        Report(options.Progress, 84, "Đang cập nhật draft_meta_info.json...");
        UpdateMetaInfo(projectDir, options.ProjectName, draftId, warnings);

        Report(options.Progress, 88, "Đang ghi báo cáo...");
        CsvReportWriter.Write(Path.Combine(outputRoot, "SRT_IMAGE_MAP.csv"), mapping);

        var validation = ProjectValidator.Validate(draftPath, mapping, analysis.TemplateMaterialId);
        ValidationReportWriter.Write(
            Path.Combine(outputRoot, "validation_report.json"),
            options, mapping, analysis, validation, warnings, draftId);

        File.WriteAllText(
            Path.Combine(outputRoot, "README.txt"),
            BuildReadme(options, mapping, draftId),
            new UTF8Encoding(false));

        if (!validation.AllPassed)
        {
            string details = string.Join("\n", validation.Checks
                .Where(c => !c.Passed)
                .Select(c => $"- {c.Name}: {c.Details}"));
            throw new InvalidOperationException(
                "Dự án vừa tạo KHÔNG vượt qua kiểm tra toàn vẹn nên không được đánh dấu thành công:\n" +
                details + "\nHãy gửi file validation_report.json để được hỗ trợ.");
        }

        Report(options.Progress, 100, "Hoàn tất.");
        return new GenerateResult
        {
            OutputRoot = outputRoot,
            ProjectDir = projectDir,
            AssetsDir = options.CopyAssets ? assetsDir : string.Empty,
            DraftContentPath = draftPath,
            DraftId = draftId,
            ClipCount = mapping.Mappings.Count,
            TotalDurationUs = maxEndUs,
            Warnings = warnings,
        };
    }

    private static void ValidateInputs(MappingResult mapping, GeneratorOptions options)
    {
        var missing = mapping.Mappings.Where(m => m.Image is null).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Thiếu {missing.Count} ảnh (đoạn SRT số {string.Join(", ", missing.Select(m => m.Order))}). " +
                "Hãy bổ sung ảnh cho đủ trước khi tạo dự án — phần mềm không tự tạo clip thay thế.");

        var invalid = mapping.Mappings.Where(m => !m.Segment.IsValid).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Có {invalid.Count} đoạn SRT bị lỗi timecode (đoạn số {string.Join(", ", invalid.Select(m => m.Order))}). " +
                "Hãy sửa file SRT trước khi tạo dự án.");

        if (mapping.Mappings.Count == 0)
            throw new InvalidOperationException("Không có đoạn SRT nào để tạo dự án.");

        if (!Directory.Exists(options.TemplateDir))
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục dự án mẫu:\n{options.TemplateDir}");

        if (string.IsNullOrWhiteSpace(options.OutputDir))
            throw new InvalidOperationException("Chưa chọn thư mục xuất dự án.");
        Directory.CreateDirectory(options.OutputDir);
    }

    private static string UniqueId(string? templateId, HashSet<string> used)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            string id = IdGenerator.NewId(templateId);
            if (used.Add(id))
                return id;
        }
        throw new InvalidOperationException("Không tạo được ID duy nhất cho clip/material.");
    }

    private static void SetString(JsonObject obj, string key, string value)
    {
        obj[key] = value;
    }

    private static void ApplyAspectRatio(JsonObject root, string aspect, List<string> warnings)
    {
        if (root["canvas_config"] is JsonObject canvas &&
            canvas["width"] is JsonValue &&
            canvas["height"] is JsonValue)
        {
            if (aspect == "9:16")
            {
                canvas["width"] = 1080;
                canvas["height"] = 1920;
            }
            else
            {
                canvas["width"] = 1920;
                canvas["height"] = 1080;
            }
        }
        else
        {
            warnings.Add(
                "Không tự đổi được tỉ lệ khung hình trong dự án mẫu — hãy đặt đúng tỉ lệ (16:9/9:16) " +
                "ngay khi tạo dự án mẫu trong CapCut.");
        }
    }

    private static void UpdateMetaInfo(string projectDir, string projectName, string draftId, List<string> warnings)
    {
        string metaPath = Path.Combine(projectDir, "draft_meta_info.json");
        if (!File.Exists(metaPath))
        {
            warnings.Add("Dự án mẫu không có draft_meta_info.json — bỏ qua bước cập nhật metadata.");
            return;
        }
        try
        {
            var meta = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject;
            if (meta is null)
            {
                warnings.Add("draft_meta_info.json không phải JSON object — giữ nguyên.");
                return;
            }
            if (meta["draft_name"] is JsonValue) meta["draft_name"] = projectName;
            else if (meta["name"] is JsonValue) meta["name"] = projectName;
            if (TemplateAnalyzer.GetString(meta, "draft_id") is not null)
                meta["draft_id"] = IdGenerator.NewId(TemplateAnalyzer.GetString(meta, "draft_id"));
            else if (TemplateAnalyzer.GetString(meta, "id") is not null)
                meta["id"] = draftId;
            File.WriteAllText(metaPath, meta.ToJsonString(WriteOptions), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            warnings.Add("Không cập nhật được draft_meta_info.json (" + ex.Message + ") — file được giữ nguyên.");
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest), overwrite: true);
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;
        string dir = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int i = 2; i < 1000; i++)
        {
            string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
        throw new IOException("Không tạo được tên file duy nhất trong thư mục Assets: " + path);
    }

    private static string ToCapCutPath(string fullPath, string separatorStyle) =>
        separatorStyle == "\\" ? fullPath.Replace('/', '\\') : fullPath.Replace('\\', '/');

    private static string SanitizeFolderName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "SRT_CapCut_Project" : name;
    }

    private static void Report(IProgress<GenerateProgress>? progress, int percent, string message) =>
        progress?.Report(new GenerateProgress { Percent = percent, Message = message });

    private static string BuildReadme(GeneratorOptions options, MappingResult mapping, string draftId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SRT TO CAPCUT AUTO TIMELINE - HUONG DAN MO DU AN");
        sb.AppendLine("=================================================");
        sb.AppendLine();
        sb.AppendLine($"So clip anh: {mapping.Mappings.Count}");
        sb.AppendLine($"FPS: {options.Fps} | Ti le: {options.AspectRatio}");
        sb.AppendLine($"Draft ID moi: {draftId}");
        sb.AppendLine();
        sb.AppendLine("CACH MO DU AN TRONG CAPCUT DESKTOP:");
        sb.AppendLine("1. Dong CapCut hoan toan (neu dang mo).");
        sb.AppendLine("2. Mo CapCut -> Cai dat (Settings) -> xem muc luu tru ban nhap (draft).");
        sb.AppendLine("   Ghi nho duong dan thu muc drafts cua CapCut tren may ban.");
        sb.AppendLine("3. Doi ten thu muc 'Project' trong ket qua nay thanh:");
        sb.AppendLine($"   {draftId}");
        sb.AppendLine("4. Copy thu muc vua doi ten vao thu muc drafts cua CapCut.");
        sb.AppendLine("5. Mo lai CapCut - du an se xuat hien trong danh sach drafts.");
        sb.AppendLine("6. Mo du an, kiem tra tung clip anh tren timeline, luu lai (Ctrl+S).");
        sb.AppendLine();
        sb.AppendLine("GHI CHU:");
        sb.AppendLine("- Moi anh la mot clip doc lap: ban co the keo, cat, them hieu ung tu do.");
        sb.AppendLine("- File SRT_IMAGE_MAP.csv: bang doi chieu doan SRT <-> anh.");
        sb.AppendLine("- File validation_report.json: ket qua kiem tra toan ven du an.");
        sb.AppendLine("- Neu CapCut bao thieu media: kiem tra lai duong dan trong Assets,");
        sb.AppendLine("  dam bao thu muc Assets khong bi doi ten/di chuyen le.");
        return sb.ToString();
    }
}
