namespace SrtCapcut.Core.Images;

/// <summary>
/// So sánh chuỗi theo "thứ tự tự nhiên": cụm chữ số được so theo giá trị số,
/// nên "2.png" đứng trước "10.png" (không bị xếp thành 1, 10, 2).
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    private NaturalStringComparer() { }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int ix = 0, iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            char cx = x[ix], cy = y[iy];
            bool dx = char.IsDigit(cx), dy = char.IsDigit(cy);

            if (dx && dy)
            {
                int sx = ix;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                int sy = iy;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;

                string nx = x.Substring(sx, ix - sx).TrimStart('0');
                string ny = y.Substring(sy, iy - sy).TrimStart('0');

                int cmp = nx.Length.CompareTo(ny.Length);
                if (cmp != 0) return cmp;
                cmp = string.Compare(nx, ny, StringComparison.Ordinal);
                if (cmp != 0) return cmp;
                // Hai cụm số bằng nhau (kể cả số 0 ở đầu) -> so tiếp phần còn lại.
            }
            else
            {
                int cmp = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
                if (cmp != 0) return cmp;
                ix++;
                iy++;
            }
        }

        return (x.Length - ix).CompareTo(y.Length - iy);
    }
}
