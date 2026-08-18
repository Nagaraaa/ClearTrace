using ClearTrace;
using Microsoft.Win32;

const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ClearTraceWorkflowFixture";
var fixtureDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearTrace", "workflow-fixture");

if (args.Contains("--cleartrace-fixture-uninstall", StringComparer.OrdinalIgnoreCase))
{
    RemoveFixture();
    return 0;
}

try
{
    RemoveFixture();
    InstallFixture();
    var app = InstalledApps.Load().Single(candidate => candidate.Name == "ClearTrace Test Package");
    Assert(UninstallCommandParser.TryParse(app.UninstallCommand, out var plan, out var error), error);
    var result = await new UninstallSessionRunner().RunAsync(app, plan!, TimeSpan.FromSeconds(10));
    Assert(result.Status == UninstallSessionStatus.CompletedAndRemoved, $"Unexpected status: {result.Status} ({result.Message})");
    Assert(!InstalledApps.Load().Any(candidate => candidate.Name == "ClearTrace Test Package"), "Fixture is still registered after uninstallation.");
    Assert(!Directory.Exists(fixtureDirectory), "Fixture files are still present after uninstallation.");
    Console.WriteLine("PASS  Safe end-to-end uninstall workflow");
    return 0;
}
finally { RemoveFixture(); }

void InstallFixture()
{
    Directory.CreateDirectory(fixtureDirectory);
    File.WriteAllText(Path.Combine(fixtureDirectory, "owned-by-cleartrace-test.txt"), "This folder is safe to remove.");
    using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)!;
    key.SetValue("DisplayName", "ClearTrace Test Package");
    key.SetValue("DisplayVersion", "1.0.0");
    key.SetValue("Publisher", "ClearTrace Tests");
    key.SetValue("InstallLocation", fixtureDirectory);
    key.SetValue("UninstallString", $"\"{Environment.ProcessPath}\" --cleartrace-fixture-uninstall");
}

void RemoveFixture()
{
    try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false); } catch { }
    try { if (Directory.Exists(fixtureDirectory)) Directory.Delete(fixtureDirectory, recursive: true); } catch { }
}

static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
