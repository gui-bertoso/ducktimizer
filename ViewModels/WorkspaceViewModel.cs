using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ducktimizer.Catalog;
using Ducktimizer.Execution;
using Ducktimizer.Models;
using Ducktimizer.Platform;

namespace Ducktimizer.ViewModels;

public sealed class ActionRow : INotifyPropertyChanged
{
    private bool _included;
    private readonly System.Action _selectionChanged;

    public ActionRow(ActionDefinition action, ActionObservation observation, System.Action selectionChanged, string protectionReason = "")
    {
        Action = action;
        Observation = observation;
        _selectionChanged = selectionChanged;
        ProtectionReason = protectionReason;
        _included = action.DefaultIncluded && CanChoose;
    }

    public ActionDefinition Action { get; }

    public ActionObservation Observation { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => Action.Title;

    public string Category => Action.Category;

    public string Description => Action.Description;

    public string Impact => Action.Impact;

    public string ProtectionReason { get; }

    public bool CanChoose => Observation.State == ActionState.Pending && ProtectionReason.Length == 0;

    public bool Included
    {
        get => _included;
        set
        {
            if (!CanChoose || _included == value)
                return;
            _included = value;
            PropertyChanged?.Invoke(this, new(nameof(Included)));
            PropertyChanged?.Invoke(this, new(nameof(State)));
            _selectionChanged();
        }
    }

    public string State =>
        Observation.State switch
        {
            ActionState.Pending when ProtectionReason.Length > 0 => "Kept for your uses",
            ActionState.Pending => Included ? "Will change" : "Keep unchanged",
            ActionState.NotAnalyzed => "Not analyzed",
            ActionState.Configured => "Configured",
            ActionState.NotApplicable => "Not applicable",
            ActionState.Blocked => "Blocked",
            ActionState.RestartRequired => "Restart needed",
            _ => "Could not verify"
        };

    public string Detail => ProtectionReason.Length == 0
        ? Observation.Detail
        : ProtectionReason + "\n" + Observation.Detail;
}

public sealed class UsageOption : INotifyPropertyChanged
{
    private bool _selected;
    private readonly System.Action _changed;

    public UsageOption(string key, string label, bool selected, System.Action changed)
    {
        Key = key;
        Label = label;
        _selected = selected;
        _changed = changed;
    }

    public string Key { get; }

    public string Label { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            PropertyChanged?.Invoke(this, new(nameof(Selected)));
            _changed();
        }
    }
}

public sealed record RunResultRow(string Title, string Status, string Detail);

public enum WorkspaceScreen
{
    Start,
    Review,
    Results
}

public sealed class WorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IPcPlatform _platform;
    private bool _busy, _reviewed, _baselineSaved;
    private WorkspaceScreen _screen;
    private string _errorMessage = "";
    private string _status =
        OperatingSystem.IsWindows()
            ? "Analyze this PC before making changes."
            : "Windows 11 is required. This is a catalog preview; this computer will not be changed.";
    private string _summary = "No analysis yet";
    private string _machine = "No system measurements have been collected.";
    private SystemSnapshot? _snapshot;
    private readonly List<ActionRow> _allRows = [];
    private IReadOnlyList<ActionDefinition> _catalog = ActionCatalog.All;
    private CancellationTokenSource? _stop;

    public WorkspaceViewModel(IPcPlatform platform)
    {
        _platform = platform;
        ActionCatalog.Validate();
        foreach (var use in MachineUses.All)
        {
            Uses.Add(new UsageOption(use.Key, use.Value, use.Key == "gaming", UsageChanged));
        }
        ShowRows(null);
    }

    public ObservableCollection<UsageOption> Uses { get; } = [];

    public bool HasSelectedUses => Uses.Any(use => use.Selected);

    public string UsageSelectionHint => HasSelectedUses
        ? "Select all that apply. Nothing changes during analysis."
        : "Choose at least one use before analyzing.";

    public string UsageSummary => "Selected uses: " + string.Join(", ", Uses.Where(use => use.Selected).Select(use => use.Label));

    public ObservableCollection<ActionRow> Actions { get; } = [];

    public ObservableCollection<RunResultRow> Results { get; } = [];

    public string ResultSummary =>
        LastReport is null
            ? "No run has been performed."
            : $"Run started {LastReport.StartedAt.LocalDateTime:g}. This record does not provide automatic restoration.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Busy
    {
        get => _busy;

        private set
        {
            _busy = value;
            Changed();
            RefreshControls();
            RefreshScreen();
        }
    }

    public bool Reviewed
    {
        get => _reviewed;

        set
        {
            _reviewed = value;
            Changed();
            RefreshControls();
        }
    }

    public string TermsText => TermsNotice.Text;

    public bool CanAnalyze => !Busy && HasSelectedUses && OperatingSystem.IsWindows();

    public bool CanExportResults => !Busy && LastReport is not null;

    public bool CanExport => !Busy && _snapshot is not null;

    public bool CanApply => !Busy && Reviewed && _baselineSaved && WindowsAccess.IsAdministrator &&
                            _snapshot is not null && !_snapshot.Inventory.RestartPending &&
                            !_snapshot.Actions.Any(a => a.State == ActionState.RestartRequired) &&
                            _allRows.Any(row => row.Included);

    public bool CanReview => !Busy && _snapshot is not null;

    public bool CanElevate => !Busy && OperatingSystem.IsWindows() && !WindowsAccess.IsAdministrator;

    public bool CanStop => Busy && _stop is not null;

    public bool IsStart => !Busy && _screen == WorkspaceScreen.Start;

    public bool IsReview => !Busy && _screen == WorkspaceScreen.Review;

    public bool IsResults => !Busy && _screen == WorkspaceScreen.Results;

    public bool IsPreview => !OperatingSystem.IsWindows();

    public bool CanGoBack => !Busy && _screen == WorkspaceScreen.Review;

    public string StepLabel =>
        _screen switch
        {
            WorkspaceScreen.Review => "REVIEW",
            WorkspaceScreen.Results => "RESULTS",
            _ => "GET STARTED"
        };

    public string WorkingTitle => _stop is null ? "Analyzing your PC" : "Applying changes";

    public string PrimaryLabel => _screen switch
    {
        WorkspaceScreen.Review => "Apply changes",
        WorkspaceScreen.Results => "Analyze again",
        _ => "Analyze PC"
    };

    public bool CanPrimary => _screen == WorkspaceScreen.Review ? CanApply : CanAnalyze;

    public string ErrorMessage
    {
        get => _errorMessage;

        private set
        {
            _errorMessage = value;
            Changed();
            Changed(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string ReviewTitle => _snapshot is null
                                     ? "What Ducktimizer changes"
                                     : $"{_allRows.Count(row => row.Included)} changes to review";

    public string InventorySummary =>
        _snapshot is null
            ? "No inventory collected."
            : $"{_snapshot.Inventory.Services.Count} services · {_snapshot.Inventory.ScheduledTasks.Count} tasks · {_snapshot.Inventory.StartupEntries.Count} startup entries\n" +
                  $"{_snapshot.Inventory.Packages.Count} app packages · {_snapshot.Inventory.OptionalFeatures.Count} optional features · {_snapshot.Inventory.Capabilities.Count} capabilities\n" +
                  $"{_snapshot.Inventory.InstalledPrograms.Count} desktop-program entries · {_snapshot.Inventory.Warnings.Count} inventory warnings";

    public string GraphicsSummary => _snapshot is null || _snapshot.Inventory.Graphics.Count == 0
        ? "Graphics adapter details unavailable."
        : string.Join("\n", _snapshot.Inventory.Graphics.Select(gpu =>
            $"{gpu.Name} · driver {gpu.DriverVersion}"));

    public string PowerSummary
    {
        get
        {
            var power = _snapshot?.Inventory.Power;
            if (power is null)
            {
                return "Power source and scheme unavailable.";
            }

            var source = power.ExternalPower switch
            {
                true => "External power",
                false => "Battery",
                _ => "Unknown power source"
            };
            return $"{source} · active scheme {power.ActiveScheme}";
        }
    }

    public string ResourceSummary => _snapshot is null
        ? "No process measurements collected."
        : "Largest working sets (shared pages may be counted more than once):\n" +
            string.Join("\n", _snapshot.Inventory.LargestProcesses.Take(5).Select(process =>
                $"{process.Name} [{process.ProcessId}] · {process.WorkingSetMiB:N0} MiB" +
                (process.Services.Length == 0 ? "" :
                    $" · {(process.Services.Length > 1 ? "shared service host: " : "service: ")}{string.Join(", ", process.Services)}")));

    public string InventoryWarnings =>
        _snapshot is null ? "" : string.Join("\n", _snapshot.Inventory.Warnings);

    public string ReviewSubtitle =>
        _snapshot is null ? "Open an item to see its effect. Analysis is available on Windows 11."
        : _snapshot.Inventory.RestartPending ||
                _snapshot.Actions.Any(a => a.State == ActionState.RestartRequired)
            ? "Windows needs a restart before changes can be applied. Your baseline is saved."
            : "Open an item to see its effect or choose to keep it. Your baseline is saved locally.";

    public string ResultsTitle =>
        LastReport is null ? "No changes applied"
        : LastReport.PausedForRestart ? "Restart to continue"
        : LastReport.Interrupted || LastReport.Results.Any(r => r.Status == "Failed") ? "Finished with issues"
                                                                                      : "Changes applied";

    public string ResultsSubtitle =>
        LastReport is null
            ? "Analyze your PC to get started."
            : $"{LastReport.Results.Count(r => r.Status == "Verified")} completed · {LastReport.Results.Count(r => r.Status == "Skipped")} skipped · {LastReport.Results.Count(r => r.Status == "Failed")} failed · {LastReport.Results.Count(r => r.Status == "RestartRequired")} waiting for restart";

    public string RestartMessage =>
        LastReport?.RestartRequired == true ? "Restart Windows before testing performance again." : "";

    public void PreviewChanges()
    {
        _screen = WorkspaceScreen.Review;
        ErrorMessage = "";
        RefreshScreen();
    }

    public void GoBack()
    {
        if (Busy)
            return;
        Reviewed = false;
        _screen = WorkspaceScreen.Start;
        RefreshScreen();
    }

    private void RefreshScreen()
    {
        foreach (var name in new[] { nameof(IsStart), nameof(IsReview), nameof(IsResults), nameof(CanGoBack),
                     nameof(StepLabel), nameof(WorkingTitle), nameof(PrimaryLabel), nameof(CanPrimary),
                     nameof(ReviewTitle), nameof(ReviewSubtitle), nameof(ResultsTitle),
                     nameof(ResultsSubtitle), nameof(RestartMessage), nameof(InventorySummary),
                     nameof(InventoryWarnings), nameof(GraphicsSummary), nameof(PowerSummary), nameof(ResourceSummary) })
            Changed(name);
    }

    public string Status
    {
        get => _status;

        private set
        {
            _status = value;
            Changed();
        }
    }

    public string Summary
    {
        get => _summary;

        private set
        {
            _summary = value;
            Changed();
        }
    }

    public string Machine
    {
        get => _machine;

        private set
        {
            _machine = value;
            Changed();
        }
    }

    public SystemSnapshot? Snapshot => _snapshot;

    public RunReport? LastReport { get; private set; }

    public async Task AnalyzeAsync()
    {
        if (!CanAnalyze)
            return;
        Busy = true;
        Reviewed = false;
        _baselineSaved = false;
        ErrorMessage = "";
        try
        {
            Status =
                "Reading Windows settings, services and installed packages. No changes are being applied.";
            var intendedUses = Uses.Where(use => use.Selected).Select(use => use.Key).ToList();
            _snapshot = (await _platform.AnalyzeAsync()) with { IntendedUses = intendedUses };
            ShowRows(_snapshot);
            var baselineFolder =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Ducktimizer", "baselines");
            var baselinePath = Path.Combine(
                baselineFolder, $"baseline-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            new RunJournal(baselinePath).SaveSnapshot(_snapshot);
            _baselineSaved = true;
            _screen = WorkspaceScreen.Review;
            Status = "Analysis complete. Baseline saved.";
        }
        catch (Exception error)
        {
            _snapshot = null;
            ShowRows(null);
            _screen = WorkspaceScreen.Start;
            ShowError("Analysis failed", error);
        }
        finally
        {
            Busy = false;
        }
    }

    public void ShowError(string context, Exception error)
    {
        ErrorMessage = context + ": " + error.Message;
        Status = ErrorMessage;
        AppLogger.Error(error, context);
    }

    public void BaselineExported(SystemSnapshot exported)
    {
        if (!ReferenceEquals(exported, _snapshot))
        {
            Status = "The analysis changed during export. Export the current baseline again.";
            return;
        }
        _baselineSaved = true;
        Status = "Baseline exported. Record the game benchmark before applying changes.";
        RefreshControls();
    }

    public void StopAfterCurrent()
    {
        _stop?.Cancel();
        Status = "Stop requested. Waiting for the current action to finish and be verified.";
    }

    public async Task ApplyAsync()
    {
        if (!CanApply || _snapshot is null)
            return;
        ErrorMessage = "";
        Busy = true;
        _stop = new();
        RefreshControls();
        RefreshScreen();
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ducktimizer", "runs");
        var path =
            Path.Combine(folder, $"run-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        try
        {
            Directory.CreateDirectory(folder);
            using var operationLock = new FileStream(Path.Combine(folder, "apply.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var progress = new Progress<string>(title =>
                                                {
                                                    if (Busy)
                                                        Status = "Applying: " + title;
                                                });
            LastReport = await new ActionExecutor(_platform).ApplyAsync(_snapshot,
                _allRows.Where(row => row.Included).Select(row => row.Action).ToArray(), new RunJournal(path),
                progress, _stop.Token, terms: TermsNotice.Acknowledge());
            Results.Clear();
            foreach (var result in LastReport.Results.GroupBy(r => r.Id).Select(g => g.Last()))
                Results.Add(new(_catalog.FirstOrDefault(a => a.Id == result.Id)?.Title ?? result.Id,
                    result.Status, result.Detail));
            Changed(nameof(ResultSummary));
            _screen = WorkspaceScreen.Results;
            _snapshot = LastReport.After;
            ShowRows(_snapshot);
            var verified = LastReport.Results.Count(r => r.Status == "Verified");
            var failed = LastReport.Results.Count(r => r.Status == "Failed");
            Status = $"{verified} verified; {failed} failed. " +
                     (LastReport.Interrupted ? "Run stopped. " : "") +
                     (LastReport.RestartRequired ? "Restart Windows before measuring again. " : "") +
                     $"Report: {path}";
        }
        catch (Exception error)
        {
            _screen = WorkspaceScreen.Review;
            ShowError("Run could not complete", error);
        }
        finally
        {
            _stop.Dispose();
            _stop = null;
            Reviewed = false;
            _baselineSaved = false;
            Busy = false;
        }
    }

    private void ShowRows(SystemSnapshot? snapshot)
    {
        _allRows.Clear();
        _catalog = snapshot is null ? ActionCatalog.All : ActionCatalog.Resolve(snapshot);
        var intendedUses = snapshot?.IntendedUses
            ?? Uses.Where(use => use.Selected).Select(use => use.Key).ToList();
        foreach (var a in _catalog)
            _allRows.Add(new(a,
                snapshot?.Actions.FirstOrDefault(o => o.Id == a.Id) ??
                    new(a.Id, ActionState.NotAnalyzed,
                        "Analyze this PC to determine whether this action applies."),
                SelectionChanged, MachineUses.ProtectionReason(a, intendedUses)));
        Actions.Clear();
        foreach (var row in _allRows)
            Actions.Add(row);
        Summary =
            snapshot is null
                ? $"{_allRows.Count} actions in the initial catalog"
                : $"{snapshot.Actions.Count(a => a.State == ActionState.Pending)} changes · {snapshot.Actions.Count(a => a.State == ActionState.Configured)} configured · {snapshot.Actions.Count(a => a.State is ActionState.Blocked or ActionState.Failed)} need attention";
        var performance =
            snapshot is null
                ? ""
                : $"\nCPU: {(snapshot.AverageCpuPercent is double cpu ? cpu.ToString("N1") + "%" : "unavailable")} · disk read/write: " +
                      $"{(snapshot.AverageDiskReadBytesPerSecond is double read ? (read / 1048576).ToString("N2") : "—")} / " +
                      $"{(snapshot.AverageDiskWriteBytesPerSecond is double write ? (write / 1048576).ToString("N2") : "—")} MiB/s · {snapshot.ResourceSampleCount} CPU samples";
        Machine =
            snapshot is null
                ? "No system measurements have been collected."
                : $"{snapshot.OperatingSystem} · build {snapshot.Build} · {snapshot.Edition}\n{snapshot.RunningServices} running services · {snapshot.Processes} processes · {snapshot.UsedMemoryMiB:N0} / {snapshot.TotalMemoryMiB:N0} MiB RAM in use" +
                      performance;
    }

    private void UsageChanged()
    {
        _snapshot = null;
        _baselineSaved = false;
        Reviewed = false;
        ShowRows(null);
        Changed(nameof(HasSelectedUses));
        Changed(nameof(UsageSelectionHint));
        Changed(nameof(UsageSummary));
        RefreshScreen();
        RefreshControls();
    }

    private void SelectionChanged()
    {
        Reviewed = false;
        Changed(nameof(ReviewTitle));
        RefreshControls();
    }

    private void RefreshControls()
    {
        foreach (var name in new[] { nameof(CanAnalyze), nameof(CanExport), nameof(CanExportResults),
                     nameof(CanApply), nameof(CanReview), nameof(CanElevate), nameof(CanStop),
                     nameof(CanPrimary) })
            Changed(name);
    }

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
