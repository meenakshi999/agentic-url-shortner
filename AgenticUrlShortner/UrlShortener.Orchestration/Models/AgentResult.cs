namespace UrlShortener.Orchestration.Models;

public sealed record AgentResult(
    bool Success,
    string Output,
    string? Error = null);