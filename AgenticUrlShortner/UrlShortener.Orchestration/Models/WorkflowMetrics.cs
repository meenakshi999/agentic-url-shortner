namespace UrlShortener.Orchestration.Models;

public sealed class WorkflowMetrics
{
    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public int TasksCompleted { get; set; }

    public int TasksFailed { get; set; }

    public int RetryCount { get; set; }

    public long? DurationMilliseconds =>
        CompletedAtUtc.HasValue
            ? (long)(CompletedAtUtc.Value - StartedAtUtc)
                .TotalMilliseconds
            : null;
}