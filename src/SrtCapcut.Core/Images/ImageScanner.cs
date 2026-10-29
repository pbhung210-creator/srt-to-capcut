using System.Text.RegularExpressions;

namespace SrtCapcut.Core.Images;

/// <summary>
/// Quét thư mục ảnh: nhận PNG/JPG/JPEG/WEBP, trích số thứ tự từ tên file
/// (1.png, 001.png, PROMPT001.png...), sắp xếp theo số tự nhiên.
/// Không tự thêm/xóa/ghép/đổi thứ tự ảnh.
/// </summary>
public static class ImageScanner
{
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);

    public static IReadOnlyList<string> GetSupportedExtensions() => SupportedExtensions;

    public static List<ImageFile> Scan(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục ảnh: {folder}");

        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new ImageFile
            {
                FullPath = Path.GetFullPath(f),
                FileName = Path.GetFileName(f),
                SequenceNumber = ExtractSequenceNumber(Path.GetFileNameWithoutExtension(f)),
                Extension = Path.GetExtension(f).ToLowerInvariant()
            })
            .OrderBy(f => f.SequenceNumber)
            .ThenBy(f => f.FileName, NaturalStringComparer.Instance)
            .ToList();
    }

    /// <summary>
    /// Lấy số thứ tự từ tên file: dùng cụm chữ số CUỐI CÙNG trong tên.
    /// "PROMPT001" -> 1, "ảnh 12" -> 12, không có số -> int.MaxValue (xếp cuối).
    /// </summary>
    public static int ExtractSequenceNumber(string nameWithoutExtension)
    {
        var matches = NumberRegex.Matches(nameWithoutExtension);
        if (matches.Count == 0)
            return int.MaxValue;
        string last = matches[^1].Value;
        return int.TryParse(last, out int n) ? n : int.MaxValue;
    }
}
