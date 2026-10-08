namespace UrlShortener.Orchestration.Models;

public sealed record WorkflowRequest(
    string Requirement,
    bool ApprovalGranted = false,
    bool SimulateFailure = false,
    WorkflowScenario Scenario = WorkflowScenario.Greenfield);