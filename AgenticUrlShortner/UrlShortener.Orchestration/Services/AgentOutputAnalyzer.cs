using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public enum RoutingDecision { Continue, RequireApproval, Block }

public sealed record RoutingResult(RoutingDecision Decision, string Reason);

/// <summary>
/// Scans agent output text for risk signals and returns a routing decision.
/// Allows the orchestrator to autonomously pause or block based on what the LLM surfaces.
/// </summary>
public sealed class AgentOutputAnalyzer
{
    private static readonly string[] BlockSignals =
    [
        "SECURITY_RISK: CRITICAL",
        "BLOCK:",
        "DO NOT PROCEED",
        "HALT WORKFLOW",
        "CRITICAL VULNERABILITY",
        "PII DETECTED",
        "COMPLIANCE VIOLATION"
    ];

    private static readonly string[] ApprovalSignals =
    [
        "RISK: HIGH",
        "SECURITY_RISKS:",
        "BREAKING CHANGE",
        "REQUIRES APPROVAL",
        "REQUIRES REVIEW",
        "HIGH RISK",
        "SCHEMA CHANGE",
        "DATABASE MIGRATION",
        "API CONTRACT CHANGE"
    ];

    public RoutingResult Analyze(string taskId, string output, WorkflowContext context)
    {
        if (string.IsNullOrWhiteSpace(output))
            return new RoutingResult(RoutingDecision.Continue, "Empty output — continuing.");

        var upper = output.ToUpperInvariant();

        foreach (var signal in BlockSignals)
        {
            if (upper.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                var reason = $"Agent output for '{taskId}' triggered block signal: '{signal}'.";
                context.AddAuditEntry($"[AgentOutputAnalyzer] BLOCK — {reason}");
                return new RoutingResult(RoutingDecision.Block, reason);
            }
        }

        foreach (var signal in ApprovalSignals)
        {
            if (upper.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                var reason = $"Agent output for '{taskId}' surfaced risk signal: '{signal}'. Pausing for human review.";
                context.AddAuditEntry($"[AgentOutputAnalyzer] APPROVAL REQUIRED — {reason}");
                return new RoutingResult(RoutingDecision.RequireApproval, reason);
            }
        }

        return new RoutingResult(RoutingDecision.Continue, "No risk signals detected.");
    }
}
