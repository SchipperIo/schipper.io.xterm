using System.Globalization;
using System.Text;

namespace Schipper.Io.Xterm.Primitives;

/// <summary>
/// Display-width and sanitizing helpers for grid painting. Continuation cells for wide runes use
/// <see cref="ContinuationGlyph"/>; the renderer skips those columns because the previous glyph
/// already advanced the terminal cursor by two.
/// </summary>
internal static class CellPaint
{
    internal const char ContinuationGlyph = '\0';
    internal const char SubstituteGlyph = '\uFFFD';

    internal static bool IsContinuation(char glyph) => glyph == ContinuationGlyph;

    internal static bool IsControl(int codePoint) =>
        codePoint <= 0x1F || (codePoint >= 0x7F && codePoint <= 0x9F);

    internal static char Sanitize(char glyph)
    {
        if (glyph == ContinuationGlyph || char.IsSurrogate(glyph) || IsControl(glyph))
        {
            return SubstituteGlyph;
        }

        return glyph;
    }

    internal static char SanitizeRune(Rune rune)
    {
        int value = rune.Value;
        if (IsControl(value) || value > char.MaxValue)
        {
            return SubstituteGlyph;
        }

        return (char)value;
    }

    internal static int Width(char glyph)
    {
        if (IsContinuation(glyph))
        {
            return 0;
        }

        return Width(new Rune(Sanitize(glyph)));
    }

    internal static int Width(Rune rune)
    {
        int value = rune.Value;
        if (value == 0)
        {
            return 0;
        }

        UnicodeCategory cat = Rune.GetUnicodeCategory(rune);
        if (cat is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.SpacingCombiningMark)
        {
            return 0;
        }

        return IsWide(value) ? 2 : 1;
    }

    internal static int Measure(ReadOnlySpan<char> text)
    {
        int width = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            char glyph = SanitizeRune(rune);
            width += Width(new Rune(glyph));
        }

        return width;
    }

    internal static string Clip(string text, int maxWidth)
    {
        if (maxWidth <= 0 || string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        int used = 0;
        int chars = 0;
        foreach (Rune rune in text.AsSpan().EnumerateRunes())
        {
            char glyph = SanitizeRune(rune);
            int w = Width(new Rune(glyph));
            if (w <= 0)
            {
                chars += rune.Utf16SequenceLength;
                continue;
            }

            if (used + w > maxWidth)
            {
                break;
            }

            used += w;
            chars += rune.Utf16SequenceLength;
        }

        return chars >= text.Length ? text : text[..chars];
    }

    private static bool IsWide(int codePoint) =>
        codePoint is (>= 0x1100 and <= 0x115F)
            or (>= 0x231A and <= 0x231B)
            or (>= 0x2329 and <= 0x232A)
            or (>= 0x23E9 and <= 0x23EC)
            or 0x23F0 or 0x23F3
            or (>= 0x25FD and <= 0x25FE)
            or (>= 0x2614 and <= 0x2615)
            or (>= 0x2648 and <= 0x2653)
            or 0x267F or 0x2693 or 0x26A1
            or (>= 0x26AA and <= 0x26AB)
            or (>= 0x26BD and <= 0x26BE)
            or (>= 0x26C4 and <= 0x26C5)
            or 0x26CE or 0x26D4 or 0x26EA
            or (>= 0x26F2 and <= 0x26F3)
            or 0x26F5 or 0x26FA or 0x26FD
            or 0x2705 or (>= 0x270A and <= 0x270B) or 0x2728 or 0x274C or 0x274E
            or (>= 0x2753 and <= 0x2755) or 0x2757
            or (>= 0x2795 and <= 0x2797) or 0x27B0 or 0x27BF
            or (>= 0x2B1B and <= 0x2B1C) or 0x2B50 or 0x2B55
            or (>= 0x2E80 and <= 0xA4CF)
            or (>= 0xAC00 and <= 0xD7A3)
            or (>= 0xF900 and <= 0xFAFF)
            or (>= 0xFE10 and <= 0xFE19)
            or (>= 0xFE30 and <= 0xFE6F)
            or (>= 0xFF00 and <= 0xFF60)
            or (>= 0xFFE0 and <= 0xFFE6)
            or (>= 0x1F300 and <= 0x1F64F)
            or (>= 0x1F900 and <= 0x1F9FF)
            or (>= 0x20000 and <= 0x3FFFD);
}
