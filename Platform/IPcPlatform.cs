using Ducktimizer.Models;

namespace Ducktimizer.Platform;

public interface IPcPlatform
{
    Task<SystemSnapshot> AnalyzeAsync();
    Task<ActionObservation> InspectAsync(ActionDefinition action);
    Task<ApplyResult> ApplyAsync(ActionDefinition action);
}
