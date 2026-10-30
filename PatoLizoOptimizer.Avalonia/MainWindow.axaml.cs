using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PatoLizoOptimizer;

public partial class MainWindow : Window
{
    private bool _busy;
    public MainWindow()
    {
        InitializeComponent();
        UpdateState();
        Opened += async (_, _) => await EnsureTerms();
    }

    private void Preset_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (PresetBox is null) return;
        var level = PresetBox.SelectedIndex;
        var debloatOnly = level == 5;
        var optimized = level == 4;
        VisualSwitch.IsChecked = !debloatOnly;
        GamingSwitch.IsChecked = !debloatOnly && level >= 1;
        PowerSwitch.IsChecked = !debloatOnly && level >= 1;
        PrivacySwitch.IsChecked = level >= 2;
        DrySwitch.IsChecked = !debloatOnly && level >= 3;
        SearchSwitch.IsChecked = level >= 2;
        DebloatSwitch.IsChecked = debloatOnly || level == 3;
        BackgroundSwitch.IsChecked = debloatOnly || level == 3 || optimized;
    }

    private async void Activate_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var groups = SelectedGroups();
        if (groups.Count == 0) return;
        var warning = groups.Contains("debloat")
            ? "\n\n⚠ EXTREME DEBLOAT: selected apps will be uninstalled for all users. This step may be IRREVERSIBLE; the backup does not guarantee that removed components can be reinstalled."
            : "";
        if (!await Confirm("Enable optimizations", $"Apply {groups.Count} selected groups?\n\nThe app will attempt to create a restore point and save the current settings.{warning}")) return;
        SetBusy(true);
        try { if (await RunOperation("--apply", string.Join(',', groups))) UpdateState(); }
        finally { SetBusy(false); }
    }

    private async void Deactivate_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!OptimizerEngine.HasActiveBackups()) return;
        if (!await Confirm("Restore optimizations", "Restore all Ducktimizer runs and return to the previous state?")) return;
        SetBusy(true);
        try
        {
            if (await RunOperation("--undo-all"))
            {
                VisualSwitch.IsChecked = GamingSwitch.IsChecked = PowerSwitch.IsChecked = false;
                PrivacySwitch.IsChecked = DrySwitch.IsChecked = SearchSwitch.IsChecked = false;
                DebloatSwitch.IsChecked = BackgroundSwitch.IsChecked = false;
                UpdateState();
            }
        }
        finally { SetBusy(false); }
    }

    private List<string> SelectedGroups()
    {
        var result = new List<string>();
        if (VisualSwitch.IsChecked == true) result.Add("visual");
        if (GamingSwitch.IsChecked == true) result.Add("gaming");
        if (PowerSwitch.IsChecked == true) result.Add("power");
        if (PrivacySwitch.IsChecked == true) result.Add("privacy");
        if (DrySwitch.IsChecked == true) result.Add("dry");
        if (SearchSwitch.IsChecked == true) result.Add("search");
        if (DebloatSwitch.IsChecked == true) result.Add("debloat");
        if (BackgroundSwitch.IsChecked == true) result.Add("background");
        return result;
    }

    private async Task<bool> RunOperation(params string[] args)
    {
        var progress = new Progress<OperationProgress>(value =>
        {
            ApplyProgress.Value = value.Percent;
            StateTitle.Text = value.Message;
            StateDetail.Text = $"{value.Percent}% complete — do not close the application";
        });
        var ok = await Task.Run(() => OptimizerEngine.RunElevated(args, progress) == 0);
        if (!ok && !string.IsNullOrWhiteSpace(OptimizerEngine.LastError))
        {
            StateTitle.Text = "Could not complete";
            StateDetail.Text = OptimizerEngine.LastError;
        }
        return ok;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ActivateButton.IsEnabled = DeactivateButton.IsEnabled = !busy;
        ApplyProgress.IsVisible = busy;
        if (busy) ApplyProgress.Value = 0;
        if (busy)
        {
            StateTitle.Text = "Applying changes…";
            StateDetail.Text = "Do not close Ducktimizer during this step";
        }
        else UpdateState();
    }

    private void UpdateState()
    {
        var active = OptimizerEngine.HasActiveBackups();
        DeactivateButton.IsEnabled = active && !_busy;
        StateTitle.Text = active ? "Optimizations active" : "System at baseline";
        StateDetail.Text = active ? "Use Restore to undo all runs" : "Select groups and apply them whenever you are ready";
    }

    private async Task<bool> Confirm(string title, string message)
    {
        var result = false;
        var dialog = new Window { Title = title, Width = 430, Height = 230, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Avalonia.Media.Brush.Parse("#202429") };
        var yes = new Button { Content = "Confirm", Classes = { "primary" }, MinWidth = 110 };
        var no = new Button { Content = "Cancel", MinWidth = 100 };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 18, Children = {
            new TextBlock { Text = title, FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold },
            new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brush.Parse("#B7C4CA") },
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { no, yes } }
        }};
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task EnsureTerms()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ducktimizer");
        var acceptedFile = Path.Combine(folder, "offline-v4-risk-notice.accepted");
        if (File.Exists(acceptedFile)) return;
        var accepted = false;
        var dialog = new Window { Title = "Before optimizing", Width = 620, Height = 470, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Avalonia.Media.Brush.Parse("#202429") };
        var check = new CheckBox { Content = "I have read and understood the risk notice." };
        var accept = new Button { Content = "Understood, continue", Classes = { "primary" }, IsEnabled = false };
        var exit = new Button { Content = "Exit" };
        check.IsCheckedChanged += (_, _) => accept.IsEnabled = check.IsChecked == true;
        accept.Click += (_, _) => { accepted = true; dialog.Close(); };
        exit.Click += (_, _) => dialog.Close();
        var heading = new TextBlock { Text = "Before optimizing", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold };
        var termsText = "DUCKTIMIZER — AVISO DE RISCO — v4.0.0\n\n" +
            "1. PURPOSE. Ducktimizer changes Windows settings to reduce background activity, visual effects, telemetry and preinstalled applications. No specific FPS, latency or performance improvement is guaranteed.\n\n" +
            "2. ADMINISTRATOR ACCESS. The program requires administrator privileges at startup. Accepting the Windows prompt authorizes changes to the Registry, local policies, power plan and application packages on this computer.\n\n" +
            "3. AGGRESSIVE MODES. Minimal Windows disables font smoothing, shadows, transparency and effects for a minimal interface. Streamlined Search removes Bing, highlights and suggestions. Extreme Debloat uninstalls packaged applications and removes provisioning. Edge, Microsoft Store, Windows Update, Defender, Firewall and critical services are not removed.\n\n" +
            "4. BACKUP AND RESTORATION. The program attempts to create a restore point and records previous settings. Registry and power restoration is automated. Reinstallation of removed applications is not guaranteed, because it depends on packages remaining available in Windows or Microsoft Store.\n\n" +
            "5. POSSIBLE EFFECTS. Changes can affect appearance, notifications, search, AI features, synchronization, power consumption, application compatibility and behavior after Windows updates. Some changes require a restart.\n\n" +
            "6. USER RESPONSIBILITY. Only use computers you own or are authorized to manage. Save important files and review selected groups before confirming. You are responsible for selecting presets and the consequences of their changes.\n\n" +
            "7. PRIVACY. The application runs locally without accounts, activation, device identifiers, telemetry or automatic server requests. Logs and backups remain on this computer. Updates are installed manually.\n\n" +
            "8. OPEN SOURCE. Ducktimizer code is available under the MIT License. Third-party dependencies and materials retain their own licenses. See LICENSE.";
        var terms = new ScrollViewer { Margin = new Avalonia.Thickness(0,16), Content = new TextBlock { Text = termsText, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brush.Parse("#BAC7CD") } };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Avalonia.Thickness(0,16,0,0), Children = { exit, accept } };
        Grid.SetRow(terms, 1); Grid.SetRow(check, 2); Grid.SetRow(buttons, 3);
        dialog.Content = new Grid { Margin = new Avalonia.Thickness(26), RowDefinitions = RowDefinitions.Parse("Auto,*,Auto,Auto"), Children = { heading, terms, check, buttons } };
        await dialog.ShowDialog(this);
        if (!accepted) { Close(); return; }
        Directory.CreateDirectory(folder);
        File.WriteAllText(acceptedFile, "acknowledged-offline-v4");
    }
}
