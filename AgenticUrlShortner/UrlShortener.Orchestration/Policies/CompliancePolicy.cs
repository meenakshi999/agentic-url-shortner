using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Policies;

// Flags PII or sensitive data patterns in requirement text.
public sealed class CompliancePolicy : IPolicyGuard
{
    public string Name => "CompliancePolicy";

    private static readonly string[] SensitivePatterns =
        ["ssn", "social security", "credit card", "password", "api_key", "secret"];

    public Task<PolicyResult> EvaluateAsync(WorkflowTask task, WorkflowContext context)
    {
        var content = context.Requirement.ToLowerInvariant();

        var hit = SensitivePatterns.FirstOrDefault(p => content.Contains(p));

        if (hit is not null)
            return Task.FromResult(new PolicyResult(
                PolicyAction.Deny,
                Name,
                $"Compliance violation: sensitive data pattern '{hit}' found in requirement."));

        return Task.FromResult(new PolicyResult(PolicyAction.Allow, Name, "Compliance check passed."));
    }
}
