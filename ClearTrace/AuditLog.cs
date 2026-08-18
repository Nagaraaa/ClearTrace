using System.Text.Json;
using System.IO;

namespace ClearTrace;

internal static class AuditLog
{
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearTrace", "audit.jsonl");

    public static void Write(string action, InstalledApp app)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var entry = new { timestampUtc = DateTimeOffset.UtcNow, action, application = app.Name, app.Version, app.Publisher, app.InstallLocation, app.UninstallCommand };
            File.AppendAllText(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch (Exception ex) { ApplicationLog.WriteException("Unable to write audit log", ex); }
    }

    public static string PathToLog => LogPath;
}
