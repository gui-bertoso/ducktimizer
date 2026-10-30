using Ducktimizer.Catalog;
using Ducktimizer.Models;
using Ducktimizer.Platform;

namespace Ducktimizer.Execution;

public sealed class ActionExecutor(IPcPlatform platform)
{
    public async Task<RunReport> ApplyAsync(SystemSnapshot reviewed, IReadOnlyList<ActionDefinition> catalog,
        RunJournal journal, IProgress<string>? progress = null, CancellationToken stop = default, TermsAcknowledgement? terms = null)
    {
        ActionCatalog.Validate(catalog);
        if (terms is not null && (terms.Version != TermsNotice.Version || terms.Sha256 != TermsNotice.Sha256))
        {
            throw new InvalidOperationException("The acknowledged notice changed. Review the current notice again.");
        }
        MachineUses.Validate(reviewed.IntendedUses);
        if (reviewed.IntendedUses.Count == 0)
        {
            throw new InvalidOperationException("Select the intended uses and analyze this PC again before applying changes.");
        }
        if (catalog.Any(action => MachineUses.ShouldPreserve(action, reviewed.IntendedUses)))
        {
            throw new InvalidOperationException("The selection contains a change protected by the intended uses of this PC.");
        }
        if (reviewed.Inventory.RestartPending ||
            reviewed.Actions.Any(a => a.State == ActionState.RestartRequired))
            throw new InvalidOperationException("Restart Windows and analyze again before applying changes.");
        var allowed = ActionCatalog.Resolve(reviewed).ToDictionary(a => a.Id, StringComparer.Ordinal);
        if (catalog.Any(a => !allowed.TryGetValue(a.Id, out var definition) || definition != a))
            throw new InvalidDataException("The requested changes do not match the reviewed catalog.");
        var report =
            new RunReport { Before = reviewed, Terms = terms, ReviewedActionIds = catalog.Select(a => a.Id).ToList() };
        // Persist the reviewed state before starting any changes. This is an audit record, not rollback data.
        journal.Save(report);
        var pending =
            reviewed.Actions.Where(a => a.State == ActionState.Pending).Select(a => a.Id).ToHashSet();
        foreach (var action in catalog.Where(a => pending.Contains(a.Id)))
        {
            if (stop.IsCancellationRequested)
            {
                report.Interrupted = true;
                break;
            }
            progress?.Report(action.Title);
            try
            {
                var current = await platform.InspectAsync(action);
                if (current.State is ActionState.Failed or ActionState.NotAnalyzed)
                    throw new InvalidOperationException("Reinspection failed: " + current.Detail);
                if (current.State == ActionState.RestartRequired)
                {
                    report.Results.Add(
                        new(action.Id, "RestartRequired", current.Detail, DateTimeOffset.UtcNow));
                    report.RestartRequired = true;
                    report.PausedForRestart = true;
                    journal.Save(report);
                    break;
                }
                if (current.State != ActionState.Pending)
                {
                    report.Results.Add(new(action.Id, "Skipped", current.Detail, DateTimeOffset.UtcNow));
                }
                else
                {
                    report.Results.Add(new(action.Id, "Started",
                        $"Before change: {current.Detail} Final state remains unknown until verification completes.",
                        DateTimeOffset.UtcNow));
                    report.RestartRequired |= action.RestartRequired;
                    journal.Save(report);
                    var applied = await platform.ApplyAsync(action);
                    report.RestartRequired |= applied.RestartRequired;
                    var after = await platform.InspectAsync(action);
                    var success =
                        after.State == ActionState.Configured ||
                        ((action.Kind == ActionKind.Application || action.Kind == ActionKind.StartupEntry) &&
                            after.State == ActionState.NotApplicable);
                    var awaitingRestart = after.State == ActionState.RestartRequired ||
                        (applied.RestartRequired &&
                            (after.State is ActionState.Configured or ActionState.NotApplicable or ActionState.Pending));
                    var detail = applied.RestartRequired
                        ? after.Detail + " Windows servicing reported that a restart is required before continuing."
                        : after.Detail;
                    report.Results.Add(new(action.Id,
                        awaitingRestart ? "RestartRequired"
                        : success ? "Verified"
                                        : "Failed",
                        detail, DateTimeOffset.UtcNow));
                    if (awaitingRestart)
                    {
                        report.RestartRequired = true;
                        report.PausedForRestart = true;
                        journal.Save(report);
                        break;
                    }
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                report.Results.Add(new(action.Id, "Failed",
                    error.Message + " Changes may be partial; no automatic rollback was attempted.",
                    DateTimeOffset.UtcNow));
                // Stop the run after a failure rather than stacking more changes onto an unknown state.
                report.Interrupted = true;
                journal.Save(report);
                break;
            }
            journal.Save(report);
            if (report.Results.Last().Status == "Failed")
            {
                report.Interrupted = true;
                break;
            }
        }
        try
        {
            report.After = (await platform.AnalyzeAsync()) with { IntendedUses = reviewed.IntendedUses.ToList() };
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            report.Results.Add(new(
                "analysis", "Failed", "Post-run analysis failed: " + error.Message, DateTimeOffset.UtcNow));
        }
        report.FinishedAt = DateTimeOffset.UtcNow;
        journal.Save(report);
        return report;
    }
}
