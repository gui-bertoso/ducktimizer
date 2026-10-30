using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ducktimizer.Platform;
using Ducktimizer.ViewModels;

namespace Ducktimizer.Views;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewModel _workspace = new(new WindowsPlatform());

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _workspace;
        Opened += async (_, _) =>
        {
            if (Environment.GetCommandLineArgs().Contains("--analyze"))
                await _workspace.AnalyzeAsync();
        };
        Closing += (_, e) =>
        {
            if (_workspace.Busy)
                e.Cancel = true;
        };
    }

    private async void Primary_Click(object? sender, RoutedEventArgs e)
    {
        if (_workspace.IsReview)
        {
            await _workspace.ApplyAsync();
            return;
        }
        if (_workspace.CanElevate)
        {
            Elevate_Click(sender, e);
            return;
        }
        await _workspace.AnalyzeAsync();
    }

    private void Preview_Click(object? sender, RoutedEventArgs e) => _workspace.PreviewChanges();

    private void Back_Click(object? sender, RoutedEventArgs e) => _workspace.GoBack();

    private void Stop_Click(object? sender, RoutedEventArgs e) => _workspace.StopAfterCurrent();

    private void Elevate_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (WindowsAccess.RestartElevated())
                Close();
        }
        catch (Exception error)
        {
            _workspace.ShowError("Administrator request canceled or failed", error);
        }
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        var baseline = _workspace.Snapshot;
        if (baseline is null)
            return;
        try
        {
            if (await ExportJsonAsync(baseline, "baseline"))
                _workspace.BaselineExported(baseline);
        }
        catch (Exception error)
        {
            _workspace.ShowError("Baseline export failed", error);
        }
    }

    private async void ExportResults_Click(object? sender, RoutedEventArgs e)
    {
        var report = _workspace.LastReport;
        if (report is null)
            return;
        try
        {
            await ExportJsonAsync(report, "results");
        }
        catch (Exception error)
        {
            _workspace.ShowError("Result export failed", error);
        }
    }

    private async Task<bool> ExportJsonAsync<T>(T payload, string name)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Save " + name,
            SuggestedFileName = $"ducktimizer-{name}-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExtension = "json",
            FileTypeChoices = [new("JSON report") { Patterns = ["*.json"] }]
        });
        if (file is null)
            return false;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await JsonSerializer.SerializeAsync(stream, payload, WindowsPlatform.Json);
        await stream.FlushAsync();
        return true;
    }
}
