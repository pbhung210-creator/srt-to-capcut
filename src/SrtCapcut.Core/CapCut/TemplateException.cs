namespace SrtCapcut.Core.CapCut;

/// <summary>Lỗi khi đọc/phân tích dự án CapCut mẫu: thông báo rõ ràng bằng tiếng Việt.</summary>
public sealed class TemplateException : Exception
{
    public TemplateException(string message) : base(message) { }
    public TemplateException(string message, Exception inner) : base(message, inner) { }
}
