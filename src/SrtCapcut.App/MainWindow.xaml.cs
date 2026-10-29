using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SrtCapcut.Core.CapCut;
using SrtCapcut.Core.Images;
using SrtCapcut.Core.Mapping;
using SrtCapcut.Core.Reports;
using SrtCapcut.Core.Srt;
using SrtCapcut.Core.Utils;

namespace SrtCapcut.App;

public partial class MainWindow : Window
{
    private readonly AppLogger _logger = new();

    private SrtParseResult? _parseResult;
    private List<ImageFile>? _images;
    private MappingResult? _mapping;
    private TemplateAnalysis? _analysis;
    private GenerateResult? _lastResult;

    public MainWindow()
    {
        InitializeComponent();
        _logger.LineAdded += line => Dispatcher.Invoke(() =>
        {
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.ScrollToEnd();
        });
        _logger.Log("Sẵn sàng. Hãy chọn file SRT, thư mục ảnh, dự án CapCut mẫu và thư mục xuất.");
        _logger.Log("Lưu ý: trước khi dùng, hãy tạo dự án mẫu theo HUONG-DAN-TEMPLATE.md rồi ĐÓNG CapCut.");
    }

    // ---------- Chọn đầu vào ----------

    private void BtnSrt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn file phụ đề SRT",
            Filter = "Phụ đề SRT (*.srt)|*.srt|Tất cả file (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            txtSrt.Text = dlg.FileName;
    }

    private void BtnImages_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục chứa ảnh (PNG/JPG/WEBP)" };
        if (dlg.ShowDialog() == true)
            txtImages.Text = dlg.FolderName;
    }

    private void BtnTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Chọn thư mục dự án CapCut MẪU (chứa draft_content.json)"
        };
        if (dlg.ShowDialog() != true)
            return;

        txtTemplate.Text = dlg.FolderName;
        try
        {
            var analysis = new TemplateAnalyzer().Analyze(dlg.FolderName);
            _analysis = analysis;
            _logger.Log($"Đã đọc dự án mẫu: 1 clip ảnh mẫu, thời lượng " +
                        $"{analysis.TemplateSegmentDurationUs / 1000.0:F1} ms, FPS mẫu {analysis.TemplateFps}.");
            foreach (string w in analysis.Warnings)
                _logger.Log("Cảnh báo template: " + w);
        }
        catch (Exception ex)
        {
            _analysis = null;
            _logger.Log("LỖI dự án mẫu: " + ex.Message);
            MessageBox.Show(ex.Message, "Dự án mẫu không hợp lệ",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục xuất dự án" };
        if (dlg.ShowDialog() == true)
            txtOutput.Text = dlg.FolderName;
    }

    // ---------- Kiểm tra ----------

    private async void BtnCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs(requireOutput: false))
            return;

        SetBusy(true, "Đang kiểm tra SRT và ảnh...");
        try
        {
            string srtPath = txtSrt.Text.Trim();
            string imgDir = txtImages.Text.Trim();
            string tplDir = txtTemplate.Text.Trim();
            int fps = SelectedFps();

            var check = await Task.Run(() =>
            {
                var parse = SrtParser.ParseFile(srtPath);
                var images = ImageScanner.Scan(imgDir);
                var mapping = MappingEngine.Build(parse.Segments, images, new MappingOptions { Fps = fps });
                var analysis = new TemplateAnalyzer().Analyze(tplDir);
                return (parse, images, mapping, analysis);
            });

            _parseResult = check.parse;
            _images = check.images;
            _mapping = check.mapping;
            _analysis = check.analysis;

            UpdateStats();
            gridPreview.ItemsSource = _mapping.Mappings.Select(m => new PreviewRow
            {
                Order = m.Order,
                ImageName = m.Image?.FileName ?? "(thiếu ảnh)",
                StartText = SrtTimecode.Format(m.StartMs),
                EndText = SrtTimecode.Format(m.EndMs),
                DurationText = SrtTimecode.FormatSeconds(m.DurationMs),
                Status = m.Status,
                Note = m.Note ?? string.Empty
            }).ToList();

            _logger.Log($"SRT: {_parseResult.Segments.Count} đoạn " +
                        $"({_parseResult.InvalidCount} lỗi, {_parseResult.OverlapCount} trùng thời gian, " +
                        $"{_parseResult.GapCount} khoảng trống).");
            foreach (var err in _parseResult.Errors.Take(10))
                _logger.Log("Lỗi SRT: " + err.Message);
            foreach (var seg in _parseResult.Segments.Where(s => !s.IsValid).Take(10))
                _logger.Log($"Đoạn {seg.Index}: " + string.Join("; ", seg.Errors));

            _logger.Log($"Ảnh: {_images.Count} file. Thiếu: {_mapping.MissingImageCount}, dư: {_mapping.ExtraImages.Count}.");
            foreach (var extra in _mapping.ExtraImages.Take(20))
                _logger.Log("Ảnh dư (không tạo clip): " + extra.FileName);
            if (_mapping.ExtraImages.Count > 20)
                _logger.Log($"... và {_mapping.ExtraImages.Count - 20} ảnh dư khác (xem file CSV).");

            _logger.Log($"Template: track #{_analysis.TrackIndex}, nhóm material '{_analysis.MaterialCollectionName}', " +
                        $"FPS mẫu {_analysis.TemplateFps}.");
            foreach (string w in _analysis.Warnings)
                _logger.Log("Cảnh báo template: " + w);

            bool canGenerate = _mapping.MissingImageCount == 0 &&
                               _mapping.InvalidSegmentCount == 0 &&
                               _mapping.Mappings.Count > 0;
            btnGenerate.IsEnabled = canGenerate;
            btnCsv.IsEnabled = _mapping.Mappings.Count > 0;

            if (canGenerate)
                _logger.Log("Kiểm tra xong: DỮ LIỆU HỢP LỆ, có thể nhấn TẠO DỰ ÁN CAPCUT.");
            else
                _logger.Log("CHƯA THỂ TẠO DỰ ÁN: hãy xử lý các lỗi / ảnh thiếu ở trên trước.");

            txtStatus.Text = "Kiểm tra xong.";
        }
        catch (Exception ex)
        {
            _logger.Log("LỖI: " + ex.Message);
            MessageBox.Show(ex.Message, "Lỗi kiểm tra", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, "Sẵn sàng.");
        }
    }

    // ---------- Tạo dự án ----------

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_mapping is null || _analysis is null)
        {
            MessageBox.Show("Hãy nhấn KIỂM TRA SRT VÀ ẢNH trước.",
                "Chưa kiểm tra", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!ValidateInputs(requireOutput: true))
            return;

        var options = new GeneratorOptions
        {
            TemplateDir = txtTemplate.Text.Trim(),
            OutputDir = txtOutput.Text.Trim(),
            ProjectName = "SRT_CapCut_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            Fps = SelectedFps(),
            AspectRatio = ((ComboBoxItem)cboAspect.SelectedItem).Content.ToString()!,
            CopyAssets = chkCopyAssets.IsChecked == true,
            Progress = new Progress<GenerateProgress>(p =>
            {
                progressBar.Value = p.Percent;
                txtStatus.Text = p.Message;
            })
        };

        SetBusy(true, "Đang tạo dự án CapCut...");
        try
        {
            var result = await Task.Run(() =>
                new CapCutProjectGenerator().Generate(_analysis, _mapping, options));

            _lastResult = result;
            btnOpenFolder.IsEnabled = true;

            _logger.Log($"TẠO DỰ ÁN XONG: {result.ClipCount} clip ảnh, " +
                        $"tổng thời lượng {result.TotalDurationUs / 1_000_000.0:F2} giây.");
            _logger.Log("Thư mục kết quả: " + result.OutputRoot);
            _logger.Log("Draft ID mới: " + result.DraftId);
            foreach (string w in result.Warnings)
                _logger.Log("Cảnh báo: " + w);
            _logger.Log("Xem README.txt trong thư mục kết quả để biết cách mở bằng CapCut.");

            MessageBox.Show(
                $"Tạo dự án thành công!\n\n" +
                $"Số clip ảnh: {result.ClipCount}\n" +
                $"Thư mục kết quả:\n{result.OutputRoot}\n\n" +
                "Xem file README.txt trong thư mục kết quả để biết cách mở bằng CapCut.",
                "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Log("LỖI tạo dự án: " + ex.Message);
            MessageBox.Show(ex.Message, "Lỗi tạo dự án", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            progressBar.Value = 0;
            SetBusy(false, "Sẵn sàng.");
        }
    }

    // ---------- Mở thư mục / xuất CSV ----------

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
            return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_lastResult.OutputRoot}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không mở được thư mục: " + ex.Message,
                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_mapping is null)
            return;
        var dlg = new SaveFileDialog
        {
            Title = "Xuất báo cáo đối chiếu SRT - ảnh",
            Filter = "CSV (*.csv)|*.csv",
            FileName = "SRT_IMAGE_MAP.csv"
        };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            CsvReportWriter.Write(dlg.FileName, _mapping);
            _logger.Log("Đã xuất báo cáo CSV: " + dlg.FileName);
            MessageBox.Show("Đã xuất báo cáo CSV:\n" + dlg.FileName,
                "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không xuất được CSV: " + ex.Message,
                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------- Hàm phụ ----------

    private bool ValidateInputs(bool requireOutput)
    {
        if (string.IsNullOrWhiteSpace(txtSrt.Text) || !File.Exists(txtSrt.Text.Trim()))
        {
            MessageBox.Show("Hãy chọn file SRT hợp lệ.", "Thiếu dữ liệu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(txtImages.Text) || !Directory.Exists(txtImages.Text.Trim()))
        {
            MessageBox.Show("Hãy chọn thư mục ảnh hợp lệ.", "Thiếu dữ liệu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(txtTemplate.Text) || !Directory.Exists(txtTemplate.Text.Trim()))
        {
            MessageBox.Show("Hãy chọn thư mục dự án CapCut mẫu.", "Thiếu dữ liệu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (requireOutput &&
            (string.IsNullOrWhiteSpace(txtOutput.Text) || !Directory.Exists(txtOutput.Text.Trim())))
        {
            MessageBox.Show("Hãy chọn thư mục xuất dự án.", "Thiếu dữ liệu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private int SelectedFps() =>
        int.Parse(((ComboBoxItem)cboFps.SelectedItem).Content.ToString()!);

    private void UpdateStats()
    {
        if (_parseResult is null || _images is null || _mapping is null)
            return;
        statSrt.Content = $"Đoạn SRT: {_parseResult.Segments.Count}";
        statImages.Content = $"Ảnh tìm thấy: {_images.Count}";
        statMissing.Content = $"Ảnh thiếu: {_mapping.MissingImageCount}";
        statExtra.Content = $"Ảnh dư: {_mapping.ExtraImages.Count}";
        statDuration.Content = $"Tổng thời lượng: {SrtTimecode.Format(_mapping.TotalDurationMs)}";
        statInvalid.Content = $"Đoạn SRT lỗi: {_mapping.InvalidSegmentCount}";
    }

    private void SetBusy(bool busy, string status)
    {
        btnCheck.IsEnabled = !busy;
        btnSrt.IsEnabled = !busy;
        btnImages.IsEnabled = !busy;
        btnTemplate.IsEnabled = !busy;
        btnOutput.IsEnabled = !busy;
        if (!busy)
        {
            btnGenerate.IsEnabled = _mapping is not null &&
                                    _mapping.MissingImageCount == 0 &&
                                    _mapping.InvalidSegmentCount == 0 &&
                                    _mapping.Mappings.Count > 0;
            btnCsv.IsEnabled = _mapping is not null && _mapping.Mappings.Count > 0;
            btnOpenFolder.IsEnabled = _lastResult is not null;
        }
        else
        {
            btnGenerate.IsEnabled = false;
            btnCsv.IsEnabled = false;
            btnOpenFolder.IsEnabled = false;
        }
        txtStatus.Text = status;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    private sealed class PreviewRow
    {
        public int Order { get; init; }
        public string ImageName { get; init; } = string.Empty;
        public string StartText { get; init; } = string.Empty;
        public string EndText { get; init; } = string.Empty;
        public string DurationText { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
    }
}
