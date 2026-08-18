using Microsoft.Win32;

namespace ClearTrace;

internal sealed record InstalledApp(string Name, string? Version, string? Publisher, string? InstallLocation,
    string? UninstallCommand, string RegistryPath)
{
    public string DisplayVersion => string.IsNullOrWhiteSpace(Version) ? "—" : Version;
    public string DisplayPublisher => string.IsNullOrWhiteSpace(Publisher) ? "—" : Publisher;
}

internal static class InstalledApps
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<InstalledApp> Load()
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, view) in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default)
        })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(UninstallKey);
                if (uninstallKey is null) continue;
                foreach (var childName in uninstallKey.GetSubKeyNames())
                {
                    using var child = uninstallKey.OpenSubKey(childName);
                    if (child is null) continue;
                    var name = child.GetValue("DisplayName")?.ToString()?.Trim() ?? string.Empty;
                    if (name.Length == 0 || child.GetValue("SystemComponent")?.ToString() == "1") continue;
                    var app = new InstalledApp(name, child.GetValue("DisplayVersion")?.ToString(), child.GetValue("Publisher")?.ToString(),
                        child.GetValue("InstallLocation")?.ToString(), child.GetValue("QuietUninstallString")?.ToString() ?? child.GetValue("UninstallString")?.ToString(),
                        $"{hive} ({view}): {UninstallKey}\\{childName}");
                    apps.TryAdd($"{app.Name}|{app.DisplayVersion}|{app.Publisher}", app);
                }
            }
            catch (UnauthorizedAccessException) { }
        }
        return apps.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
