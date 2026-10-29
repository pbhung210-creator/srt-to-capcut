using System.Text;

namespace SrtCapcut.Core.Utils;

/// <summary>Logger đơn giản, thread-safe, dùng chung cho Core và giao diện.</summary>
public sealed class AppLogger
{
    private readonly object _lock = new();
    private readonly List<string> _lines = new();

    public event Action<string>? LineAdded;

    public void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (_lock)
            _lines.Add(line);
        LineAdded?.Invoke(line);
    }

    public IReadOnlyList<string> Lines
    {
        get { lock (_lock) return _lines.ToList(); }
    }

    public void SaveTo(string path)
    {
        lock (_lock)
            File.WriteAllLines(path, _lines, new UTF8Encoding(false));
    }
}
