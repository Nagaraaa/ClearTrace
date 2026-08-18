using System.Text;
using System.IO;

namespace ClearTrace;

internal static class ApplicationLog
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearTrace", "diagnostic.log");

    public static void WriteException(string context, Exception exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTimeOffset.UtcNow:O}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch { /* A logger must never crash the application it is diagnosing. */ }
    }
}
