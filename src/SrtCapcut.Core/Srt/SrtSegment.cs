namespace SrtCapcut.Core.Srt;

/// <summary>Một đoạn phụ đề SRT: số thứ tự, thời gian bắt đầu/kết thúc (ms, số nguyên), nội dung.</summary>
public sealed class SrtSegment
{
    /// <summary>Thứ tự đoạn trong file, tính từ 1.</summary>
    public int Order { get; init; }

    /// <summary>Số thứ tự ghi trong file SRT (có thể không liên tục).</summary>
    public int Index { get; init; }

    /// <summary>Thời điểm bắt đầu, tính bằng mili giây.</summary>
    public long StartMs { get; init; }

    /// <summary>Thời điểm kết thúc, tính bằng mili giây.</summary>
    public long EndMs { get; init; }

    /// <summary>Thời lượng = EndMs - StartMs (mili giây).</summary>
    public long DurationMs => EndMs - StartMs;

    /// <summary>Nội dung phụ đề (nhiều dòng được nối bằng \n).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Các lỗi khiến đoạn không dùng được (timecode sai, trùng thời gian...).</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Các cảnh báo không chặn (khoảng trống, số thứ tự lệch...).</summary>
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
