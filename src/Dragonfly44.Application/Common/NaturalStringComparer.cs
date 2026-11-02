using System.Text.RegularExpressions;

namespace Dragonfly44.Application.Common;

/// <summary>
/// Natural sort order comparer that orders strings containing numbers logically
/// (e.g., "page1.png", "page2.png", "page10.png") rather than purely alphabetically.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int ix = 0;
        int iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            char cx = x[ix];
            char cy = y[iy];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                // Scan digit sequences
                int startX = ix;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                int lenX = ix - startX;

                int startY = iy;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;
                int lenY = iy - startY;

                // Skip leading zeros to compare numerical value
                int nzX = startX;
                while (nzX < ix - 1 && x[nzX] == '0') nzX++;
                int numLenX = ix - nzX;

                int nzY = startY;
                while (nzY < iy - 1 && y[nzY] == '0') nzY++;
                int numLenY = iy - nzY;

                if (numLenX != numLenY)
                {
                    return numLenX.CompareTo(numLenY);
                }

                // Same length of digits: compare lexicographically (which equals numerical value)
                for (int k = 0; k < numLenX; k++)
                {
                    if (x[nzX + k] != y[nzY + k])
                    {
                        return x[nzX + k].CompareTo(y[nzY + k]);
                    }
                }

                // If numerical value is identical, compare total length (e.g. "01" vs "1")
                if (lenX != lenY)
                {
                    return lenX.CompareTo(lenY);
                }
            }
            else
            {
                // Compare case-insensitively
                char upperX = char.ToUpperInvariant(cx);
                char upperY = char.ToUpperInvariant(cy);

                if (upperX != upperY)
                {
                    return upperX.CompareTo(upperY);
                }

                ix++;
                iy++;
            }
        }

        return x.Length.CompareTo(y.Length);
    }
}
