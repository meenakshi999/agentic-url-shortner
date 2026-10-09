using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Policies;

public interface IPolicyGuard
{
    string Name { get; }
    Task<PolicyResult> EvaluateAsync(WorkflowTask task, WorkflowContext context);
}
