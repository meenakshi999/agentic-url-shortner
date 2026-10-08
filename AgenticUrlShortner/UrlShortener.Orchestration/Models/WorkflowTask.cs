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
}