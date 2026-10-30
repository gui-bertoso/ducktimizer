using System.Security.Cryptography;
using System.Text;
using Ducktimizer.Models;

namespace Ducktimizer.Catalog;

public static class StartupCatalog
{
    public static IReadOnlySet<string> RunKeys { get; } =
        new HashSet<string>(ActionCatalog.Startup.RunKeys, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, StartupRule> Rules =
        ActionCatalog.Startup.Entries.ToDictionary(rule => rule.ValueName, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string key, string? name)
    {
        return RunKeys.Contains(key) && name is not null && Rules.ContainsKey(name);
    }

    public static IReadOnlyList<ActionDefinition> FromInventory(SystemInventory inventory)
    {
        return inventory.StartupEntries
            .Where(entry => entry.Kind == "Registry" && IsKnown(entry.Location, entry.Name))
            .DistinctBy(entry => (entry.Location.ToUpperInvariant(), entry.Name.ToUpperInvariant()))
            .Select(CreateAction)
            .ToArray();
    }

    private static ActionDefinition CreateAction(StartupEntry entry)
    {
        var identity = entry.Location.ToUpperInvariant() + "|" + entry.Name.ToUpperInvariant();
        var hash =
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16].ToLowerInvariant();
        var rule = Rules[entry.Name];
        var startup = ActionCatalog.Startup;

        return new ActionDefinition(Id: "startup-" + hash,
            Title: "Stop " + rule.Title + " opening at sign-in", Category: "Startup",
            Kind: ActionKind.StartupEntry, Target: entry.Location, ValueName: entry.Name, DesiredValue: 0,
            Description: startup.Description,
            Impact: startup.Impact.Replace("{title}", rule.Title, StringComparison.Ordinal),
            Source: startup.Source, RestartRequired: false, DefaultIncluded: rule.DefaultIncluded, PreserveFor: rule.PreserveFor);
    }
}
