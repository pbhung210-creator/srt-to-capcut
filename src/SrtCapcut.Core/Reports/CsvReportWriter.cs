using System.Text;
using SrtCapcut.Core.Mapping;
using SrtCapcut.Core.Srt;

namespace SrtCapcut.Core.Reports;

/// <summary>Ghi báo cáo đối chiếu SRT <-> ảnh ra file CSV (UTF-8 BOM, phân cách ';').</summary>
public static class CsvReportWriter
{
    public static void Write(string path, MappingResult mapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine("STT;Ten anh;Duong dan anh;Bat dau (SRT);Ket thuc (SRT);Thoi luong;Bat dau (frame);So frame;Thoi luong (micro giay);Do lech lam tron (micro giay);Noi dung phu de;Trang thai;Ghi chu");

        foreach (var m in mapping.Mappings)
        {
            sb.Append(m.Order).Append(';');
            sb.Append(Esc(m.Image?.FileName ?? "(thieu anh)")).Append(';');
            sb.Append(Esc(m.Image?.FullPath ?? "")).Append(';');
            sb.Append(SrtTimecode.Format(m.StartMs)).Append(';');
            sb.Append(SrtTimecode.Format(m.EndMs)).Append(';');
            sb.Append(SrtTimecode.FormatSeconds(m.DurationMs)).Append(';');
            sb.Append(m.FrameStart).Append(';');
            sb.Append(m.FrameCount).Append(';');
            sb.Append(m.DurationUs).Append(';');
            sb.Append(m.RoundingOffsetUs).Append(';');
            sb.Append(Esc(OneLine(m.Segment.Text))).Append(';');
            sb.Append(Esc(m.Status)).Append(';');
            sb.Append(Esc(m.Note ?? ""));
            sb.AppendLine();
        }

        foreach (var extra in mapping.ExtraImages)
        {
            sb.Append("").Append(';');
            sb.Append(Esc(extra.FileName)).Append(';');
            sb.Append(Esc(extra.FullPath)).Append(';');
            sb.Append("").Append(';').Append("").Append(';').Append("").Append(';');
            sb.Append("").Append(';').Append("").Append(';').Append("").Append(';').Append("").Append(';');
            sb.Append("").Append(';');
            sb.Append(Esc("Ảnh dư")).Append(';');
            sb.Append(Esc("Ảnh dư (nhiều hơn số đoạn SRT) — không tạo clip, chỉ báo cáo."));
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string OneLine(string s) =>
        s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();

    private static string Esc(string s)
    {
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
