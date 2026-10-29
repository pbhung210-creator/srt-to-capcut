using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SrtCapcut.Core.CapCut;
using SrtCapcut.Core.Mapping;

namespace SrtCapcut.Core.Reports;

/// <summary>Ghi validation_report.json kèm thông tin dự án, template và kết quả kiểm tra.</summary>
public static class ValidationReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Write(
        string path,
        GeneratorOptions options,
        MappingResult mapping,
        TemplateAnalysis analysis,
        ValidationResult validation,
        List<string> warnings,
        string draftId)
    {
        var report = new JsonObject
        {
            ["tool"] = "SRT to CapCut Auto Timeline",
            ["version"] = "1.0.0",
            ["generated_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["project_name"] = options.ProjectName,
            ["draft_id"] = draftId,
            ["fps"] = options.Fps,
            ["aspect_ratio"] = options.AspectRatio,
            ["copy_assets"] = options.CopyAssets,
            ["clip_count"] = mapping.Mappings.Count,
            ["total_duration_us"] = mapping.TotalDurationUs,
            ["srt_segments"] = mapping.Mappings.Count,
            ["missing_images"] = mapping.MissingImageCount,
            ["extra_images"] = mapping.ExtraImages.Count,
            ["extra_image_names"] = new JsonArray(
                mapping.ExtraImages.Select(e => (JsonNode)JsonValue.Create(e.FileName)!).ToArray()),
            ["invalid_srt_segments"] = mapping.InvalidSegmentCount,
            ["template"] = new JsonObject
            {
                ["track_index"] = analysis.TrackIndex,
                ["material_collection"] = analysis.MaterialCollectionName,
                ["segment_material_id_key"] = analysis.SegmentMaterialIdKey,
                ["material_path_key"] = analysis.MaterialPathKey,
                ["template_fps"] = analysis.TemplateFps,
                ["template_segment_duration_us"] = analysis.TemplateSegmentDurationUs,
            },
            ["warnings"] = new JsonArray(
                warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray()),
            ["checks"] = new JsonArray(
                validation.Checks.Select(c => (JsonNode)new JsonObject
                {
                    ["name"] = c.Name,
                    ["passed"] = c.Passed,
                    ["details"] = c.Details,
                }).ToArray()),
            ["all_checks_passed"] = validation.AllPassed,
            // Lưu ý trung thực: unit test/validation pass không đồng nghĩa CapCut mở thành công.
            ["capcut_9x_verified"] = false,
            ["capcut_9x_verified_note"] =
                "Chưa xác minh bằng CapCut Desktop thật. Hãy mở dự án bằng CapCut 9.x, " +
                "kiểm tra tất cả clip, lưu lại (Ctrl+S) rồi mở lại thành công mới xác nhận tương thích.",
        };

        File.WriteAllText(path, report.ToJsonString(Options), new UTF8Encoding(false));
    }
}
