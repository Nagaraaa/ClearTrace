using ClearTrace;

var tests = new (string Name, Action Run)[]
{
    ("Search filters by application name", SearchFiltersByName),
    ("Search filters by publisher", SearchFiltersByPublisher),
    ("Quoted executable command is parsed", ParsesQuotedExecutable),
    ("MSI command is recognized", RecognizesMsi),
    ("Shell wrapper is refused", RefusesShellWrapper),
    ("Malformed command is refused", RefusesMalformedCommand)
};

var failures = new List<string>();
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS  {name}"); }
    catch (Exception ex) { failures.Add($"FAIL  {name}: {ex.Message}"); }
}
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

static InstalledApp App(string name, string publisher) => new(name, "1.0", publisher, null, null, "fixture");
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void SearchFiltersByName() => Assert(ApplicationSearch.Filter([App("ClearTrace Sandbox", "Open Source"), App("Other", "Vendor")], "sandbox").Single().Name == "ClearTrace Sandbox", "Expected name match.");
static void SearchFiltersByPublisher() => Assert(ApplicationSearch.Filter([App("Other", "ClearTrace Team")], "team").Single().Name == "Other", "Expected publisher match.");
static void ParsesQuotedExecutable()
{
    Assert(UninstallCommandParser.TryParse("\"C:\\Program Files\\Sample\\uninstall.exe\" /quiet", out var plan, out _), "Expected a parse result.");
    Assert(plan!.Executable == "C:\\Program Files\\Sample\\uninstall.exe" && plan.Arguments == "/quiet", "Unexpected parsed executable or arguments.");
}
static void RecognizesMsi()
{
    Assert(UninstallCommandParser.TryParse("MsiExec.exe /X {12345678-1234-1234-1234-123456789ABC}", out var plan, out _), "Expected MSI parse result.");
    Assert(plan!.Type == UninstallCommandType.WindowsInstaller, "Expected MSI command type.");
}
static void RefusesShellWrapper() => Assert(!UninstallCommandParser.TryParse("cmd.exe /c del C:\\temp", out _, out _), "cmd.exe must be rejected.");
static void RefusesMalformedCommand() => Assert(!UninstallCommandParser.TryParse("not-a-command", out _, out _), "Malformed command must be rejected.");
