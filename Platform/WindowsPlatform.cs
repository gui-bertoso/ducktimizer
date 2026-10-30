using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ducktimizer.Catalog;
using Ducktimizer.Models;

namespace Ducktimizer.Platform;

public sealed class WindowsPlatform : IPcPlatform
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PowerShellProcess _process = new();
    private static readonly string Operations = ReadOperations();

    private static string ReadOperations()
    {
        var scripts = new List<string>();
        foreach (var name in new[] { "WindowsInventory.ps1", "WindowsOperations.ps1" })
        {
            using var stream = typeof(WindowsPlatform).Assembly.GetManifestResourceStream(name) ??
                               throw new FileNotFoundException(name + " resource is missing.");
            using var reader = new StreamReader(stream);
            scripts.Add(reader.ReadToEnd());
        }
        return string.Join("\n", scripts);
    }

    private Task<string> RunAsync(string operation, IReadOnlyList<ActionDefinition> actions)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { Operation = operation, Actions = actions, StartupRunKeys = ActionCatalog.Startup.RunKeys },
            Json)));
        return _process.RunAsync(
            $"$request = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{payload}')) | ConvertFrom-Json\n" +
                Operations,
            operation is "apply" or "analyze" ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(3));
    }

    public async Task<SystemSnapshot> AnalyzeAsync()
    {
        var shellActions = ActionCatalog.All.Where(action => !WindowsNativeSettings.Handles(action)).ToArray();
        var snapshot = JsonSerializer.Deserialize<SystemSnapshot>(await RunAsync("analyze", shellActions), Json)
            ?? throw new InvalidDataException("Windows returned an empty analysis.");

        try
        {
            snapshot.Inventory.Power = await Task.Run(WindowsNativeSettings.ReadPowerState);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidDataException)
        {
            snapshot.Inventory.Warnings.Add("Power state: " + error.Message);
        }

        var observations = snapshot.Actions.ToList();
        foreach (var action in ActionCatalog.All.Where(WindowsNativeSettings.Handles))
        {
            observations.Add(await Task.Run(() => WindowsNativeSettings.Inspect(action)));
        }

        var discovered = StartupCatalog.FromInventory(snapshot.Inventory);
        if (discovered.Count > 0)
        {
            var startupStates = JsonSerializer.Deserialize<List<ActionObservation>>(
                await RunAsync("inspect-many", discovered), Json)
                ?? throw new InvalidDataException("Windows returned an empty startup analysis.");
            observations.AddRange(startupStates);
        }

        return snapshot with
        {
            Actions = observations,
            DiscoveredActions = discovered.ToList()
        };
    }

    public async Task<ActionObservation> InspectAsync(ActionDefinition action)
    {
        if (WindowsNativeSettings.Handles(action))
        {
            return await Task.Run(() => WindowsNativeSettings.Inspect(action));
        }

        return JsonSerializer.Deserialize<ActionObservation>(await RunAsync("inspect", new[] { action }), Json)
            ?? throw new InvalidDataException("Windows returned an empty action state.");
    }

    public async Task<ApplyResult> ApplyAsync(ActionDefinition action)
    {
        if (WindowsNativeSettings.Handles(action))
        {
            await RunAsync("ensure-ready", Array.Empty<ActionDefinition>());
            await Task.Run(() => WindowsNativeSettings.Apply(action));
            return new ApplyResult();
        }

        var output = await RunAsync("apply", new[] { action });
        if (action.Kind is ActionKind.Capability or ActionKind.OptionalFeature)
        {
            return JsonSerializer.Deserialize<ApplyResult>(output, Json)
                ?? throw new InvalidDataException("Windows returned no servicing result.");
        }

        return new ApplyResult();
    }
}
