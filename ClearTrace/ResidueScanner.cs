namespace ClearTrace;

internal sealed record ResidueCandidate(string Path, string Type, string Reason);

internal static class ResidueScanner
{
    public static IReadOnlyList<ResidueCandidate> Scan(InstalledApp app)
    {
        var result = new List<ResidueCandidate>();
        AddDirectory(result, app.InstallLocation, "Dossier d’installation", "Emplacement enregistré par Windows");
        var name = SanitizeFolderName(app.Name);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddDirectory(result, Path.Combine(appData, name), "Données utilisateur", "Correspondance exacte avec le nom du logiciel");
        AddDirectory(result, Path.Combine(localAppData, name), "Données locales", "Correspondance exacte avec le nom du logiciel");
        return result.DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddDirectory(List<ResidueCandidate> result, string? path, string type, string reason)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) result.Add(new ResidueCandidate(path, type, reason));
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
    }
}
