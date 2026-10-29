namespace SrtCapcut.Core.Images;

/// <summary>
/// Đọc kích thước (rộng x cao) của ảnh PNG/JPEG/WEBP bằng cách đọc header,
/// không cần thư viện ngoài. Thất bại -> trả về false (không chặn luồng chính).
/// </summary>
public static class ImageDimensionReader
{
    public static bool TryRead(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => TryReadPng(path, out width, out height),
                ".jpg" or ".jpeg" => TryReadJpeg(path, out width, out height),
                ".webp" => TryReadWebp(path, out width, out height),
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadPng(string path, out int w, out int h)
    {
        w = 0; h = 0;
        using var fs = File.OpenRead(path);
        Span<byte> buf = stackalloc byte[24];
        if (fs.Read(buf) < 24) return false;
        if (buf[0] != 0x89 || buf[1] != 0x50 || buf[2] != 0x4E || buf[3] != 0x47)
            return false; // không phải chữ ký PNG
        w = (buf[16] << 24) | (buf[17] << 16) | (buf[18] << 8) | buf[19];
        h = (buf[20] << 24) | (buf[21] << 16) | (buf[22] << 8) | buf[23];
        return w > 0 && h > 0 && w < 100_000 && h < 100_000;
    }

    private static bool TryReadJpeg(string path, out int w, out int h)
    {
        w = 0; h = 0;
        using var fs = File.OpenRead(path);
        int toRead = (int)Math.Min(65536, fs.Length);
        byte[] data = new byte[toRead];
        int read = fs.Read(data, 0, toRead);
        if (read < 4 || data[0] != 0xFF || data[1] != 0xD8)
            return false;

        int i = 2;
        while (i + 9 < read)
        {
            if (data[i] != 0xFF) { i++; continue; }
            byte marker = data[i + 1];
            if (marker is >= 0xC0 and <= 0xC3) // SOF0..SOF3: chứa kích thước
            {
                h = (data[i + 5] << 8) | data[i + 6];
                w = (data[i + 7] << 8) | data[i + 8];
                return w > 0 && h > 0;
            }
            if (marker is 0xD8 or 0xD9 or 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                i += 2;
                continue;
            }
            int len = (data[i + 2] << 8) | data[i + 3];
            if (len < 2) return false;
            i += 2 + len;
        }
        return false;
    }

    private static bool TryReadWebp(string path, out int w, out int h)
    {
        w = 0; h = 0;
        using var fs = File.OpenRead(path);
        byte[] buf = new byte[32];
        int read = fs.Read(buf, 0, buf.Length);
        if (read < 16) return false;
        if (buf[0] != 'R' || buf[1] != 'I' || buf[2] != 'F' || buf[3] != 'F')
            return false;
        if (buf[8] != 'W' || buf[9] != 'E' || buf[10] != 'B' || buf[11] != 'P')
            return false;

        string chunk = new(new[] { (char)buf[12], (char)buf[13], (char)buf[14], (char)buf[15] });

        if (chunk == "VP8X" && read >= 27)
        {
            // byte 20: flags; 21-23: (rộng-1) 24-bit LE; 24-26: (cao-1) 24-bit LE
            w = (buf[21] | (buf[22] << 8) | (buf[23] << 16)) + 1;
            h = (buf[24] | (buf[25] << 8) | (buf[26] << 16)) + 1;
            return w > 0 && h > 0;
        }
        if (chunk == "VP8 " && read >= 30)
        {
            // frame tag 3 byte (20-22) + start code 3 byte (23-25), rồi rộng/cao 14-bit LE
            w = buf[26] | ((buf[27] & 0x3F) << 8);
            h = buf[28] | ((buf[29] & 0x3F) << 8);
            return w > 0 && h > 0;
        }
        if (chunk == "VP8L" && read >= 26)
        {
            // byte 21: signature 0x2F; sau đó 14+14 bit LSB-first
            int b1 = buf[22], b2 = buf[23], b3 = buf[24], b4 = buf[25];
            w = 1 + (b1 | ((b2 & 0x3F) << 8));
            h = 1 + (((b2 >> 6) & 0x03) | (b3 << 2) | ((b4 & 0x0F) << 10));
            return w > 0 && h > 0;
        }
        return false;
    }
}
