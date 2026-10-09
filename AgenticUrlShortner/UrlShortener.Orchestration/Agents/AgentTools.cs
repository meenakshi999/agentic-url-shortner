namespace UrlShortener.Orchestration.Agents;

/// <summary>
/// Tools available to agents during a ReAct loop.
/// Each tool takes a string argument and returns a string result.
/// </summary>
public static class AgentTools
{
    public static readonly IReadOnlyDictionary<string, Func<string, string>> All =
        new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["estimate_complexity"] = EstimateComplexity,
            ["check_security"]      = CheckSecurity,
            ["query_codebase"]      = QueryCodebase,
            ["list_modules"]        = _ => ListModules(),
            ["assess_risk"]         = AssessRisk,
        };

    public static bool TryParse(string line, out string toolName, out string toolArg)
    {
        // Expected format: TOOL_CALL: tool_name | argument
        toolName = string.Empty;
        toolArg  = string.Empty;

        if (!line.TrimStart().StartsWith("TOOL_CALL:", StringComparison.OrdinalIgnoreCase))
            return false;

        var body = line["TOOL_CALL:".Length..].Trim();
        var sep  = body.IndexOf('|');

        if (sep < 0)
        {
            toolName = body.Trim();
        }
        else
        {
            toolName = body[..sep].Trim();
            toolArg  = body[(sep + 1)..].Trim();
        }

        return !string.IsNullOrWhiteSpace(toolName);
    }

    private static string EstimateComplexity(string feature)
    {
        var lower = feature.ToLowerInvariant();
        if (lower.Contains("auth") || lower.Contains("payment") || lower.Contains("encrypt"))
            return $"COMPLEXITY: HIGH — '{feature}' involves security-sensitive logic. Estimated effort: 5–8 days. Requires security review.";
        if (lower.Contains("analytics") || lower.Contains("report") || lower.Contains("dashboard"))
            return $"COMPLEXITY: MEDIUM — '{feature}' requires aggregation queries and UI. Estimated effort: 3–5 days.";
        return $"COMPLEXITY: LOW — '{feature}' is a standard CRUD feature. Estimated effort: 1–2 days.";
    }

    private static string CheckSecurity(string input)
    {
        var lower = input.ToLowerInvariant();
        var risks = new List<string>();

        if (lower.Contains("sql") || lower.Contains("query") || lower.Contains("exec"))
            risks.Add("SQL injection risk — use parameterised queries only");
        if (lower.Contains("url") || lower.Contains("redirect"))
            risks.Add("Open redirect risk — validate and whitelist redirect targets");
        if (lower.Contains("user") || lower.Contains("auth") || lower.Contains("login"))
            risks.Add("Authentication surface — enforce rate limiting and token expiry");
        if (lower.Contains("api") || lower.Contains("endpoint"))
            risks.Add("API exposure — apply input validation and output encoding");

        return risks.Count > 0
            ? $"SECURITY_RISKS: {string.Join("; ", risks)}"
            : "SECURITY_RISKS: None identified for this scope.";
    }

    private static string QueryCodebase(string module)
    {
        return module.ToLowerInvariant() switch
        {
            var m when m.Contains("domain") =>
                "CODEBASE: Domain layer contains UrlMapping entity (ShortCode, LongUrl, ClickCount, ExpiresAt, CreatedAt). No external dependencies.",
            var m when m.Contains("application") =>
                "CODEBASE: Application layer defines IUrlShortenerService with CreateShortUrl, ResolveUrl, GetAnalytics, DeleteUrl. Uses repository pattern.",
            var m when m.Contains("infrastructure") =>
                "CODEBASE: Infrastructure uses EF Core 10 + SQLite. UrlShortenerDbContext. UrlMappingRepository implements IUrlMappingRepository.",
            var m when m.Contains("api") =>
                "CODEBASE: API layer has UrlsController (CRUD + analytics), WorkflowsController (orchestration), HealthController. Uses Swashbuckle Swagger.",
            var m when m.Contains("orchestration") =>
                "CODEBASE: Orchestration has WorkflowOrchestrator, RollbackService, DynamicReplanner, PolicyEngine, OllamaAgentProvider, DemoAgentProvider.",
            _ =>
                $"CODEBASE: Module '{module}' not found. Available: Domain, Application, Infrastructure, Api, Orchestration."
        };
    }

    private static string ListModules() =>
        "MODULES: UrlShortener.Domain | UrlShortener.Application | UrlShortener.Infrastructure | UrlShortener.Api | UrlShortener.Orchestration";

    private static string AssessRisk(string change)
    {
        var lower = change.ToLowerInvariant();
        if (lower.Contains("schema") || lower.Contains("migration") || lower.Contains("database"))
            return "RISK: HIGH — database schema change. Requires migration script, rollback plan, and data validation.";
        if (lower.Contains("api") || lower.Contains("contract") || lower.Contains("breaking"))
            return "RISK: HIGH — API contract change. May break existing consumers. Version the endpoint.";
        if (lower.Contains("config") || lower.Contains("secret") || lower.Contains("key"))
            return "RISK: MEDIUM — configuration change. Audit secret rotation and environment parity.";
        return "RISK: LOW — isolated change with limited blast radius.";
    }
}
