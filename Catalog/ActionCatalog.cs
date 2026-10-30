using System.Text.Json;
using System.Text.Json.Serialization;
using Ducktimizer.Models;
using Ducktimizer.Platform;

namespace Ducktimizer.Catalog;

public static class ActionCatalog
{
    private static readonly CatalogDocument Document = Load();

    public static IReadOnlyList<ActionDefinition> All { get; } = Document.Actions.AsReadOnly();

    internal static StartupRules Startup => Document.Startup;

    private static CatalogDocument Load()
    {
        using var stream = typeof(ActionCatalog).Assembly.GetManifestResourceStream("optimizations.json") ??
                           throw new FileNotFoundException("The optimization table is missing.");

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };

        var document = JsonSerializer.Deserialize<CatalogDocument>(stream, options) ??
                       throw new InvalidDataException("The optimization table is empty.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported optimization-table schema.");
        }

        if (document.Actions.Count == 0 || document.Actions.Any(a => a.Kind == ActionKind.StartupEntry))
        {
            throw new InvalidDataException(
                "Static actions must be present; startup actions are discovered separately.");
        }

        document.Startup.Validate();
        Validate(document.Actions);

        return document;
    }

    public static IReadOnlyList<ActionDefinition> Resolve(SystemSnapshot snapshot)
    {
        var actions = All.Concat(StartupCatalog.FromInventory(snapshot.Inventory)).ToArray();
        Validate(actions);
        return actions;
    }

    public static void Validate() => Validate(All);

    public static void Validate(IReadOnlyList<ActionDefinition> actions)
    {
        if (actions.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != actions.Count)
            throw new InvalidDataException("Duplicate action ID.");
        string[] protectedServices = [
            "WinDefend", "Sense", "SecurityHealthService", "mpssvc", "wuauserv", "BITS", "DoSvc", "Dhcp",
            "Dnscache", "Audiosrv", "AudioEndpointBuilder", "BFE", "WlanSvc", "bthserv", "RpcSs",
            "DcomLaunch", "PlugPlay", "CryptSvc", "EventLog", "Schedule", "Winmgmt"
        ];
        if (actions.Where(a => a.Kind == ActionKind.Registry)
                .Select(a => (a.Target.ToUpperInvariant(), a.ValueName?.ToUpperInvariant()))
                .Distinct()
                .Count() != actions.Count(a => a.Kind == ActionKind.Registry))
            throw new InvalidDataException("Conflicting registry targets.");
        foreach (var a in actions)
        {
            if (!Enum.IsDefined(a.Kind) || string.IsNullOrWhiteSpace(a.Id)
                || string.IsNullOrWhiteSpace(a.Target) || string.IsNullOrWhiteSpace(a.Title)
                || string.IsNullOrWhiteSpace(a.Description) || a.MinimumBuild < 22000)
                throw new InvalidDataException("Invalid action definition: " + a.Id);
            if (string.IsNullOrWhiteSpace(a.Impact) || !Uri.IsWellFormedUriString(a.Source, UriKind.Absolute))
                throw new InvalidDataException(a.Id);
            if (a.Kind == ActionKind.Service &&
                protectedServices.Contains(a.Target, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Protected service: " + a.Target);
            if (a.Kind == ActionKind.ScheduledTask &&
                a.Target is not (
                    @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator" or
                    @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"))
                throw new InvalidDataException("Unreviewed task path: " + a.Target);
            if (a.Kind == ActionKind.StartupEntry && !StartupCatalog.IsKnown(a.Target, a.ValueName))
                throw new InvalidDataException("Unreviewed startup entry: " + a.Target);
            if (a.Kind == ActionKind.OptionalFeature &&
                a.Target is not ("WorkFolders-Client" or "Printing-XPSServices-Features" or "Recall"
                    or "IIS-WebServerRole" or "SMB1Protocol" or "TelnetClient" or "TFTP" or "SimpleTCP"
                    or "MicrosoftWindowsPowerShellV2Root"))
                throw new InvalidDataException("Unreviewed optional feature: " + a.Target);
            MachineUses.Validate(a.PreserveFor ?? []);
            if (a.Kind == ActionKind.Capability &&
                (a.DefaultIncluded || a.Target is not (
                    "App.StepsRecorder~~~~0.0.1.0" or "MathRecognizer~~~~0.0.1.0"
                    or "Print.Fax.Scan~~~~0.0.1.0" or "App.Support.QuickAssist~~~~0.0.1.0"
                    or "Print.Management.Console~~~~0.0.1.0")))
                throw new InvalidDataException("Unreviewed capability removal: " + a.Target);
            if (a.Kind == ActionKind.VisualEffect &&
                (!WindowsNativeSettings.IsSupportedEffect(a.Target) || a.DesiredValue != 0 || a.ValueName is not null))
                throw new InvalidDataException("Unsupported visual-effect definition: " + a.Id);
            if (a.Kind == ActionKind.PowerPlan &&
                (a.Target != WindowsNativeSettings.HighPerformanceScheme || a.DefaultIncluded || a.ValueName is not null))
                throw new InvalidDataException("Unsupported power-plan definition: " + a.Id);
            if (a.Kind == ActionKind.BackgroundApp &&
                (a.DesiredValue != 2 || !a.RequiresPro || a.DefaultIncluded || a.ValueName is not null))
                throw new InvalidDataException("Background restrictions must be explicit, optional per-package choices.");
            if ((a.Kind == ActionKind.Application || a.Kind == ActionKind.BackgroundApp) &&
                (a.Target.IndexOfAny(['*', '?', ';', '\\', '/', '\0']) >= 0 || a.Target.Contains("Xbox", StringComparison.OrdinalIgnoreCase) ||
                    a.Target.Contains("Store", StringComparison.OrdinalIgnoreCase) ||
                    a.Target.Contains("GamingServices", StringComparison.OrdinalIgnoreCase) ||
                    a.Target.Contains("VCLibs", StringComparison.OrdinalIgnoreCase) ||
                    a.Target.Contains("UI.Xaml", StringComparison.OrdinalIgnoreCase) ||
                    a.Target.Contains("NET.Native", StringComparison.OrdinalIgnoreCase) ||
                    a.Target.Contains("WebView", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unsupported package target: " + a.Target);
        }
    }
}

internal sealed class CatalogDocument
{
    public required int SchemaVersion { get; init; }

    public required List<ActionDefinition> Actions { get; init; }

    public required StartupRules Startup { get; init; }
}

internal sealed record StartupRule(
    string ValueName,
    string Title,
    bool DefaultIncluded,
    string[]? PreserveFor = null);

internal sealed class StartupRules
{
    public required List<string> RunKeys { get; init; }

    public required List<StartupRule> Entries { get; init; }

    public required string Description { get; init; }

    public required string Impact { get; init; }

    public required string Source { get; init; }

    public void Validate()
    {
        string[] supportedKeys = [
            @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
        ];

        if (RunKeys.Count == 0 ||
            RunKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != RunKeys.Count ||
            RunKeys.Any(key => !supportedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Unsupported or duplicate startup registry location.");
        }

        if (Entries.Count == 0 ||
            Entries.Any(entry => string.IsNullOrWhiteSpace(entry.ValueName) ||
                                 entry.ValueName.IndexOfAny(['*', '?']) >= 0 ||
                                 string.IsNullOrWhiteSpace(entry.Title)) ||
            Entries.Select(entry => entry.ValueName).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                Entries.Count)
        {
            throw new InvalidDataException("Startup rules need unique, exact registration names and titles.");
        }

        foreach (var rule in Entries)
        {
            MachineUses.Validate(rule.PreserveFor ?? []);
        }

        if (string.IsNullOrWhiteSpace(Description) || !Impact.Contains("{title}", StringComparison.Ordinal) ||
            !Uri.IsWellFormedUriString(Source, UriKind.Absolute))
        {
            throw new InvalidDataException("Startup rules need a description, impact template and source.");
        }
    }
}
