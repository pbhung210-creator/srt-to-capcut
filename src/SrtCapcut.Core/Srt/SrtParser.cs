using System.Text;

namespace SrtCapcut.Core.Srt;

/// <summary>Lỗi cú pháp khi đọc file SRT.</summary>
public sealed class SrtParseError
{
    public int LineNumber { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Kết quả phân tích file SRT.</summary>
public sealed class SrtParseResult
{
    public List<SrtSegment> Segments { get; } = new();
    public List<SrtParseError> Errors { get; } = new();
    public int OverlapCount { get; set; }
    public int GapCount { get; set; }
    public int InvalidCount { get; set; }
    public long TotalDurationMs => Segments.Count == 0 ? 0 : Segments.Max(s => s.EndMs);
}

/// <summary>
/// Bộ phân tích SRT tự xây dựng: UTF-8/BOM, CRLF/LF, nội dung nhiều dòng,
/// tiếng Nhật/Hàn/Việt; phát hiện timecode lỗi, trùng thời gian, khoảng trống.
/// Không tự sửa timecode gốc.
/// </summary>
public static class SrtParser
{
    public static SrtParseResult ParseFile(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ParseText(reader.ReadToEnd());
    }

    public static SrtParseResult ParseText(string text)
    {
        var result = new SrtParseResult();
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        int i = 0;
        int order = 0;
        SrtSegment? prev = null;

        while (i < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) { i++; continue; }
            int blockLine = i + 1;

            // Dòng 1: số thứ tự đoạn
            if (!int.TryParse(lines[i].Trim(), out int index))
            {
                result.Errors.Add(new SrtParseError
                {
                    LineNumber = blockLine,
                    Message = $"Dòng {blockLine}: số thứ tự đoạn không hợp lệ: '{lines[i].Trim()}'."
                });
                i = SkipToBlank(lines, i);
                continue;
            }
            i++;

            // Dòng 2: timecode "start --> end"
            if (i >= lines.Length || !lines[i].Contains("-->"))
            {
                result.Errors.Add(new SrtParseError
                {
                    LineNumber = blockLine,
                    Message = $"Đoạn {index}: thiếu dòng timecode 'start --> end'."
                });
                i = SkipToBlank(lines, i);
                continue;
            }

            string tcLine = lines[i].Trim();
            i++;
            string[] tcParts = tcLine.Split(new[] { "-->" }, StringSplitOptions.None);

            long startMs = 0, endMs = 0;
            bool okStart = SrtTimecode.TryParse(tcParts[0], out startMs);
            bool okEnd = tcParts.Length > 1 && SrtTimecode.TryParse(tcParts[1], out endMs);

            // Các dòng nội dung cho tới dòng trống
            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(lines[i]);
                i++;
            }

            order++;
            var seg = new SrtSegment
            {
                Order = order,
                Index = index,
                StartMs = okStart ? startMs : 0,
                EndMs = okStart && okEnd ? endMs : (okStart ? startMs : 0),
                Text = string.Join("\n", textLines).Trim()
            };

            if (!okStart || !okEnd)
            {
                seg.Errors.Add($"Timecode không hợp lệ: '{tcLine}'. Định dạng đúng: HH:MM:SS,mmm --> HH:MM:SS,mmm");
            }
            else
            {
                if (endMs <= startMs)
                {
                    seg.Errors.Add(
                        $"Thời gian kết thúc ({SrtTimecode.Format(endMs)}) phải sau thời gian bắt đầu ({SrtTimecode.Format(startMs)}).");
                }

                if (prev != null && prev.Errors.Count == 0)
                {
                    if (startMs < prev.EndMs)
                    {
                        seg.Errors.Add(
                            $"Trùng thời gian với đoạn {prev.Index}: bắt đầu {SrtTimecode.Format(startMs)} " +
                            $"nhỏ hơn thời điểm kết thúc đoạn trước {SrtTimecode.Format(prev.EndMs)}.");
                        result.OverlapCount++;
                    }
                    else if (startMs > prev.EndMs)
                    {
                        seg.Warnings.Add(
                            $"Khoảng trống {startMs - prev.EndMs} ms trước đoạn này (sẽ được giữ nguyên trên timeline).");
                        result.GapCount++;
                    }
                }

                if (index != order)
                {
                    seg.Warnings.Add($"Số thứ tự trong file ({index}) khác thứ tự đọc ({order}).");
                }
            }

            result.Segments.Add(seg);
            if (!seg.IsValid)
                result.InvalidCount++;
            prev = seg;
        }

        return result;
    }

    private static int SkipToBlank(string[] lines, int i)
    {
        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            i++;
        return i;
    }
}
