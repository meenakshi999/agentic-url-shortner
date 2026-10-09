namespace UrlShortener.Orchestration.Models;

public sealed class WorkflowTask
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public WorkflowStage Stage { get; init; }
    public List<string> Dependencies { get; init; } = new();
    public bool RequiresApproval { get; init; }
    public bool IsCompleted { get; set; }
    public int RetryCount { get; set; }

    // Timing — set by the orchestrator during execution
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public bool IsRolledBack { get; set; }
    public string? RollbackReason { get; set; }
}
