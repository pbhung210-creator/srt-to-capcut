using SrtCapcut.Core.Images;
using SrtCapcut.Core.Srt;

namespace SrtCapcut.Core.Mapping;

/// <summary>
/// Ghép 1 đoạn SRT <-> 1 ảnh theo thứ tự. Ảnh thiếu/dư chỉ được báo cáo,
/// không tự thêm/xóa/ghép/đổi thứ tự.
/// </summary>
public static class MappingEngine
{
    public static MappingResult Build(
        IList<SrtSegment> segments,
        IList<ImageFile> images,
        MappingOptions options)
    {
        var result = new MappingResult { Fps = options.Fps };
        long fps = options.Fps;

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            ImageFile? img = i < images.Count ? images[i] : null;

            // Làm tròn theo frame bằng số nguyên, tính độc lập trên thời gian
            // tuyệt đối của từng đoạn -> không có sai số tích lũy.
            long frameStart = (seg.StartMs * fps + 500) / 1000;
            long frameEnd = (seg.EndMs * fps + 500) / 1000;
            long startUs = (frameStart * 1_000_000 + fps / 2) / fps;
            long endUs = (frameEnd * 1_000_000 + fps / 2) / fps;

            var m = new TimelineMapping
            {
                Order = i + 1,
                Segment = seg,
                Image = img,
                FrameStart = frameStart,
                FrameCount = Math.Max(frameEnd - frameStart, 0),
                StartUs = startUs,
                DurationUs = Math.Max(endUs - startUs, 0),
            };
            m.RoundingOffsetUs = m.DurationUs - seg.DurationMs * 1000;

            if (img is null)
            {
                m.Status = "Thiếu ảnh";
                m.Note = $"Không có ảnh thứ {i + 1} tương ứng với đoạn SRT này.";
                result.MissingImageCount++;
            }
            else if (!seg.IsValid)
            {
                m.Status = "SRT lỗi";
                m.Note = string.Join("; ", seg.Errors);
                result.InvalidSegmentCount++;
            }
            else
            {
                m.Status = "OK";
                if (seg.Warnings.Count > 0)
                    m.Note = string.Join("; ", seg.Warnings);
            }

            result.Mappings.Add(m);
        }

        for (int i = segments.Count; i < images.Count; i++)
            result.ExtraImages.Add(images[i]);

        result.TotalDurationMs = segments.Count == 0 ? 0 : segments.Max(s => s.EndMs);
        long lastFrame = (result.TotalDurationMs * fps + 500) / 1000;
        result.TotalDurationUs = (lastFrame * 1_000_000 + fps / 2) / fps;

        return result;
    }
}
