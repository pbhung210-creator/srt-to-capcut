namespace SrtCapcut.Core.Images;

/// <summary>Một file ảnh tìm thấy trong thư mục ảnh.</summary>
public sealed class ImageFile
{
    public string FullPath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public int SequenceNumber { get; init; }
    public string Extension { get; init; } = string.Empty;

    /// <summary>True nếu tên file chứa số thứ tự.</summary>
    public bool HasSequence => SequenceNumber != int.MaxValue;

    public bool Exists => File.Exists(FullPath);
}
