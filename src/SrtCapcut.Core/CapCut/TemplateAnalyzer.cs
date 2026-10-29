using System.Text.Json.Nodes;

namespace SrtCapcut.Core.CapCut;

/// <summary>
/// Phân tích dự án CapCut mẫu (draft_content.json) để tìm:
/// track video chứa clip ảnh mẫu, material ảnh tương ứng, và tên các trường
/// JSON liên quan (phát hiện động, không hard-code schema).
///
/// Yêu cầu template: đúng 1 ảnh duy nhất đặt trên timeline.
/// </summary>
public sealed class TemplateAnalyzer
{
    private static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".tif" };

    public TemplateAnalysis Analyze(string templateDir)
    {
        string draftPath = Path.Combine(templateDir, "draft_content.json");
        if (!File.Exists(draftPath))
            throw new TemplateException(
                $"Không tìm thấy file 'draft_content.json' trong thư mục mẫu:\n{templateDir}\n" +
                "Hãy chọn đúng thư mục dự án CapCut mẫu (xem HUONG-DAN-TEMPLATE.md).");

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(File.ReadAllText(draftPath));
        }
        catch (Exception ex)
        {
            throw new TemplateException(
                $"Không đọc được 'draft_content.json' (JSON không hợp lệ): {ex.Message}");
        }

        if (rootNode is not JsonObject root)
            throw new TemplateException("'draft_content.json' không có cấu trúc dự án CapCut (cần là JSON object).");

        var tracks = root["tracks"] as JsonArray
            ?? throw new TemplateException("Không tìm thấy mảng 'tracks' trong dự án mẫu: cấu trúc chưa được hỗ trợ.");
        var materials = root["materials"] as JsonObject
            ?? throw new TemplateException("Không tìm thấy đối tượng 'materials' trong dự án mẫu: cấu trúc chưa được hỗ trợ.");

        var candidates = new List<Candidate>();

        for (int ti = 0; ti < tracks.Count; ti++)
        {
            if (tracks[ti] is not JsonObject track)
                continue;
            if (!string.Equals(GetString(track, "type"), "video", StringComparison.OrdinalIgnoreCase))
                continue;
            if (track["segments"] is not JsonArray segments)
                continue;

            for (int si = 0; si < segments.Count; si++)
            {
                if (segments[si] is not JsonObject seg)
                    continue;

                string? materialIdKey = FindMaterialIdKey(seg, materials);
                if (materialIdKey is null)
                    continue;

                string? matId = GetString(seg, materialIdKey);
                var found = FindImageMaterial(materials, matId);
                if (found is null)
                    continue;

                candidates.Add(new Candidate(ti, si, seg, materialIdKey, found));
            }
        }

        if (candidates.Count == 0)
            throw new TemplateException(
                "Không tìm thấy clip ảnh nào trên timeline của dự án mẫu.\n" +
                "Hãy tạo dự án mẫu đúng hướng dẫn: 1 ảnh duy nhất trên timeline, lưu rồi ĐÓNG CapCut.");
        if (candidates.Count > 1)
            throw new TemplateException(
                $"Tìm thấy {candidates.Count} clip ảnh trên timeline, nhưng dự án mẫu phải chứa đúng 1 ảnh duy nhất.\n" +
                "Hãy xóa bớt chỉ chừa 1 ảnh trên timeline, lưu rồi đóng CapCut.");

        var c = candidates[0];

        // Kiểm tra các trường thời gian bắt buộc trên clip mẫu.
        var target = c.Segment["target_timerange"] as JsonObject
            ?? throw new TemplateException("Clip ảnh mẫu thiếu 'target_timerange': cấu trúc chưa được hỗ trợ.");
        if (target["start"] is not JsonValue || target["duration"] is not JsonValue)
            throw new TemplateException("Clip ảnh mẫu thiếu 'target_timerange.start/duration': cấu trúc chưa được hỗ trợ.");
        var source = c.Segment["source_timerange"] as JsonObject
            ?? throw new TemplateException("Clip ảnh mẫu thiếu 'source_timerange': cấu trúc chưa được hỗ trợ.");
        if (source["start"] is not JsonValue || source["duration"] is not JsonValue)
            throw new TemplateException("Clip ảnh mẫu thiếu 'source_timerange.start/duration': cấu trúc chưa được hỗ trợ.");

        string templatePath = GetString(c.Material, c.PathKey) ?? string.Empty;

        var analysis = new TemplateAnalysis
        {
            TrackIndex = c.TrackIndex,
            SegmentIndex = c.SegmentIndex,
            MaterialCollectionName = c.MaterialInfo.CollectionName,
            MaterialIndex = c.MaterialInfo.Index,
            SegmentMaterialIdKey = c.MaterialIdKey,
            MaterialPathKey = c.PathKey,
            TemplateSegmentId = GetString(c.Segment, "id") ?? string.Empty,
            TemplateMaterialId = GetString(c.Material, "id") ?? string.Empty,
            TemplateMaterialLocalId = GetString(c.Material, "local_material_id") ?? string.Empty,
            TemplateMaterialPath = templatePath,
            TemplateSegmentDurationUs = GetLong(target["duration"]),
            TemplateFps = GetDouble(root["fps"], 30),
            PathSeparatorStyle = templatePath.Contains('\\') ? "\\" : "/",
            TrackCount = tracks.Count,
        };

        if (tracks.Count > 1)
            analysis.Warnings.Add(
                $"Dự án mẫu có {tracks.Count} track — chỉ track video chứa ảnh mẫu được nhân bản, các track khác (nhạc, chữ...) được giữ nguyên.");

        if (c.Segment["keyframe_list"] is JsonArray kf && kf.Count > 0)
            analysis.Warnings.Add(
                "Clip ảnh mẫu chứa keyframe — keyframe sẽ được sao chép nguyên sang mọi clip; hãy kiểm tra lại chuyển động trong CapCut.");

        if (analysis.TemplateSegmentDurationUs <= 0)
            analysis.Warnings.Add("Thời lượng clip ảnh mẫu không đọc được (<= 0); thời gian các clip mới vẫn được tính từ file SRT.");

        return analysis;
    }

    private sealed record Candidate(
        int TrackIndex,
        int SegmentIndex,
        JsonObject Segment,
        string MaterialIdKey,
        MaterialInfo MaterialInfo)
    {
        public JsonObject Material => MaterialInfo.Material;
        public string PathKey => MaterialInfo.PathKey;
    }

    private sealed record MaterialInfo(
        JsonObject Material,
        string CollectionName,
        int Index,
        string PathKey);

    /// <summary>
    /// Tìm tên trường trên segment trỏ tới material: ưu tiên "material_id",
    /// nếu không có thì dò mọi trường string trùng với id của một material.
    /// </summary>
    private static string? FindMaterialIdKey(JsonObject segment, JsonObject materials)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in materials)
        {
            if (prop.Value is not JsonArray arr)
                continue;
            foreach (var n in arr)
            {
                if (n is JsonObject m && GetString(m, "id") is string id && id.Length > 0)
                    ids.Add(id);
            }
        }
        if (ids.Count == 0)
            return null;

        if (GetString(segment, "material_id") is string direct && ids.Contains(direct))
            return "material_id";

        foreach (var prop in segment)
        {
            if (prop.Value is JsonValue v &&
                v.TryGetValue<string>(out string? s) &&
                s is not null && ids.Contains(s))
                return prop.Key;
        }
        return null;
    }

    /// <summary>Tìm material có id cho trước và có đường dẫn trỏ tới file ảnh.</summary>
    private static MaterialInfo? FindImageMaterial(JsonObject materials, string? matId)
    {
        if (string.IsNullOrEmpty(matId))
            return null;

        foreach (var prop in materials)
        {
            if (prop.Value is not JsonArray arr)
                continue;
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject mat)
                    continue;
                if (!string.Equals(GetString(mat, "id"), matId, StringComparison.Ordinal))
                    continue;

                string? pathKey = mat
                    .Where(p => p.Value is JsonValue v &&
                                v.TryGetValue<string>(out string? s) &&
                                !string.IsNullOrEmpty(s) &&
                                ImageExtensions.Any(e => s.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => p.Key)
                    .FirstOrDefault();

                if (pathKey is null)
                    return null; // material tồn tại nhưng không trỏ tới file ảnh
                return new MaterialInfo(mat, prop.Key, i, pathKey);
            }
        }
        return null;
    }

    internal static string? GetString(JsonObject obj, string key)
    {
        try
        {
            return obj[key] is JsonValue v && v.TryGetValue<string>(out string? s) ? s : null;
        }
        catch
        {
            return null;
        }
    }

    internal static long GetLong(JsonNode? node)
    {
        try
        {
            if (node is JsonValue v)
            {
                if (v.TryGetValue<long>(out long l)) return l;
                if (v.TryGetValue<double>(out double d)) return (long)d;
            }
        }
        catch { }
        return 0;
    }

    internal static double GetDouble(JsonNode? node, double fallback)
    {
        try
        {
            if (node is JsonValue v)
            {
                if (v.TryGetValue<double>(out double d)) return d;
                if (v.TryGetValue<long>(out long l)) return l;
            }
        }
        catch { }
        return fallback;
    }
}
