using System.Security.Cryptography;

namespace SrtCapcut.Core.CapCut;

/// <summary>
/// Sinh ID mới không trùng, giữ đúng định dạng của ID trong template:
/// UUID -> UUID mới; chuỗi hex -> hex ngẫu nhiên cùng độ dài.
/// </summary>
public static class IdGenerator
{
    public static string NewId(string? templateId)
    {
        // Chỉ coi là GUID khi có dấu gạch ngang (định dạng CapCut dùng).
        // Chuỗi hex 32 ký tự không gạch ngang được giữ nguyên dạng hex.
        if (!string.IsNullOrEmpty(templateId) &&
            templateId.Contains('-') &&
            Guid.TryParse(templateId, out _))
            return Guid.NewGuid().ToString("D").ToLowerInvariant();

        if (!string.IsNullOrEmpty(templateId) && IsHex(templateId))
        {
            int len = templateId.Length;
            byte[] bytes = RandomNumberGenerator.GetBytes((len + 1) / 2);
            string hex = Convert.ToHexString(bytes).ToLowerInvariant();
            string id = hex.Length > len ? hex.Substring(0, len) : hex;
            bool hasUpper = templateId.Any(char.IsUpper);
            bool hasLower = templateId.Any(char.IsLower);
            if (hasUpper && !hasLower)
                id = id.ToUpperInvariant();
            return id;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsHex(string s) =>
        s.Length >= 8 && s.All(c =>
            (c >= '0' && c <= '9') ||
            (c >= 'a' && c <= 'f') ||
            (c >= 'A' && c <= 'F'));
}
