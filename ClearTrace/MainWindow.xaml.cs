using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ClearTrace;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly List<InstalledApp> _apps = [];
    private string _searchText = string.Empty;
    private InstalledApp? _selectedApp;
    private string _statusText = "Inventaire en cours…";
    private bool _isBusy;

    public ObservableCollection<InstalledApp> FilteredApps { get; } = [];
    public ObservableCollection<ResidueCandidate> Residues { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;

    public string SearchText { get => _searchText; set { if (_searchText == value) return; _searchText = value; ApplyFilter(); OnPropertyChanged(); } }
    public InstalledApp? SelectedApp { get => _selectedApp; set { if (_selectedApp == value) return; _selectedApp = value; Residues.Clear(); OnPropertyChanged(); OnPropertyChanged(nameof(ResiduesEmptyVisibility)); } }
    public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
    public int AppCount => _apps.Count;
    public string ResultsText => FilteredApps.Count == _apps.Count ? $"{_apps.Count} applications détectées" : $"{FilteredApps.Count} résultat(s)";
    public Visibility ResiduesEmptyVisibility => Residues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await LoadAppsAsync();
    }

    private async Task LoadAppsAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        StatusText = "Inventaire des applications en cours…";
        try
        {
            var loaded = await Task.Run(InstalledApps.Load);
            _apps.Clear();
            _apps.AddRange(loaded);
            ApplyFilter();
            StatusText = $"{_apps.Count} applications détectées dans le Registre Windows.";
            OnPropertyChanged(nameof(AppCount));
        }
        catch (Exception ex) { StatusText = $"Échec de l’inventaire : {ex.Message}"; }
        finally { _isBusy = false; }
    }

    private void ApplyFilter()
    {
        var selectedName = SelectedApp?.Name;
        var query = SearchText.Trim();
        var result = ApplicationSearch.Filter(_apps, query);
        FilteredApps.Clear();
        foreach (var app in result) FilteredApps.Add(app);
        if (SelectedApp is not null && !FilteredApps.Contains(SelectedApp)) SelectedApp = null;
        if (SelectedApp is null && FilteredApps.Count > 0) SelectedApp = FilteredApps.FirstOrDefault(app => app.Name == selectedName) ?? FilteredApps[0];
        OnPropertyChanged(nameof(ResultsText));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAppsAsync();

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || SelectedApp is null) { StatusText = "Sélectionne une application avant de lancer l’analyse."; return; }
        _isBusy = true;
        StatusText = $"Analyse prudente de {SelectedApp.Name}…";
        try
        {
            var candidates = await Task.Run(() => ResidueScanner.Scan(SelectedApp));
            Residues.Clear();
            foreach (var candidate in candidates) Residues.Add(candidate);
            AuditLog.Write("residue-scan", SelectedApp);
            StatusText = candidates.Count == 0 ? "Aucune trace candidate trouvée." : $"{candidates.Count} trace(s) candidate(s) à vérifier.";
        }
        catch (Exception ex) { StatusText = $"Échec de l’analyse : {ex.Message}"; }
        finally { _isBusy = false; OnPropertyChanged(nameof(ResiduesEmptyVisibility)); }
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedApp is null) { StatusText = "Sélectionne une application avant de la désinstaller."; return; }
        if (!UninstallCommandParser.TryParse(SelectedApp.UninstallCommand, out var plan, out var error)) { StatusText = error; return; }
        var launchPlan = plan!;
        var kind = launchPlan.Type == UninstallCommandType.WindowsInstaller ? "Windows Installer (MSI)" : "exécutable";
        var confirmation = MessageBox.Show($"Lancer la désinstallation officielle de :\n\n{SelectedApp.Name}\n\nType détecté : {kind}\nExécutable : {launchPlan.Executable}\nArguments : {launchPlan.Arguments}\n\nClearTrace ne supprimera aucune trace automatiquement.", "Confirmer la désinstallation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;
        try
        {
            AuditLog.Write("uninstall-launched", SelectedApp);
            Process.Start(launchPlan.CreateStartInfo());
            StatusText = $"Désinstallation lancée : {SelectedApp.Name}.";
        }
        catch (Exception ex) { StatusText = $"Impossible de lancer la désinstallation : {ex.Message}"; }
    }

    private void OpenJournal_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AuditLog.PathToLog)!);
        if (!File.Exists(AuditLog.PathToLog)) File.WriteAllText(AuditLog.PathToLog, string.Empty);
        Process.Start(new ProcessStartInfo(AuditLog.PathToLog) { UseShellExecute = true });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
