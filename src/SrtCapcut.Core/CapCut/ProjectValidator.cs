using System.Text.Json.Nodes;
using SrtCapcut.Core.Mapping;

namespace SrtCapcut.Core.CapCut;

/// <summary>Một hạng mục kiểm tra toàn vẹn của dự án đã sinh.</summary>
public sealed class ValidationCheck
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Details { get; init; } = string.Empty;
}

/// <summary>Kết quả kiểm tra toàn vẹn.</summary>
public sealed class ValidationResult
{
    public List<ValidationCheck> Checks { get; } = new();
    public bool AllPassed => Checks.Count > 0 && Checks.All(c => c.Passed);
}

/// <summary>
/// Kiểm tra độc lập dự án vừa sinh bằng cách đọc lại draft_content.json:
/// - JSON hợp lệ, đủ số clip
/// - không trùng segment/material ID
/// - mọi material_id đều tham chiếu đúng
/// - mọi ảnh đều tồn tại trên đĩa
/// - thời gian clip khớp SRT (sau làm tròn frame)
/// - duration dự án đúng
/// - không còn material của ảnh mẫu
///
/// Lưu ý: unit test thành công KHÔNG đồng nghĩa CapCut mở thành công —
/// trạng thái "tương thích CapCut 9.x đã xác minh" chỉ được ghi nhận
/// sau khi mở, kiểm tra, lưu và mở lại bằng CapCut Desktop thật.
/// </summary>
public static class ProjectValidator
{
    public static ValidationResult Validate(
        string draftContentPath,
        MappingResult mapping,
        string templateMaterialId)
    {
        var result = new ValidationResult();
        void Add(string name, bool passed, string details) =>
            result.Checks.Add(new ValidationCheck { Name = name, Passed = passed, Details = details });

        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(draftContentPath)) as JsonObject
                ?? throw new InvalidOperationException("JSON root không phải object.");
            Add("JSON hợp lệ", true, "draft_content.json đọc và phân tích thành công.");
        }
        catch (Exception ex)
        {
            Add("JSON hợp lệ", false, ex.Message);
            return result;
        }

        try
        {
            // Thu thập toàn bộ materials theo id (mọi collection trong "materials").
            var materialPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root["materials"] is JsonObject materialsObj)
            {
                foreach (var prop in materialsObj)
                {
                    if (prop.Value is not JsonArray arr)
                        continue;
                    foreach (var n in arr)
                    {
                        if (n is not JsonObject mat)
                            continue;
                        string? id = TemplateAnalyzer.GetString(mat, "id");
                        if (string.IsNullOrEmpty(id))
                            continue;
                        materialPathById[id] = FindPathValue(mat) ?? "(không có đường dẫn)";
                    }
                }
            }

            // Thu thập segments trên mọi track.
            var segments = new List<JsonObject>();
            if (root["tracks"] is JsonArray tracks)
            {
                foreach (var t in tracks)
                {
                    if (t is not JsonObject track) continue;
                    if (track["segments"] is not JsonArray segs) continue;
                    foreach (var s in segs)
                        if (s is JsonObject so) segments.Add(so);
                }
            }

            Add("Số lượng clip",
                segments.Count == mapping.Mappings.Count,
                $"mong đợi {mapping.Mappings.Count}, thực tế {segments.Count}.");

            var segIds = new HashSet<string>(StringComparer.Ordinal);
            int dupSeg = 0, emptySeg = 0;
            foreach (var seg in segments)
            {
                string? id = TemplateAnalyzer.GetString(seg, "id");
                if (string.IsNullOrEmpty(id)) emptySeg++;
                else if (!segIds.Add(id)) dupSeg++;
            }
            Add("Không trùng segment ID", dupSeg == 0 && emptySeg == 0,
                dupSeg == 0 && emptySeg == 0
                    ? $"{segIds.Count} ID duy nhất."
                    : $"trùng: {dupSeg}, rỗng: {emptySeg}.");

            var matIds = new HashSet<string>(StringComparer.Ordinal);
            int dupMat = 0;
            foreach (string id in materialPathById.Keys)
                if (!matIds.Add(id)) dupMat++;
            Add("Không trùng material ID", dupMat == 0,
                dupMat == 0 ? $"{matIds.Count} ID duy nhất." : $"trùng: {dupMat}.");

            int badRefs = 0, missingFiles = 0;
            var missingSamples = new List<string>();
            foreach (var seg in segments)
            {
                string? refId = FindMaterialRef(seg, materialPathById);
                if (refId is null || !materialPathById.ContainsKey(refId))
                {
                    badRefs++;
                    continue;
                }
                string path = materialPathById[refId];
                if (!File.Exists(path))
                {
                    missingFiles++;
                    if (missingSamples.Count < 5)
                        missingSamples.Add(path);
                }
            }
            Add("Mọi clip tham chiếu material hợp lệ", badRefs == 0,
                badRefs == 0 ? "tất cả material_id đều tồn tại." : $"{badRefs} clip tham chiếu sai.");
            Add("Mọi ảnh đều tồn tại trên đĩa", missingFiles == 0,
                missingFiles == 0
                    ? "tất cả đường dẫn ảnh đều đọc được."
                    : $"{missingFiles} file thiếu, ví dụ: {string.Join("; ", missingSamples)}.");

            // Thời gian từng clip khớp mapping (sắp theo thời gian bắt đầu).
            var ordered = segments
                .Select(seg => new
                {
                    Start = TemplateAnalyzer.GetLong((seg["target_timerange"] as JsonObject)?["start"]),
                    Duration = TemplateAnalyzer.GetLong((seg["target_timerange"] as JsonObject)?["duration"]),
                })
                .OrderBy(x => x.Start)
                .ToList();

            bool timeOk = ordered.Count == mapping.Mappings.Count;
            string timeDetail;
            if (timeOk)
            {
                timeDetail = "tất cả clip khớp thời gian SRT sau làm tròn frame.";
                for (int i = 0; i < ordered.Count; i++)
                {
                    var mm = mapping.Mappings[i];
                    if (ordered[i].Start != mm.StartUs || ordered[i].Duration != mm.DurationUs)
                    {
                        timeOk = false;
                        timeDetail = $"clip {i + 1}: mong đợi start={mm.StartUs}, duration={mm.DurationUs}; " +
                                     $"thực tế start={ordered[i].Start}, duration={ordered[i].Duration}.";
                        break;
                    }
                }
            }
            else
            {
                timeDetail = "số clip không khớp số đoạn SRT.";
            }
            Add("Thời gian clip khớp SRT", timeOk, timeDetail);

            long maxEnd = ordered.Count == 0 ? 0 : ordered.Max(x => x.Start + x.Duration);
            long rootDuration = TemplateAnalyzer.GetLong(root["duration"]);
            Add("Duration dự án đúng", rootDuration == maxEnd,
                $"duration={rootDuration}, thời điểm kết thúc clip cuối={maxEnd}.");

            bool templateGone = string.IsNullOrEmpty(templateMaterialId) ||
                                !materialPathById.ContainsKey(templateMaterialId);
            Add("Không còn material của ảnh mẫu", templateGone,
                templateGone ? "material ảnh mẫu đã được thay thế hoàn toàn."
                             : "VẪN CÒN material của ảnh mẫu trong dự án!");
        }
        catch (Exception ex)
        {
            Add("Kiểm tra toàn vẹn", false, "Lỗi khi kiểm tra: " + ex.Message);
        }

        return result;
    }

    /// <summary>Tìm trường đường dẫn (giá trị string trông như đường dẫn file) trong material.</summary>
    private static string? FindPathValue(JsonObject mat)
    {
        foreach (var prop in mat)
        {
            if (prop.Value is JsonValue v &&
                v.TryGetValue<string>(out string? s) &&
                !string.IsNullOrEmpty(s) &&
                (s.Contains('/') || s.Contains('\\') || s.Contains(':')))
                return s;
        }
        return null;
    }

    /// <summary>Tìm id material mà segment tham chiếu tới (ưu tiên "material_id").</summary>
    private static string? FindMaterialRef(JsonObject seg, Dictionary<string, string> materialPathById)
    {
        string? direct = TemplateAnalyzer.GetString(seg, "material_id");
        if (!string.IsNullOrEmpty(direct) && materialPathById.ContainsKey(direct))
            return direct;

        foreach (var prop in seg)
        {
            if (prop.Value is JsonValue v &&
                v.TryGetValue<string>(out string? s) &&
                !string.IsNullOrEmpty(s) &&
                materialPathById.ContainsKey(s))
                return s;
        }
        return null;
    }
}
