using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ClearTrace;

public static class ApplicationSearch
{
    public static IEnumerable<InstalledApp> Filter(IEnumerable<InstalledApp> applications, string? query)
    {
        var term = query?.Trim();
        return string.IsNullOrEmpty(term)
            ? applications
            : applications.Where(app => app.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                                      || app.DisplayPublisher.Contains(term, StringComparison.CurrentCultureIgnoreCase));
    }
}

public enum UninstallCommandType { Executable, WindowsInstaller }

public sealed record UninstallLaunchPlan(UninstallCommandType Type, string Executable, string Arguments)
{
    public ProcessStartInfo CreateStartInfo() => new(Executable, Arguments) { UseShellExecute = true };
}

public static class UninstallCommandParser
{
    private static readonly Regex UnquotedExecutable = new(@"^(?<file>.+?\.exe)(?<arguments>\s.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParse(string? command, out UninstallLaunchPlan? plan, out string error)
    {
        plan = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(command)) { error = "Aucune commande de désinstallation n’est enregistrée."; return false; }

        var text = command.Trim();
        string executable;
        string arguments;
        if (text.StartsWith('"'))
        {
            var closingQuote = text.IndexOf('"', 1);
            if (closingQuote <= 1) { error = "Le chemin de désinstallation est incomplet."; return false; }
            executable = text[1..closingQuote];
            arguments = text[(closingQuote + 1)..].Trim();
        }
        else
        {
            var match = UnquotedExecutable.Match(text);
            if (!match.Success) { error = "ClearTrace ne peut pas interpréter cette commande sans passer par un shell."; return false; }
            executable = match.Groups["file"].Value;
            arguments = match.Groups["arguments"].Value.Trim();
        }

        var filename = Path.GetFileName(executable);
        if (filename.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) || filename.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            error = "Les commandes via un shell ne sont pas lancées pour des raisons de sécurité.";
            return false;
        }
        if (!filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { error = "Le désinstalleur n’est pas un exécutable Windows reconnu."; return false; }

        var type = filename.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) ? UninstallCommandType.WindowsInstaller : UninstallCommandType.Executable;
        plan = new UninstallLaunchPlan(type, executable, arguments);
        return true;
    }
}

public enum UninstallSessionStatus
{
    CompletedAndRemoved,
    CompletedButStillListed,
    Failed,
    StillRunning,
    LaunchFailed
}

public sealed record UninstallSessionResult(UninstallSessionStatus Status, int? ExitCode, string Message)
{
    public bool IsTerminal => Status is not UninstallSessionStatus.StillRunning;
}

public sealed class UninstallSessionRunner
{
    public async Task<UninstallSessionResult> RunAsync(InstalledApp app, UninstallLaunchPlan plan, TimeSpan observationWindow, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.Start(plan.CreateStartInfo());
            if (process is null) return new(UninstallSessionStatus.LaunchFailed, null, "Windows n’a pas pu démarrer le désinstalleur.");

            var exitTask = process.WaitForExitAsync(cancellationToken);
            var exited = await Task.WhenAny(exitTask, Task.Delay(observationWindow, cancellationToken)) == exitTask;
            if (!exited) return new(UninstallSessionStatus.StillRunning, null, "Le désinstalleur est toujours en cours. Le résultat sera à vérifier après sa fermeture.");

            var stillListed = await Task.Run(() => InstalledApps.Load().Any(candidate => candidate.RegistryPath == app.RegistryPath), cancellationToken);
            if (process.ExitCode != 0) return new(UninstallSessionStatus.Failed, process.ExitCode, $"Le désinstalleur a terminé avec le code {process.ExitCode}.");
            return stillListed
                ? new(UninstallSessionStatus.CompletedButStillListed, process.ExitCode, "Le désinstalleur a terminé, mais l’application est toujours enregistrée dans Windows.")
                : new(UninstallSessionStatus.CompletedAndRemoved, process.ExitCode, "Désinstallation terminée : l’application n’est plus enregistrée dans Windows.");
        }
        catch (OperationCanceledException) { return new(UninstallSessionStatus.StillRunning, null, "L’observation a été interrompue ; vérifie l’état de l’application après la fermeture du désinstalleur."); }
        catch (Exception ex)
        {
            ApplicationLog.WriteException("Unable to run uninstaller", ex);
            return new(UninstallSessionStatus.LaunchFailed, null, $"Impossible de démarrer le désinstalleur : {ex.Message}");
        }
    }
}
