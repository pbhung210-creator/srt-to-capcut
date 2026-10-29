using SrtCapcut.Core.Mapping;
using SrtCapcut.Core.Srt;
using Xunit;

namespace SrtCapcut.Tests;

public sealed class MappingEngineTests : IDisposable
{
    private readonly List<string> _dirs = new();

    private string NewImageDir(int count, string prefix = "PROMPT")
    {
        string dir = TestHelpers.CreateImageFolder(count, prefix);
        _dirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var d in _dirs)
            TestHelpers.DeleteDir(d);
    }

    private static MappingResult Build(int srtCount, int imgCount, int fps = 30, int segMs = 4500)
    {
        var parse = SrtParser.ParseText(TestHelpers.BuildSrt(srtCount, segMs));
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(
            TestHelpers.CreateImageFolder(imgCount));
        return MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = fps });
    }

    [Fact]
    public void Map163_163_TatCaOK()
    {
        string imgDir = NewImageDir(163);
        var parse = SrtParser.ParseText(TestHelpers.BuildSrt(163));
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        Assert.Equal(163, result.Mappings.Count);
        Assert.All(result.Mappings, m => Assert.Equal("OK", m.Status));
        Assert.Equal(0, result.MissingImageCount);
        Assert.Empty(result.ExtraImages);
        Assert.Equal(0, result.InvalidSegmentCount);
    }

    [Fact]
    public void Map163_168_BaoAnhDu_KhongTuTaoClip()
    {
        string imgDir = NewImageDir(168);
        var parse = SrtParser.ParseText(TestHelpers.BuildSrt(163));
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        Assert.Equal(163, result.Mappings.Count);
        Assert.Equal(5, result.ExtraImages.Count);
        Assert.Equal(0, result.MissingImageCount);
        Assert.Contains(result.ExtraImages, e => e.FileName == "PROMPT164.png");
        Assert.Contains(result.ExtraImages, e => e.FileName == "PROMPT168.png");
    }

    [Fact]
    public void Map168_163_BaoThieuAnh()
    {
        string imgDir = NewImageDir(163);
        var parse = SrtParser.ParseText(TestHelpers.BuildSrt(168));
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        Assert.Equal(168, result.Mappings.Count);
        Assert.Equal(5, result.MissingImageCount);
        Assert.Empty(result.ExtraImages);
        Assert.All(result.Mappings.Skip(163), m => Assert.Equal("Thiếu ảnh", m.Status));
    }

    [Fact]
    public void GiuKhoangTrongTheoSRT()
    {
        string srt = """
            1
            00:00:00,000 --> 00:00:05,000
            Một

            2
            00:00:07,000 --> 00:00:12,000
            Hai

            """;
        string imgDir = NewImageDir(2);
        var parse = SrtParser.ParseText(srt);
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        // Ảnh 1: 0 -> 5s ; Ảnh 2: 7 -> 12s ; khoảng 5-7s được giữ nguyên.
        Assert.Equal(0, result.Mappings[0].StartUs);
        Assert.Equal(5_000_000, result.Mappings[0].DurationUs);
        Assert.Equal(7_000_000, result.Mappings[1].StartUs);
        Assert.Equal(5_000_000, result.Mappings[1].DurationUs);
    }

    [Fact]
    public void LamTronKhongSaiSoTichLuy_163Doan()
    {
        string imgDir = NewImageDir(163);
        var parse = SrtParser.ParseText(TestHelpers.BuildSrt(163, segMs: 4500));
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        // Đoạn cuối (162): bắt đầu tại 162*4500 = 729000 ms -> tính độc lập, không cộng dồn.
        long expectedFrame = (729000L * 30 + 500) / 1000;
        long expectedUs = (expectedFrame * 1_000_000 + 15) / 30;
        Assert.Equal(expectedUs, result.Mappings[162].StartUs);

        // Tổng thời lượng cũng tính từ mốc tuyệt đối.
        long lastFrame = (733500L * 30 + 500) / 1000;
        long expectedTotal = (lastFrame * 1_000_000 + 15) / 30;
        Assert.Equal(expectedTotal, result.TotalDurationUs);
    }

    [Fact]
    public void DoLechLamTronDuocBaoCao()
    {
        // 1 ms ở 30fps -> làm tròn về frame 0 -> lệch -1000 µs, được báo cáo rõ.
        string srt = "1\n00:00:00,000 --> 00:00:00,001\nX\n\n";
        string imgDir = NewImageDir(1);
        var parse = SrtParser.ParseText(srt);
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        Assert.Equal(-1000, result.Mappings[0].RoundingOffsetUs);
    }

    [Fact]
    public void ChinhXacMiliGiay_4500ms_30fps()
    {
        string imgDir = NewImageDir(1);
        var parse = SrtParser.ParseText("1\n00:00:00,000 --> 00:00:04,500\nX\n\n");
        var images = SrtCapcut.Core.Images.ImageScanner.Scan(imgDir);

        var result = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });

        Assert.Equal(4_500_000, result.Mappings[0].DurationUs);
        Assert.Equal(0, result.Mappings[0].RoundingOffsetUs);
        Assert.Equal(135, result.Mappings[0].FrameCount);
    }
}
