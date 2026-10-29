using System.Text;
using SrtCapcut.Core.Srt;
using Xunit;

namespace SrtCapcut.Tests;

public sealed class SrtParserTests
{
    [Fact]
    public void Parse_ViDuTrongSpec_ChinhXacDenMiliGiay()
    {
        string srt = """
            1
            00:00:00,000 --> 00:00:04,500
            昭和45年の東京。

            2
            00:00:04,500 --> 00:00:09,200
            小さな町工場で働く父。

            3
            00:00:09,200 --> 00:00:14,800
            毎朝早く家を出ていました。

            """;

        var result = SrtParser.ParseText(srt);

        Assert.Equal(3, result.Segments.Count);
        Assert.Equal(0, result.InvalidCount);
        Assert.Empty(result.Errors);

        Assert.Equal(0, result.Segments[0].StartMs);
        Assert.Equal(4500, result.Segments[0].EndMs);
        Assert.Equal(4500, result.Segments[1].StartMs);
        Assert.Equal(9200, result.Segments[1].EndMs);
        Assert.Equal(9200, result.Segments[2].StartMs);
        Assert.Equal(14800, result.Segments[2].EndMs);

        Assert.Contains("昭和45年", result.Segments[0].Text);
        Assert.Equal(14800, result.TotalDurationMs);
    }

    [Fact]
    public void Parse_BomVaCrlf_DocDuoc()
    {
        // BOM được loại bỏ ở tầng đọc file (ParseFile), đúng như thực tế sử dụng.
        string path = Path.Combine(Path.GetTempPath(), "bom_" + Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            File.WriteAllText(path,
                "1\r\n00:00:00,000 --> 00:00:01,500\r\nXin chào\r\n\r\n",
                new UTF8Encoding(true)); // encoder tự ghi BOM; StreamReader sẽ loại bỏ

            var result = SrtParser.ParseFile(path);

            Assert.Single(result.Segments);
            Assert.Equal(0, result.Segments[0].StartMs);
            Assert.Equal(1500, result.Segments[0].EndMs);
            Assert.Equal("Xin chào", result.Segments[0].Text);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void Parse_TimecodeKetThucTruocBatDau_BaoLoi()
    {
        string srt = "1\n00:00:05,000 --> 00:00:03,000\nNội dung\n\n";

        var result = SrtParser.ParseText(srt);

        Assert.Single(result.Segments);
        Assert.False(result.Segments[0].IsValid);
        Assert.Equal(1, result.InvalidCount);
    }

    [Fact]
    public void Parse_TimecodeSaiDinhDang_BaoLoi()
    {
        string srt = "1\n99:99:99,999 --> 00:00:03,000\nNội dung\n\n";

        var result = SrtParser.ParseText(srt);

        Assert.Single(result.Segments);
        Assert.False(result.Segments[0].IsValid);
    }

    [Fact]
    public void Parse_TrungThoiGian_PhatHien()
    {
        string srt = """
            1
            00:00:00,000 --> 00:00:05,000
            Đoạn một

            2
            00:00:04,000 --> 00:00:08,000
            Đoạn hai

            """;

        var result = SrtParser.ParseText(srt);

        Assert.Equal(1, result.OverlapCount);
        Assert.True(result.Segments[0].IsValid);
        Assert.False(result.Segments[1].IsValid);
        Assert.Contains(result.Segments[1].Errors, e => e.Contains("Trùng thời gian"));
    }

    [Fact]
    public void Parse_KhoangTrong_CanhBaoNhungVanHopLe()
    {
        string srt = """
            1
            00:00:00,000 --> 00:00:05,000
            Đoạn một

            2
            00:00:07,000 --> 00:00:12,000
            Đoạn hai

            """;

        var result = SrtParser.ParseText(srt);

        Assert.Equal(1, result.GapCount);
        Assert.All(result.Segments, s => Assert.True(s.IsValid));
        Assert.Contains(result.Segments[1].Warnings, w => w.Contains("Khoảng trống"));
    }

    [Fact]
    public void Parse_NoiDungNhieuDong_GiuNguyen()
    {
        string srt = "1\n00:00:00,000 --> 00:00:02,000\nDòng một\nDòng hai\nDòng ba\n\n";

        var result = SrtParser.ParseText(srt);

        Assert.Equal("Dòng một\nDòng hai\nDòng ba", result.Segments[0].Text);
    }

    [Fact]
    public void Parse_ThieuDongTimecode_BaoLoiCuPhap()
    {
        string srt = "1\nKhông phải timecode\nNội dung\n\n";

        var result = SrtParser.ParseText(srt);

        Assert.Empty(result.Segments);
        Assert.NotEmpty(result.Errors);
    }
}
