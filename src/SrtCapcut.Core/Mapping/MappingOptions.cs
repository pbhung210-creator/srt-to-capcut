namespace SrtCapcut.Core.Mapping;

/// <summary>Tùy chọn ghép SRT với ảnh thành timeline.</summary>
public sealed class MappingOptions
{
    /// <summary>FPS của dự án (24/25/30/60). Dùng để làm tròn thời gian theo frame.</summary>
    public int Fps { get; init; } = 30;

    /// <summary>
    /// Luôn giữ khoảng trống giữa các đoạn đúng như SRT (mặc định true,
    /// không tự kéo dài ảnh để lấp khoảng trống).
    /// </summary>
    public bool KeepGaps { get; init; } = true;
}
