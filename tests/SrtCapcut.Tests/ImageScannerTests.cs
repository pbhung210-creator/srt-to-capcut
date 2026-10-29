using SrtCapcut.Core.Images;
using Xunit;

namespace SrtCapcut.Tests;

public sealed class ImageScannerTests : IDisposable
{
    private readonly List<string> _dirs = new();

    private string NewDir(string? name = null)
    {
        string dir = Path.Combine(Path.GetTempPath(), name ?? "scan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    private static void Touch(string dir, string fileName) =>
        File.WriteAllBytes(Path.Combine(dir, fileName), TestHelpers.MinimalPng);

    public void Dispose()
    {
        foreach (var d in _dirs)
            TestHelpers.DeleteDir(d);
    }

    [Fact]
    public void SapXepTuNhien_KhongXepKieuChuoi()
    {
        string dir = NewDir();
        Touch(dir, "10.png");
        Touch(dir, "2.png");
        Touch(dir, "1.png");

        var files = ImageScanner.Scan(dir);

        Assert.Equal(new[] { "1.png", "2.png", "10.png" },
            files.Select(f => f.FileName).ToArray());
    }

    [Fact]
    public void NhanDangCacMauTenFile()
    {
        Assert.Equal(1, ImageScanner.ExtractSequenceNumber("1"));
        Assert.Equal(2, ImageScanner.ExtractSequenceNumber("002"));
        Assert.Equal(3, ImageScanner.ExtractSequenceNumber("PROMPT003"));
        Assert.Equal(12, ImageScanner.ExtractSequenceNumber("ảnh 12"));
        Assert.Equal(int.MaxValue, ImageScanner.ExtractSequenceNumber("cover"));
    }

    [Fact]
    public void MauTenPrompt_SapXepDung()
    {
        string dir = NewDir();
        Touch(dir, "PROMPT010.png");
        Touch(dir, "PROMPT002.png");
        Touch(dir, "PROMPT001.png");

        var files = ImageScanner.Scan(dir);

        Assert.Equal(new[] { "PROMPT001.png", "PROMPT002.png", "PROMPT010.png" },
            files.Select(f => f.FileName).ToArray());
        Assert.Equal(new[] { 1, 2, 10 },
            files.Select(f => f.SequenceNumber).ToArray());
    }

    [Fact]
    public void ThuMucUnicode_DocDuoc()
    {
        string dir = NewDir("anh_kiem_tra_日本語_ảnh");
        Touch(dir, "001.png");
        Touch(dir, "002.jpg");

        var files = ImageScanner.Scan(dir);

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.True(f.Exists));
    }

    [Fact]
    public void FileKhongCoSo_XepCuoiCung()
    {
        string dir = NewDir();
        Touch(dir, "cover.png");
        Touch(dir, "001.png");

        var files = ImageScanner.Scan(dir);

        Assert.Equal("001.png", files[0].FileName);
        Assert.Equal("cover.png", files[1].FileName);
    }

    [Fact]
    public void ChiNhanDinhDangAnhHoTro()
    {
        string dir = NewDir();
        Touch(dir, "001.png");
        Touch(dir, "002.JPG");
        Touch(dir, "003.webp");
        File.WriteAllText(Path.Combine(dir, "ghi_chu.txt"), "x");
        File.WriteAllBytes(Path.Combine(dir, "video.mp4"), new byte[] { 0 });

        var files = ImageScanner.Scan(dir);

        Assert.Equal(3, files.Count);
    }

    [Fact]
    public void ThuMucKhongTonTai_NemLoi()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            ImageScanner.Scan(Path.Combine(Path.GetTempPath(), "khong_ton_tai_" + Guid.NewGuid())));
    }
}
