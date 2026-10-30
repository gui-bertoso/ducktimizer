namespace Ducktimizer.Models;

public static class MachineUses
{
    public static IReadOnlyDictionary<string, string> All { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gaming"] = "Games",
            ["art"] = "Art, photos and design",
            ["music"] = "Music and audio recording",
            ["streaming"] = "Streaming and video",
            ["work"] = "Work and study",
            ["development"] = "Software development",
            ["printing"] = "Printing and scanning",
            ["remote-access"] = "Remote support and access",
            ["file-sharing"] = "LAN files and media devices",
            ["general"] = "General browsing and daily use"
        };

    public static bool ShouldPreserve(ActionDefinition action, IEnumerable<string> uses) =>
        (action.PreserveFor ?? []).Intersect(uses, StringComparer.Ordinal).Any();

    public static string ProtectionReason(ActionDefinition action, IEnumerable<string> uses)
    {
        var matching = (action.PreserveFor ?? []).Intersect(uses, StringComparer.Ordinal)
            .Select(key => All[key]);
        var labels = string.Join(", ", matching);
        return labels.Length == 0 ? "" : "Kept for your selected uses: " + labels + ".";
    }

    public static void Validate(IEnumerable<string> uses)
    {
        if (uses.Any(key => !All.ContainsKey(key)))
        {
            throw new InvalidDataException("An unknown machine use was selected.");
        }
    }
}
