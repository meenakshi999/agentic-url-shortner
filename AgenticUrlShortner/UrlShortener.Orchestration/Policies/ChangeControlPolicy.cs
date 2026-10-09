using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Policies;

// High-impact stages (implementation, release) require explicit approval.
public sealed class ChangeControlPolicy : IPolicyGuard
{
    public string Name => "ChangeControlPolicy";

    private static readonly WorkflowStage[] HighImpactStages =
        [WorkflowStage.Implementation, WorkflowStage.ReleaseReadiness];

    public Task<PolicyResult> EvaluateAsync(WorkflowTask task, WorkflowContext context)
    {
        if (HighImpactStages.Contains(task.Stage) && !context.ApprovalGranted)
            return Task.FromResult(new PolicyResult(
                PolicyAction.RequireApproval,
                Name,
                $"Stage '{task.Stage}' is high-impact and requires human approval before execution."));

        return Task.FromResult(new PolicyResult(PolicyAction.Allow, Name, "Change control check passed."));
    }
}
