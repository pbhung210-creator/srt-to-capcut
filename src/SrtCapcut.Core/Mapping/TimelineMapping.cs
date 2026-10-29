using SrtCapcut.Core.Images;
using SrtCapcut.Core.Srt;

namespace SrtCapcut.Core.Mapping;

/// <summary>
/// Một cặp ghép: 1 đoạn SRT <-> 1 ảnh, cùng thời gian hiển thị đã làm tròn theo frame.
/// </summary>
public sealed class TimelineMapping
{
    public int Order { get; init; }
    public SrtSegment Segment { get; init; } = null!;
    public ImageFile? Image { get; init; }

    public long StartMs => Segment.StartMs;
    public long EndMs => Segment.EndMs;
    public long DurationMs => Segment.DurationMs;

    /// <summary>Thời điểm bắt đầu sau làm tròn frame, tính bằng micro giây.</summary>
    public long StartUs { get; set; }

    /// <summary>Thời lượng hiển thị sau làm tròn frame, tính bằng micro giây.</summary>
    public long DurationUs { get; set; }

    public long FrameStart { get; set; }
    public long FrameCount { get; set; }

    /// <summary>Độ lệch do làm tròn frame: DurationUs - DurationMs*1000 (micro giây).</summary>
    public long RoundingOffsetUs { get; set; }

    /// <summary>"OK" | "Thiếu ảnh" | "SRT lỗi".</summary>
    public string Status { get; set; } = "OK";

    public string? Note { get; set; }
}

/// <summary>Kết quả ghép toàn bộ SRT với ảnh.</summary>
public sealed class MappingResult
{
    public List<TimelineMapping> Mappings { get; } = new();

    /// <summary>Các ảnh dư (nhiều hơn số đoạn SRT): chỉ báo cáo, không tự tạo clip.</summary>
    public List<ImageFile> ExtraImages { get; } = new();

    public int MissingImageCount { get; set; }
    public int InvalidSegmentCount { get; set; }
    public long TotalDurationMs { get; set; }
    public long TotalDurationUs { get; set; }
    public int Fps { get; set; }
}
