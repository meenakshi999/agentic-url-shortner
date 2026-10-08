using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Agents;

public sealed class DemoAgentProvider : IAgentProvider
{
    public Task<AgentResult> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SimulateFailure &&
            request.TaskName == "Execute unit and integration validation")
        {
            return Task.FromResult(
                new AgentResult(
                    Success: false,
                    Output: string.Empty,
                    Error: "Simulated validation failure for resilience testing."));
        }

        var output = request.TaskName switch
        {
            "Analyze and normalize requirement" =>
                BuildRequirementOutput(request.Scenario),

            "Identify ambiguity, assumptions and required clarifications" =>
                "Ambiguity identified: the security scope is not explicitly defined. " +
                "Potential concerns include authentication, authorization, rate limiting, " +
                "malicious URL handling, audit logging and abuse prevention. " +
                "High-impact assumptions should be approved before implementation.",

            "Analyze existing codebase and impacted modules" =>
                "Brownfield analysis completed. Impacted areas include the URL domain model, " +
                "persistence schema, create/resolve APIs, application services and automated tests. " +
                "Existing API behavior should remain backward compatible.",

            "Decompose requirement into engineering tasks" =>
                BuildPlanningOutput(request.Scenario),

            "Produce architecture and design decisions" =>
                BuildArchitectureOutput(request.Scenario),

            "Implement required changes" =>
                "Implementation completed and aligned with the approved architecture and dependency plan.",

            "Generate engineering documentation" =>
                "Documentation prepared covering architecture, API behavior, orchestration, " +
                "assumptions, risks and operational considerations.",

            "Execute unit and integration validation" =>
                "Validation completed: functional, integration and reliability checks passed.",

            "Validate outputs, risks and quality gates" =>
                "Validation gate passed. Risks, assumptions, required engineering outputs and " +
                "quality gates were reviewed.",

            "Human approval before release" =>
                "Awaiting explicit human approval before release.",

            "Verify release readiness" =>
                "Release readiness verified: implementation, tests, documentation and validation are complete.",

            _ =>
                $"Completed task: {request.TaskName}"
        };

        return Task.FromResult(
            new AgentResult(
                Success: true,
                Output: output));
    }

    private static string BuildRequirementOutput(
        WorkflowScenario scenario)
    {
        return scenario switch
        {
            WorkflowScenario.Brownfield =>
                "Brownfield requirement normalized: extend an existing URL shortener " +
                "while preserving existing behavior and identifying impacted modules, " +
                "data flows and compatibility constraints.",

            WorkflowScenario.Ambiguous =>
                "Ambiguous requirement detected. The requested change requires clarification " +
                "of scope, assumptions, acceptance criteria and risk boundaries before high-impact changes.",

            _ =>
                "Requirement normalized: build a URL shortener with creation, resolution, " +
                "analytics and reliability capabilities."
        };
    }

    private static string BuildPlanningOutput(
        WorkflowScenario scenario)
    {
        return scenario switch
        {
            WorkflowScenario.Brownfield =>
                "Brownfield plan: analyze existing modules, identify impacted APIs and data flows, " +
                "preserve backward compatibility, implement the change, update tests and documentation, " +
                "then validate regression risk.",

            WorkflowScenario.Ambiguous =>
                "Ambiguous requirement plan: identify missing acceptance criteria, document assumptions, " +
                "assess security and operational risks, establish validation criteria and require governed " +
                "approval before release.",

            _ =>
                "Tasks identified: API design, persistence, URL generation, expiration, analytics, " +
                "validation, testing and documentation."
        };
    }

    private static string BuildArchitectureOutput(
        WorkflowScenario scenario)
    {
        return scenario switch
        {
            WorkflowScenario.Brownfield =>
                "Brownfield architecture: preserve existing service boundaries and API contracts where possible. " +
                "Apply backward-compatible persistence and service changes while protecting existing consumers.",

            WorkflowScenario.Ambiguous =>
                "Architecture approach: use defense-in-depth with explicit security boundaries, " +
                "input validation, least privilege, abuse controls, auditability and governed change approval.",

            _ =>
                "Architecture: ASP.NET Core API, application services, domain model, EF Core persistence " +
                "and governed agentic orchestration."
        };
    }
}