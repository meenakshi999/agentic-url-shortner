using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Policies;

public sealed class PolicyEngine
{
    private readonly IReadOnlyList<IPolicyGuard> _guards;

    public PolicyEngine(IEnumerable<IPolicyGuard> guards)
    {
        _guards = guards.ToList();
    }

    public static PolicyEngine Default() => new([
        new SecurityPolicy(),
        new ChangeControlPolicy(),
        new CompliancePolicy()
    ]);

    public async Task<PolicyResult> EvaluateAsync(WorkflowTask task, WorkflowContext context)
    {
        foreach (var guard in _guards)
        {
            var result = await guard.EvaluateAsync(task, context);

            if (!result.IsAllowed)
            {
                context.AddAuditEntry(
                    $"Policy '{result.PolicyName}' blocked task '{task.Id}': {result.Reason}");
                return result;
            }

            context.AddAuditEntry(
                $"Policy '{result.PolicyName}' passed for task '{task.Id}'.");
        }

        return new PolicyResult(PolicyAction.Allow, "PolicyEngine", "All policy checks passed.");
    }
}
