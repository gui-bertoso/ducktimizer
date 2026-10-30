namespace Ducktimizer.Models;

public enum ActionKind
{
    Registry,
    Service,
    Application,
    ScheduledTask,
    StartupEntry,
    OptionalFeature,
    BackgroundApp,
    VisualEffect,
    PowerPlan,
    Capability
}

public enum ActionState
{
    NotAnalyzed,
    Pending,
    Configured,
    NotApplicable,
    Blocked,
    Failed,
    RestartRequired
}

public sealed record ActionDefinition(string Id, string Title, string Category, ActionKind Kind,
    string Target, string? ValueName, int DesiredValue, string Description, string Impact, string Source,
    bool RequiresPro = false, bool RestartRequired = true, bool RequiresEnterprise = false,
    bool DefaultIncluded = true, int MinimumBuild = 22000, int MinimumEdgeMajor = 0, string[]? PreserveFor = null);

public sealed record ActionObservation(string Id, ActionState State, string Detail);
public sealed record SystemSnapshot(DateTimeOffset CapturedAt, string OperatingSystem, int Build,
    string Edition, int RunningServices, int Processes, long TotalMemoryMiB, long UsedMemoryMiB,
    List<ActionObservation> Actions)
{
    public List<string> IntendedUses { get; init; } = [];

    public SystemInventory Inventory { get; init; } = new();

    public List<ActionDefinition> DiscoveredActions { get; init; } = [];

    public double? AverageCpuPercent { get; init; }

    public double? AverageDiskReadBytesPerSecond { get; init; }

    public double? AverageDiskWriteBytesPerSecond { get; init; }

    public int ResourceSampleCount { get; init; }

    public List<string> MeasurementWarnings { get; init; } = [];
}

public sealed record ApplyResult(bool RestartRequired = false);

public sealed record ActionResult(string Id, string Status, string Detail, DateTimeOffset Time);
public sealed class RunReport
{
    public string Version { get; init; } = "5.0.0-preview.2";

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? FinishedAt { get; set; }

    public SystemSnapshot? Before { get; init; }

    public SystemSnapshot? After { get; set; }

    public TermsAcknowledgement? Terms { get; init; }

    public List<string> ReviewedActionIds { get; init; } = [];

    public List<ActionResult> Results { get; init; } = [];

    public bool RestartRequired { get; set; }

    public bool Interrupted { get; set; }

    public bool PausedForRestart { get; set; }
}
