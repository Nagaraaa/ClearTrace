using System.Text.Json;
using System.IO;

namespace ClearTrace;

internal static class AuditLog
{
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearTrace", "audit.jsonl");

    public static void Write(string action, InstalledApp app)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        var entry = new { timestampUtc = DateTimeOffset.UtcNow, action, application = app.Name, app.Version, app.Publisher, app.InstallLocation, app.UninstallCommand };
        File.AppendAllText(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
    }

    public static string PathToLog => LogPath;
}
