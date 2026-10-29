namespace SrtCapcut.Core.Srt;

/// <summary>
/// Phân tích và định dạng timecode SRT dạng "HH:MM:SS,mmm".
/// Mọi phép tính dùng số nguyên (mili giây) để tránh sai số số thực.
/// </summary>
public static class SrtTimecode
{
    /// <summary>
    /// Phân tích timecode SRT thành tổng số mili giây (số nguyên).
    /// Chấp nhận cả dấu phẩy và dấu chấm ngăn cách mili giây.
    /// </summary>
    public static bool TryParse(string? s, out long milliseconds)
    {
        milliseconds = 0;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        s = s.Trim().Replace('.', ',');

        string[] parts = s.Split(':');
        if (parts.Length != 3)
            return false;

        string[] secParts = parts[2].Split(',');
        if (secParts.Length != 2)
            return false;

        if (!int.TryParse(parts[0].Trim(), out int hours) || hours < 0)
            return false;
        if (!int.TryParse(parts[1].Trim(), out int minutes) || minutes < 0 || minutes > 59)
            return false;
        if (!int.TryParse(secParts[0].Trim(), out int seconds) || seconds < 0 || seconds > 59)
            return false;

        string msText = secParts[1].Trim();
        if (msText.Length != 3 || !int.TryParse(msText, out int millis) || millis < 0 || millis > 999)
            return false;

        milliseconds = ((long)hours * 3600 + (long)minutes * 60 + seconds) * 1000 + millis;
        return true;
    }

    /// <summary>Định dạng mili giây thành "HH:MM:SS,mmm".</summary>
    public static string Format(long milliseconds)
    {
        if (milliseconds < 0)
            milliseconds = 0;
        long h = milliseconds / 3_600_000;
        milliseconds %= 3_600_000;
        long m = milliseconds / 60_000;
        milliseconds %= 60_000;
        long s = milliseconds / 1000;
        long ms = milliseconds % 1000;
        return $"{h:D2}:{m:D2}:{s:D2},{ms:D3}";
    }

    /// <summary>Định dạng mili giây thành chuỗi giây thập phân, ví dụ "4,5s".</summary>
    public static string FormatSeconds(long milliseconds)
    {
        long whole = milliseconds / 1000;
        long frac = milliseconds % 1000;
        return frac == 0 ? $"{whole}s" : $"{whole},{frac:D3} s".TrimEnd('0').Replace(" s", "s");
    }
}
