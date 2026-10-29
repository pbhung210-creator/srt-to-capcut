namespace SrtCapcut.Core.CapCut;

/// <summary>Tiến trình tạo dự án (dùng cho thanh tiến trình trên giao diện).</summary>
public sealed class GenerateProgress
{
    public int Percent { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Tùy chọn sinh dự án CapCut.</summary>
public sealed class GeneratorOptions
{
    public string TemplateDir { get; init; } = string.Empty;
    public string OutputDir { get; init; } = string.Empty;

    /// <summary>Tên dự án (dùng đặt tên thư mục đầu ra).</summary>
    public string ProjectName { get; init; } = "SRT_CapCut_Project";

    public int Fps { get; init; } = 30;

    /// <summary>"16:9" hoặc "9:16". Chỉ áp dụng được nếu phát hiện trường canvas trong template.</summary>
    public string AspectRatio { get; init; } = "16:9";

    /// <summary>Sao chép ảnh vào thư mục Assets của dự án đầu ra (khuyến nghị).</summary>
    public bool CopyAssets { get; init; } = true;

    public IProgress<GenerateProgress>? Progress { get; init; }
}

/// <summary>Kết quả tạo dự án.</summary>
public sealed class GenerateResult
{
    public string OutputRoot { get; init; } = string.Empty;
    public string ProjectDir { get; init; } = string.Empty;
    public string AssetsDir { get; init; } = string.Empty;
    public string DraftContentPath { get; init; } = string.Empty;

    /// <summary>ID dự án mới (dùng để đặt tên thư mục khi chép vào CapCut).</summary>
    public string DraftId { get; init; } = string.Empty;

    public int ClipCount { get; init; }
    public long TotalDurationUs { get; init; }
    public List<string> Warnings { get; init; } = new();
}
