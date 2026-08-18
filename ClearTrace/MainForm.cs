using System.Diagnostics;

namespace ClearTrace;

internal sealed class MainForm : Form
{
    private readonly TextBox _search = new() { PlaceholderText = "Rechercher un logiciel…", Dock = DockStyle.Top, Margin = new Padding(12) };
    private readonly DataGridView _appsGrid = NewGrid();
    private readonly DataGridView _residueGrid = NewGrid();
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(12, 7, 12, 0), Text = "Chargement…" };
    private List<InstalledApp> _apps = [];
    private bool _isBusy;

    public MainForm()
    {
        Text = "ClearTrace — désinstallation transparente";
        MinimumSize = new Size(940, 620);
        Size = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;

        var refresh = new Button { Text = "Actualiser", AutoSize = true };
        var uninstall = new Button { Text = "Désinstaller", AutoSize = true };
        var scan = new Button { Text = "Analyser les traces", AutoSize = true };
        var openLog = new Button { Text = "Ouvrir le journal", AutoSize = true };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(12, 8, 12, 0), FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.AddRange([refresh, uninstall, scan, openLog]);

        var top = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(12) };
        top.Controls.Add(_search);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_appsGrid);
        split.Panel2.Controls.Add(_residueGrid);
        split.Panel2.Controls.Add(new Label { Text = "Traces candidates — affichées pour contrôle, jamais supprimées automatiquement", Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 8, 8, 0), ForeColor = Color.DimGray });
        Controls.AddRange([split, _status, actions, top]);

        refresh.Click += async (_, _) => await LoadAppsAsync();
        _search.TextChanged += (_, _) => BindApps();
        _appsGrid.SelectionChanged += (_, _) => ClearResidues();
        uninstall.Click += (_, _) => UninstallSelected();
        scan.Click += async (_, _) => await ScanSelectedAsync();
        openLog.Click += (_, _) => OpenLog();
        Shown += async (_, _) =>
        {
            // SplitContainer has no usable dimensions during construction. Configure it only after layout.
            split.Panel2MinSize = 160;
            split.SplitterDistance = Math.Clamp(split.Height * 3 / 5, split.Panel1MinSize, split.Height - split.Panel2MinSize - split.SplitterWidth);
            await LoadAppsAsync();
        };
    }

    private static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false,
        BackgroundColor = Color.White, BorderStyle = BorderStyle.None
    };

    private async Task LoadAppsAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        Cursor = Cursors.WaitCursor;
        _status.Text = "Inventaire des logiciels en cours…";
        try
        {
            _apps = (await Task.Run(InstalledApps.Load)).ToList();
            BindApps();
            _status.Text = $"{_apps.Count} logiciels détectés dans le Registre Windows.";
        }
        catch (Exception ex) { _status.Text = $"Échec de l’inventaire : {ex.Message}"; }
        finally { _isBusy = false; Cursor = Cursors.Default; }
    }

    private void BindApps()
    {
        var query = _search.Text.Trim();
        var shown = string.IsNullOrWhiteSpace(query) ? _apps : _apps.Where(x => x.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.DisplayPublisher.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        _appsGrid.DataSource = shown.Select(x => new { x.Name, Version = x.DisplayVersion, Publisher = x.DisplayPublisher, x.InstallLocation, x.UninstallCommand, x.RegistryPath }).ToList();
        ClearResidues();
    }

    private InstalledApp? SelectedApp()
    {
        if (_appsGrid.CurrentRow?.DataBoundItem is null) return null;
        var name = _appsGrid.CurrentRow.Cells["Name"].Value?.ToString();
        var version = _appsGrid.CurrentRow.Cells["Version"].Value?.ToString();
        return _apps.FirstOrDefault(x => x.Name == name && x.DisplayVersion == version);
    }

    private void UninstallSelected()
    {
        var app = SelectedApp();
        if (app is null) { _status.Text = "Sélectionne un logiciel."; return; }
        if (string.IsNullOrWhiteSpace(app.UninstallCommand)) { _status.Text = "Aucune commande de désinstallation disponible pour ce logiciel."; return; }
        var answer = MessageBox.Show($"Lancer la désinstallation Windows de :\n\n{app.Name}\n\nClearTrace n’effacera aucune trace automatiquement.", "Confirmer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        try
        {
            AuditLog.Write("uninstall-launched", app);
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + app.UninstallCommand) { UseShellExecute = true });
            _status.Text = $"Désinstallation lancée : {app.Name}.";
        }
        catch (Exception ex) { _status.Text = $"Impossible de lancer la désinstallation : {ex.Message}"; }
    }

    private async Task ScanSelectedAsync()
    {
        if (_isBusy) return;
        var app = SelectedApp();
        if (app is null) { _status.Text = "Sélectionne un logiciel."; return; }
        _isBusy = true;
        Cursor = Cursors.WaitCursor;
        _status.Text = $"Analyse des traces de {app.Name}…";
        try
        {
            var candidates = await Task.Run(() => ResidueScanner.Scan(app));
            _residueGrid.DataSource = candidates;
            AuditLog.Write("residue-scan", app);
            _status.Text = candidates.Count == 0 ? "Aucune trace candidate trouvée par le scan prudent." : $"{candidates.Count} trace(s) candidate(s) : vérifie-les avant toute action manuelle.";
        }
        catch (Exception ex) { _status.Text = $"Échec de l’analyse : {ex.Message}"; }
        finally { _isBusy = false; Cursor = Cursors.Default; }
    }

    private void ClearResidues() => _residueGrid.DataSource = null;

    private void OpenLog()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AuditLog.PathToLog)!);
        if (!File.Exists(AuditLog.PathToLog)) File.WriteAllText(AuditLog.PathToLog, "");
        Process.Start(new ProcessStartInfo(AuditLog.PathToLog) { UseShellExecute = true });
    }
}
