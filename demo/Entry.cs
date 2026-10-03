using Schipper.Io.Xterm.Primitives;

namespace Schipper.Io.FileBrowser;

/// <summary>One row in either pane: a drive, the ".." up-link, a directory, or a file.</summary>
internal sealed record Entry(
    string Name,
    string FullPath,
    bool IsDirectory,
    DateTime Created,
    long Size = 0,
    bool IsUp = false,
    bool IsDrive = false);

/// <summary>Maps a file entry to a foreground color by kind / extension — the "syntax" coloring.</summary>
internal static class ExtensionColors
{
    public static Color For(Entry e)
    {
        if (e.IsUp)
        {
            return Color.Basic(BasicColor.BrightBlack);
        }

        if (e.IsDrive)
        {
            return Color.Basic(BasicColor.BrightCyan);
        }

        if (e.IsDirectory)
        {
            return Color.Basic(BasicColor.BrightBlue);
        }

        return Path.GetExtension(e.Name).ToLowerInvariant() switch
        {
            ".cs" or ".csproj" or ".sln" or ".fs" or ".fsproj" or ".vb" or ".ts" or ".tsx" or ".js"
                or ".jsx" or ".py" or ".go" or ".rs" or ".c" or ".cc" or ".cpp" or ".h" or ".hpp"
                or ".java" or ".kt" or ".rb" or ".php" or ".lua" or ".swift" or ".sql"
                => Color.Basic(BasicColor.BrightCyan),

            ".md" or ".markdown" or ".txt" or ".rst" or ".adoc" or ".log"
                => Color.Basic(BasicColor.White),

            ".json" or ".xml" or ".yml" or ".yaml" or ".toml" or ".ini" or ".cfg" or ".conf"
                or ".env" or ".editorconfig" or ".props" or ".targets"
                => Color.Basic(BasicColor.Yellow),

            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".ico" or ".webp"
                or ".tif" or ".tiff" or ".psd"
                => Color.Basic(BasicColor.BrightMagenta),

            ".zip" or ".tar" or ".gz" or ".tgz" or ".7z" or ".rar" or ".bz2" or ".xz" or ".zst"
                or ".nupkg" or ".jar"
                => Color.Basic(BasicColor.Red),

            ".exe" or ".dll" or ".so" or ".dylib" or ".bat" or ".cmd" or ".sh" or ".ps1" or ".com"
                or ".msi" or ".appimage"
                => Color.Basic(BasicColor.BrightGreen),

            ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" or ".mp4" or ".mkv" or ".avi" or ".mov"
                or ".webm"
                => Color.Basic(BasicColor.Magenta),

            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx"
                => Color.Basic(BasicColor.BrightRed),

            _ => Color.Default,
        };
    }
}
