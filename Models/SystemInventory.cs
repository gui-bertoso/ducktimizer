namespace Ducktimizer.Models;

public sealed record ServiceEntry(
    string Name, string DisplayName, string State, string StartMode, int ProcessId, string[] Dependents);
public sealed record TaskEntry(string Path, string Name, string State, bool Enabled);
public sealed record StartupEntry(string Name, string Location, string Scope, string Kind);
public sealed record PackageEntry(
    string Name, string Version, bool NonRemovable, bool IsFramework, bool Provisioned);
public sealed record FeatureEntry(string Name, string State);
public sealed record InstalledProgram(string Name, string Publisher, string Version);
public sealed record DiskEntry(string Drive, long SizeMiB, long FreeMiB);
public sealed record ProcessEntry(string Name, long WorkingSetMiB, int ProcessId = 0)
{
    public string[] Services { get; init; } = [];
}

public sealed record GraphicsEntry(string Name, string DriverVersion, string Manufacturer);

public sealed record PowerEntry(string ActiveScheme, bool? ExternalPower, bool? BatterySaver);

public sealed class SystemInventory
{
    public List<ServiceEntry> Services { get; init; } = [];

    public List<TaskEntry> ScheduledTasks { get; init; } = [];

    public List<StartupEntry> StartupEntries { get; init; } = [];

    public List<PackageEntry> Packages { get; init; } = [];

    public List<FeatureEntry> OptionalFeatures { get; init; } = [];

    public List<FeatureEntry> Capabilities { get; init; } = [];

    public List<InstalledProgram> InstalledPrograms { get; init; } = [];

    public List<DiskEntry> Disks { get; init; } = [];

    public List<ProcessEntry> LargestProcesses { get; init; } = [];

    public List<GraphicsEntry> Graphics { get; init; } = [];

    public PowerEntry? Power { get; set; }

    public List<string> Warnings { get; init; } = [];

    public DateTimeOffset? LastBootTime { get; init; }

    public bool RestartPending { get; init; }

    public string EdgeVersion { get; init; } = "";
}
