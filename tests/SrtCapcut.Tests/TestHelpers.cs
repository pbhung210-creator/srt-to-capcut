using System.Text;

namespace SrtCapcut.Tests;

/// <summary>Hàm dựng dữ liệu giả cho kiểm thử: template CapCut, file SRT, thư mục ảnh.</summary>
public static class TestHelpers
{
    /// <summary>Tạo thư mục template CapCut mẫu (draft_content.json + draft_meta_info.json).</summary>
    public static (string Dir, TemplateIds Ids) CreateTemplateDir(int photoCount = 1)
    {
        string dir = Path.Combine(Path.GetTempPath(), "capcut_tpl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var ids = new TemplateIds(
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"));

        // ID cho các material/segment phụ (khi photoCount > 1), phải khớp nhau giữa hai vòng lặp.
        var extraMatIds = new string[photoCount];
        var extraLocalIds = new string[photoCount];
        var extraSegIds = new string[photoCount];
        for (int k = 0; k < photoCount; k++)
        {
            extraMatIds[k] = k == 0 ? ids.MaterialId : Guid.NewGuid().ToString("D");
            extraLocalIds[k] = k == 0 ? ids.LocalMaterialId : Guid.NewGuid().ToString("D");
            extraSegIds[k] = k == 0 ? ids.SegmentId : Guid.NewGuid().ToString("D");
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"canvas_config\": { \"width\": 1920, \"height\": 1080 },");
        sb.AppendLine("  \"duration\": 3000000,");
        sb.AppendLine("  \"fps\": 30.0,");
        sb.AppendLine($"  \"id\": \"{ids.DraftId}\",");
        sb.AppendLine("  \"materials\": {");
        sb.AppendLine("    \"audios\": [],");
        sb.AppendLine("    \"canvases\": [],");
        sb.AppendLine("    \"speeds\": [],");
        sb.AppendLine("    \"texts\": [],");
        sb.AppendLine("    \"videos\": [");
        for (int k = 0; k < photoCount; k++)
        {
            string matId = extraMatIds[k];
            string localId = extraLocalIds[k];
            sb.AppendLine("      {");
            sb.AppendLine($"        \"id\": \"{matId}\",");
            sb.AppendLine($"        \"local_material_id\": \"{localId}\",");
            sb.AppendLine("        \"type\": \"photo\",");
            sb.AppendLine($"        \"path\": \"C:/TemplateMau/anh_mau_{k}.png\",");
            sb.AppendLine("        \"width\": 1920,");
            sb.AppendLine("        \"height\": 1080,");
            sb.AppendLine("        \"duration\": 10800000000");
            sb.AppendLine(k == photoCount - 1 ? "      }" : "      },");
        }
        sb.AppendLine("    ]");
        sb.AppendLine("  },");
        sb.AppendLine("  \"name\": \"Mau\",");
        sb.AppendLine("  \"tracks\": [");
        sb.AppendLine("    {");
        sb.AppendLine($"      \"id\": \"{ids.TrackId}\",");
        sb.AppendLine("      \"type\": \"video\",");
        sb.AppendLine("      \"segments\": [");
        for (int k = 0; k < photoCount; k++)
        {
            string segId = extraSegIds[k];
            string matId = extraMatIds[k];
            string localId = extraLocalIds[k];
            sb.AppendLine("        {");
            sb.AppendLine($"          \"id\": \"{segId}\",");
            sb.AppendLine($"          \"material_id\": \"{matId}\",");
            sb.AppendLine($"          \"local_material_id\": \"{localId}\",");
            sb.AppendLine($"          \"target_timerange\": {{ \"start\": {k * 3000000}, \"duration\": 3000000 }},");
            sb.AppendLine("          \"source_timerange\": { \"start\": 0, \"duration\": 3000000 },");
            sb.AppendLine("          \"speed\": 1.0,");
            sb.AppendLine("          \"visible\": true");
            sb.AppendLine(k == photoCount - 1 ? "        }" : "        },");
        }
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"version\": 360802");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(dir, "draft_content.json"), sb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "draft_meta_info.json"),
            $"{{\"draft_id\": \"{ids.DraftId}\", \"draft_name\": \"Mau\"}}", new UTF8Encoding(false));
        return (dir, ids);
    }

    public sealed record TemplateIds(
        string DraftId, string TrackId, string SegmentId, string MaterialId, string LocalMaterialId);

    /// <summary>Tạo nội dung SRT với count đoạn, mỗi đoạn dài segMs mili giây.</summary>
    public static string BuildSrt(int count, int segMs = 4500)
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= count; i++)
        {
            long start = (long)(i - 1) * segMs;
            long end = (long)i * segMs;
            sb.AppendLine(i.ToString());
            sb.AppendLine($"{Fmt(start)} --> {Fmt(end)}");
            sb.AppendLine($"Nội dung đoạn {i} — 日本語テスト {i}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Fmt(long ms)
    {
        long h = ms / 3_600_000; ms %= 3_600_000;
        long m = ms / 60_000; ms %= 60_000;
        long s = ms / 1000; long r = ms % 1000;
        return $"{h:D2}:{m:D2}:{s:D2},{r:D3}";
    }

    /// <summary>Tạo thư mục chứa count file PNG 1x1 đặt tên theo prefix + số thứ tự.</summary>
    public static string CreateImageFolder(int count, string prefix = "PROMPT", string? dirName = null)
    {
        string dir = Path.Combine(Path.GetTempPath(), dirName ?? ("imgs_" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir);
        for (int i = 1; i <= count; i++)
            File.WriteAllBytes(
                Path.Combine(dir, $"{prefix}{i:D3}.png"), MinimalPng);
        return dir;
    }

    /// <summary>File PNG 1x1 tối thiểu (đủ để đọc kích thước từ header).</summary>
    public static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00,
        0x90, 0x77, 0x53, 0xDE,
        0x00, 0x00, 0x00, 0x00,
        0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82
    };

    public static void DeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* bỏ qua lỗi dọn dẹp */ }
    }
}
