namespace UrlShortener.Orchestration.Models;

public sealed class WorkflowContext
{
    public Guid WorkflowId { get; init; } = Guid.NewGuid();

    public string Requirement { get; init; } = string.Empty;

    public WorkflowStage CurrentStage { get; set; }

    public WorkflowStatus Status { get; set; } =
        WorkflowStatus.NotStarted;

    public Dictionary<string, string> Outputs { get; } = new();

    public List<string> CompletedStages { get; } = new();

    public List<string> Decisions { get; } = new();

    public List<string> AuditTrail { get; } = new();

    public int RetryCount { get; set; }

    public bool ApprovalRequired { get; set; }

    public bool ApprovalGranted { get; set; }
    public bool SimulateFailure { get; set; }

    public bool StopRequested { get; set; }
    public WorkflowMetrics Metrics { get; set; } = new();
    public WorkflowScenario Scenario { get; set; }

    public void AddAuditEntry(string message)
    {
        AuditTrail.Add(
            $"{DateTime.UtcNow:O} | {message}");
    }
}