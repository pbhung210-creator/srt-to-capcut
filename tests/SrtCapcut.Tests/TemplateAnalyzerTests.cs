using System.Text;
using SrtCapcut.Core.CapCut;
using Xunit;

namespace SrtCapcut.Tests;

public sealed class TemplateAnalyzerTests : IDisposable
{
    private readonly List<string> _dirs = new();

    private string NewTemplate(int photoCount = 1)
    {
        var (dir, _) = TestHelpers.CreateTemplateDir(photoCount);
        _dirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var d in _dirs)
            TestHelpers.DeleteDir(d);
    }

    [Fact]
    public void PhanTichTemplateHopLe_PhatHienCacTruong()
    {
        string dir = NewTemplate(1);

        var analysis = new TemplateAnalyzer().Analyze(dir);

        Assert.Equal(0, analysis.TrackIndex);
        Assert.Equal("videos", analysis.MaterialCollectionName);
        Assert.Equal("material_id", analysis.SegmentMaterialIdKey);
        Assert.Equal("path", analysis.MaterialPathKey);
        Assert.False(string.IsNullOrEmpty(analysis.TemplateMaterialId));
        Assert.Equal(3_000_000, analysis.TemplateSegmentDurationUs);
        Assert.Equal(30, analysis.TemplateFps);
    }

    [Fact]
    public void ThieuDraftContent_NemLoiTiengViet()
    {
        string dir = Path.Combine(Path.GetTempPath(), "empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);

        var ex = Assert.Throws<TemplateException>(() => new TemplateAnalyzer().Analyze(dir));
        Assert.Contains("draft_content.json", ex.Message);
    }

    [Fact]
    public void JsonKhongHopLe_NemLoi()
    {
        string dir = Path.Combine(Path.GetTempPath(), "badjson_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        File.WriteAllText(Path.Combine(dir, "draft_content.json"), "{ không phải json");

        Assert.Throws<TemplateException>(() => new TemplateAnalyzer().Analyze(dir));
    }

    [Fact]
    public void KhongCoClipAnh_NemLoi()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nophoto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        File.WriteAllText(Path.Combine(dir, "draft_content.json"),
            "{\"tracks\": [{\"type\": \"video\", \"segments\": []}], \"materials\": {\"videos\": []}}",
            new UTF8Encoding(false));

        var ex = Assert.Throws<TemplateException>(() => new TemplateAnalyzer().Analyze(dir));
        Assert.Contains("clip ảnh", ex.Message);
    }

    [Fact]
    public void NhieuClipAnh_NemLoi_YeuCauDung1Anh()
    {
        string dir = NewTemplate(photoCount: 2);

        var ex = Assert.Throws<TemplateException>(() => new TemplateAnalyzer().Analyze(dir));
        Assert.Contains("đúng 1 ảnh", ex.Message);
    }

    [Fact]
    public void ThieuTracks_NemLoiCauTrucChuaHoTro()
    {
        string dir = Path.Combine(Path.GetTempPath(), "notracks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        File.WriteAllText(Path.Combine(dir, "draft_content.json"),
            "{\"materials\": {}}", new UTF8Encoding(false));

        var ex = Assert.Throws<TemplateException>(() => new TemplateAnalyzer().Analyze(dir));
        Assert.Contains("chưa được hỗ trợ", ex.Message);
    }
}
