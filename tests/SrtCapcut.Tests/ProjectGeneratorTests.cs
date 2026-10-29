using System.Text.Json.Nodes;
using SrtCapcut.Core.CapCut;
using SrtCapcut.Core.Images;
using SrtCapcut.Core.Mapping;
using SrtCapcut.Core.Srt;
using Xunit;

namespace SrtCapcut.Tests;

public sealed class ProjectGeneratorTests : IDisposable
{
    private readonly List<string> _dirs = new();

    private string Track(string dir) { _dirs.Add(dir); return dir; }

    public void Dispose()
    {
        foreach (var d in _dirs)
            TestHelpers.DeleteDir(d);
    }

    private static (TemplateAnalysis Analysis, MappingResult Mapping, string TplDir, string ImgDir)
        Prepare(int count, int fps = 30)
    {
        var (tplDir, _) = TestHelpers.CreateTemplateDir(1);
        string imgDir = TestHelpers.CreateImageFolder(count);
        var parse = SrtParser.ParseText(TestHelpers.BuildSrt(count));
        var images = ImageScanner.Scan(imgDir);
        var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = fps });
        var analysis = new TemplateAnalyzer().Analyze(tplDir);
        return (analysis, mapping, tplDir, imgDir);
    }

    private string NewOutputDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "out_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    [Fact]
    public void TaoDuAn_5Clip_HopLe_DuFileDauRa()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(5);
        Track(tplDir); Track(imgDir);
        string outDir = NewOutputDir();

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir,
            OutputDir = outDir,
            ProjectName = "Test5",
            Fps = 30,
            CopyAssets = true,
        });

        Assert.Equal(5, result.ClipCount);
        Assert.True(File.Exists(result.DraftContentPath));
        Assert.True(File.Exists(Path.Combine(result.OutputRoot, "SRT_IMAGE_MAP.csv")));
        Assert.True(File.Exists(Path.Combine(result.OutputRoot, "validation_report.json")));
        Assert.True(File.Exists(Path.Combine(result.OutputRoot, "README.txt")));
        Assert.Equal(5, Directory.GetFiles(result.AssetsDir).Length);

        // Đọc lại JSON đầu ra: 5 segments, thời gian khớp mapping.
        var root = JsonNode.Parse(File.ReadAllText(result.DraftContentPath)) as JsonObject;
        Assert.NotNull(root);
        var segments = (root!["tracks"] as JsonArray)?[0]?["segments"] as JsonArray;
        Assert.NotNull(segments);
        Assert.Equal(5, segments!.Count);

        for (int i = 0; i < 5; i++)
        {
            var seg = segments[i] as JsonObject;
            Assert.NotNull(seg);
            var target = seg!["target_timerange"] as JsonObject;
            Assert.NotNull(target);
            Assert.Equal(mapping.Mappings[i].StartUs, target!["start"]!.GetValue<long>());
            Assert.Equal(mapping.Mappings[i].DurationUs, target["duration"]!.GetValue<long>());
        }

        // Duration dự án = thời điểm kết thúc clip cuối.
        long maxEnd = mapping.Mappings.Max(m => m.StartUs + m.DurationUs);
        Assert.Equal(maxEnd, root!["duration"]!.GetValue<long>());
    }

    [Fact]
    public void TaoDuAn_KhongTrungId()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(10);
        Track(tplDir); Track(imgDir);
        string outDir = NewOutputDir();

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir,
            OutputDir = outDir,
            ProjectName = "TestIds",
            CopyAssets = false,
        });

        var root = JsonNode.Parse(File.ReadAllText(result.DraftContentPath)) as JsonObject;
        var segments = (root!["tracks"] as JsonArray)?[0]?["segments"] as JsonArray;
        var videos = (root["materials"] as JsonObject)?["videos"] as JsonArray;

        var segIds = segments!.Select(s => (s as JsonObject)?["id"]?.GetValue<string>()).ToList();
        var matIds = videos!.Select(v => (v as JsonObject)?["id"]?.GetValue<string>()).ToList();

        Assert.Equal(10, segIds.Distinct().Count());
        Assert.Equal(10, matIds.Distinct().Count());
        // Không còn ID của material mẫu.
        Assert.DoesNotContain(analysis.TemplateMaterialId, matIds);
        Assert.All(segIds, id => Assert.NotEqual(analysis.TemplateSegmentId, id));
    }

    [Fact]
    public void TaoDuAn_MoiClipTroToAnhTonTai()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(5);
        Track(tplDir); Track(imgDir);
        string outDir = NewOutputDir();

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir,
            OutputDir = outDir,
            ProjectName = "TestPaths",
            CopyAssets = true,
        });

        var root = JsonNode.Parse(File.ReadAllText(result.DraftContentPath)) as JsonObject;
        var segments = (root!["tracks"] as JsonArray)?[0]?["segments"] as JsonArray;
        var videos = (root["materials"] as JsonObject)?["videos"] as JsonArray;
        var pathById = videos!
            .Select(v => v as JsonObject)
            .ToDictionary(v => v!["id"]!.GetValue<string>(), v => v!["path"]!.GetValue<string>());

        foreach (var s in segments!)
        {
            var seg = s as JsonObject;
            string matId = seg!["material_id"]!.GetValue<string>();
            Assert.True(pathById.ContainsKey(matId), "material_id không tham chiếu đúng");
            Assert.True(File.Exists(pathById[matId]), "ảnh không tồn tại: " + pathById[matId]);
        }
    }

    [Fact]
    public void TaoDuAn_163Clip_QuyMoThucTe()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(163);
        Track(tplDir); Track(imgDir);
        string outDir = NewOutputDir();

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir,
            OutputDir = outDir,
            ProjectName = "Test163",
            Fps = 30,
            CopyAssets = true,
        });

        Assert.Equal(163, result.ClipCount);
        var validation = ProjectValidator.Validate(
            result.DraftContentPath, mapping, analysis.TemplateMaterialId);
        Assert.True(validation.AllPassed,
            string.Join("\n", validation.Checks.Where(c => !c.Passed).Select(c => c.Name + ": " + c.Details)));
        Assert.Equal(163, Directory.GetFiles(result.AssetsDir).Length);
    }

    [Fact]
    public void ThieuAnh_NemLoiKhongTaoDuAn()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(5);
        Track(tplDir); Track(imgDir);
        // Giả lập thiếu ảnh: bỏ ảnh của đoạn cuối.
        mapping.Mappings[4] = new TimelineMapping
        {
            Order = 5,
            Segment = mapping.Mappings[4].Segment,
            Image = null,
            Status = "Thiếu ảnh",
        };
        mapping.MissingImageCount = 1;
        string outDir = NewOutputDir();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
            {
                TemplateDir = tplDir,
                OutputDir = outDir,
                ProjectName = "TestMissing",
            }));
        Assert.Contains("Thiếu 1 ảnh", ex.Message);
    }

    [Fact]
    public void SrtLoi_NemLoiKhongTaoDuAn()
    {
        var (tplDir, _) = TestHelpers.CreateTemplateDir(1);
        Track(tplDir);
        string imgDir = TestHelpers.CreateImageFolder(2);
        Track(imgDir);

        var parse = SrtParser.ParseText(
            "1\n00:00:05,000 --> 00:00:03,000\nSai\n\n2\n00:00:03,000 --> 00:00:06,000\nĐúng\n\n");
        var images = ImageScanner.Scan(imgDir);
        var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = 30 });
        var analysis = new TemplateAnalyzer().Analyze(tplDir);
        string outDir = NewOutputDir();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
            {
                TemplateDir = tplDir,
                OutputDir = outDir,
                ProjectName = "TestBadSrt",
            }));
        Assert.Contains("timecode", ex.Message);
    }

    [Fact]
    public void ThuMucDauRaDaTonTai_NemLoi()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(3);
        Track(tplDir); Track(imgDir);
        string outDir = NewOutputDir();

        var gen = new CapCutProjectGenerator();
        var opts = new GeneratorOptions
        {
            TemplateDir = tplDir,
            OutputDir = outDir,
            ProjectName = "TrungTen",
        };
        gen.Generate(analysis, mapping, opts);

        var ex = Assert.Throws<IOException>(() => gen.Generate(analysis, mapping, opts));
        Assert.Contains("đã tồn tại", ex.Message);
    }

    [Fact]
    public void Validator_DocLap_XacNhanDuAnHopLe()
    {
        var (analysis, mapping, tplDir, imgDir) = Prepare(5);
        Track(tplDir); Track(imgDir);
        string outDir = NewOutputDir();

        var result = new CapCutProjectGenerator().Generate(analysis, mapping, new GeneratorOptions
        {
            TemplateDir = tplDir,
            OutputDir = outDir,
            ProjectName = "TestValidate",
        });

        var validation = ProjectValidator.Validate(
            result.DraftContentPath, mapping, analysis.TemplateMaterialId);

        Assert.True(validation.AllPassed,
            string.Join("\n", validation.Checks.Where(c => !c.Passed).Select(c => c.Name + ": " + c.Details)));
    }
}
