using System.Text.Json;
using Ducktimizer.Catalog;
using Ducktimizer.Execution;
using Ducktimizer.Models;
using Ducktimizer.Platform;

namespace Ducktimizer;

internal static class SelfTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static async Task<int> RunAsync()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "ducktimizer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            ActionCatalog.Validate();
            var first = ActionCatalog.All[0];
            var second = ActionCatalog.All[1];
            var before = Snapshot(new(first.Id, ActionState.Pending, "pending"),
                new(second.Id, ActionState.Configured, "already configured"));
            var path = Path.Combine(temporary, "success.json");
            var fake = new FakePlatform(before, path);
            var report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(fake.Applied.SequenceEqual([first.Id]), "Only reviewed pending actions may execute.");
            Require(
                report.Results.Count(r => r.Status == "Verified") == 1, "Applied action must be verified.");
            Require(report.FinishedAt is not null && File.Exists(path), "Run report must be persisted.");
            Require(fake.SawStartedJournal, "Started state must reach disk before any mutation.");
            var roundTrip =
                JsonSerializer.Deserialize<RunReport>(File.ReadAllText(path), WindowsPlatform.Json);
            Require(roundTrip?.Before?.Actions.Count == 2, "Report must retain the reviewed baseline.");
            Require(roundTrip?.Results.Count == report.Results.Count &&
                        roundTrip.Results.Last().Status == "Verified",
                "Exported reports must reload their complete results.");

            fake.Applied.Clear();
            var after = await fake.AnalyzeAsync();
            await new ActionExecutor(fake).ApplyAsync(
                after, [first, second], new(Path.Combine(temporary, "repeat.json")));
            Require(fake.Applied.Count == 0, "Repeated execution must not reapply configured actions.");

            path = Path.Combine(temporary, "failure.json");
            before = Snapshot(new(first.Id, ActionState.Pending, "pending"),
                new(second.Id, ActionState.Pending, "pending"));
            fake = new(before, path) { FailApply = true };
            report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(report.Interrupted && report.Results.Any(r => r.Status == "Failed"),
                "Failure must be recorded and stop the run.");
            Require(fake.Applied.Count == 1, "Failure must prevent the next mutation.");

            path = Path.Combine(temporary, "verification.json");
            fake = new(before, path) { IgnoreApply = true };
            report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(report.Results.Any(r => r.Status == "Failed") && fake.Applied.Count == 1,
                "An unverified change must not be reported as success or followed by another mutation.");

            path = Path.Combine(temporary, "inspection-failure.json");
            fake = new(before, path) { InspectionOverride = ActionState.Failed };
            report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(report.Interrupted && fake.Applied.Count == 0 &&
                        report.Results.Any(r => r.Status == "Failed"),
                "Failed reinspection must stop the run before mutations.");

            path = Path.Combine(temporary, "stale-review.json");
            fake = new(before, path) { InspectionOverride = ActionState.Configured };
            report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(fake.Applied.Count == 0 && report.Results.All(r => r.Status == "Skipped"),
                "A reviewed action that changed meanwhile must be rechecked and skipped.");

            path = Path.Combine(temporary, "cancel.json");
            fake = new(before, path);
            using var stop = new CancellationTokenSource();
            stop.Cancel();
            report = await new ActionExecutor(fake).ApplyAsync(before, [first], new(path), stop: stop.Token);
            Require(
                report.Interrupted && fake.Applied.Count == 0, "A stop request must prevent new actions.");

            fake = new(before, "unused");
            try
            {
                await new ActionExecutor(fake).ApplyAsync(before, [first], new("\0invalid"));
                throw new InvalidOperationException("Invalid journal unexpectedly succeeded.");
            }
            catch (ArgumentException)
            {
            }
            Require(fake.Applied.Count == 0, "A failed journal must prevent all changes.");

            path = Path.Combine(temporary, "restart.json");
            fake = new(before, path) { InspectionOverride = ActionState.RestartRequired };
            report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(report.PausedForRestart && report.RestartRequired && fake.Applied.Count == 0,
                "A newly detected pending restart must pause before any mutation.");

            var restartSnapshot = before with { Inventory = new() { RestartPending = true } };
            fake = new(restartSnapshot, path);
            var rejected = false;
            try
            {
                await new ActionExecutor(fake).ApplyAsync(restartSnapshot, [first], new(path));
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected && fake.Applied.Count == 0, "A baseline with a pending restart cannot execute.");

            fake = new(before, path);
            rejected = false;
            try
            {
                await new ActionExecutor(fake).ApplyAsync(
                    before, [first with { DesiredValue = 99 }], new(path));
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Require(rejected && fake.Applied.Count == 0,
                "Changed catalog definitions cannot execute under a reviewed action ID.");

            var startup = new SystemInventory
            {
                StartupEntries = [
                new("Steam", @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Current user",
                    "Registry"),
                new("Unknown tool", @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Current user",
                    "Registry")
            ]
            };
            var discovered = StartupCatalog.FromInventory(startup);
            Require(discovered.Count == 1 && discovered[0].ValueName == "Steam",
                "Unknown startup registrations must remain inventory-only.");

            var musicAction = ActionCatalog.All.First(action =>
                action.Kind == ActionKind.Application && action.Target == "Microsoft.ZuneMusic");
            var musicBaseline = Snapshot(new ActionObservation(musicAction.Id, ActionState.Pending, "installed"))
                with { IntendedUses = ["music", "gaming"] };
            fake = new(musicBaseline, path);
            rejected = false;
            try { await new ActionExecutor(fake).ApplyAsync(musicBaseline, [musicAction], new(path)); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && fake.Applied.Count == 0,
                "A multi-select usage protection must prevent mutation in the executor.");

            var protectedRow = new Ducktimizer.ViewModels.ActionRow(musicAction,
                musicBaseline.Actions[0], () => { }, MachineUses.ProtectionReason(musicAction, musicBaseline.IntendedUses));
            protectedRow.Included = true;
            Require(!protectedRow.CanChoose && !protectedRow.Included,
                "Protected changes must remain excluded from review selection.");

            path = Path.Combine(temporary, "servicing-restart.json");
            before = Snapshot(new(first.Id, ActionState.Pending, "pending"), new(second.Id, ActionState.Pending, "pending"));
            fake = new(before, path) { ApplyNeedsRestart = true };
            report = await new ActionExecutor(fake).ApplyAsync(before, [first, second], new(path));
            Require(report.PausedForRestart && fake.Applied.Count == 1,
                "A servicing restart receipt must pause before the next action even when the immediate setting reads as configured.");

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    await new WindowsPlatform().AnalyzeAsync();
                    throw new InvalidOperationException("Non-Windows analysis unexpectedly succeeded.");
                }
                catch (PlatformNotSupportedException)
                {
                }
            }
            Console.WriteLine("PASS: catalog protection, reviewed scope, pre-change journal, verification, " +
                              "report round trip, failed reinspection, stale review, repeat execution, " +
                              "failure stop, cancellation, journal failure and platform guard.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    private static SystemSnapshot Snapshot(params ActionObservation[] actions) => new(
        DateTimeOffset.UtcNow, "Test fixture", 22631, "Professional", 0, 0, 0, 0, actions.ToList())
        { IntendedUses = ["gaming"] };

    private sealed class FakePlatform(SystemSnapshot snapshot, string journal) : IPcPlatform
    {
        private readonly Dictionary<string, ActionObservation> _states =
            snapshot.Actions.ToDictionary(a => a.Id);
        public List<string> Applied { get; } = [];
        public bool SawStartedJournal { get; private set; }
        public bool FailApply { get; init; }
        public bool IgnoreApply { get; init; }
        public bool ApplyNeedsRestart { get; init; }
        public ActionState? InspectionOverride { get; init; }
        public Task<SystemSnapshot> AnalyzeAsync() => Task.FromResult(
            snapshot with { Actions = _states.Values.ToList() });
        public Task<ActionObservation> InspectAsync(ActionDefinition action) => Task.FromResult(
            InspectionOverride is ActionState state
                ? new ActionObservation(action.Id, state, "Reinspection fixture")
                : _states[action.Id]);
        public Task<ApplyResult> ApplyAsync(ActionDefinition action)
        {
            Applied.Add(action.Id);
            SawStartedJournal = File.ReadAllText(journal).Contains("Started");
            if (FailApply)
                throw new IOException("Simulated mutation failure.");
            if (!IgnoreApply)
                _states[action.Id] = new(action.Id, ActionState.Configured, "configured");
            return Task.FromResult(new ApplyResult(ApplyNeedsRestart));
        }
    }
}
