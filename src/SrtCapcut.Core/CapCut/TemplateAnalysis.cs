namespace SrtCapcut.Core.CapCut;

/// <summary>
/// Kết quả phân tích dự án CapCut mẫu: vị trí track/clip/material ảnh mẫu
/// và tên các trường JSON được phát hiện (không hard-code schema).
/// </summary>
public sealed class TemplateAnalysis
{
    public int TrackIndex { get; init; }
    public int SegmentIndex { get; init; }
    public string MaterialCollectionName { get; init; } = string.Empty;
    public int MaterialIndex { get; init; }

    /// <summary>Tên trường trên segment trỏ tới material (thường là "material_id").</summary>
    public string SegmentMaterialIdKey { get; init; } = "material_id";

    /// <summary>Tên trường local material id trên segment (thường "local_material_id").</summary>
    public string SegmentLocalMaterialIdKey { get; init; } = "local_material_id";

    /// <summary>Tên trường đường dẫn file trên material (thường là "path").</summary>
    public string MaterialPathKey { get; init; } = "path";

    public string TemplateSegmentId { get; init; } = string.Empty;
    public string TemplateMaterialId { get; init; } = string.Empty;
    public string TemplateMaterialLocalId { get; init; } = string.Empty;
    public string TemplateMaterialPath { get; init; } = string.Empty;
    public long TemplateSegmentDurationUs { get; init; }
    public double TemplateFps { get; init; }

    /// <summary>Kiểu phân tách đường dẫn trong template: "/" hoặc "\\".</summary>
    public string PathSeparatorStyle { get; init; } = "/";

    public int TrackCount { get; init; }

    public List<string> Warnings { get; } = new();
}
