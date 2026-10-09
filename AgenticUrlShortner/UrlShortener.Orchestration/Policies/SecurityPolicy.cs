using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Policies;

// Blocks tasks whose requirement contains injection or XSS patterns.
public sealed class SecurityPolicy : IPolicyGuard
{
    public string Name => "SecurityPolicy";

    private static readonly string[] DangerousPatterns =
        ["<script", "DROP TABLE", "'; --", "exec(", "eval(", "javascript:"];

    public Task<PolicyResult> EvaluateAsync(WorkflowTask task, WorkflowContext context)
    {
        var content = context.Requirement + string.Join(" ", context.Outputs.Values);

        var hit = DangerousPatterns.FirstOrDefault(p =>
            content.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (hit is not null)
            return Task.FromResult(new PolicyResult(
                PolicyAction.Deny,
                Name,
                $"Dangerous pattern detected: '{hit}'. Task blocked for security."));

        return Task.FromResult(new PolicyResult(PolicyAction.Allow, Name, "Security check passed."));
    }
}
