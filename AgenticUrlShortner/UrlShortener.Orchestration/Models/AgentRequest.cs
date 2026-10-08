namespace UrlShortener.Orchestration.Models;

public sealed record AgentRequest(
    string TaskName,
    string Requirement,
    IReadOnlyDictionary<string, string> PreviousOutputs,
    WorkflowScenario Scenario,
    bool SimulateFailure = false);