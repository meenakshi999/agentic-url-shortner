namespace UrlShortener.Orchestration.Models;

public sealed class WorkflowMetrics
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksFailed { get; set; }
    public int RetryCount { get; set; }
    public int RollbackCount { get; set; }
    public int ReplanCount { get; set; }

    // Per-task execution time in milliseconds
    public Dictionary<string, long> TaskLatenciesMs { get; } = new();

    // Each entry = time between a task failure and its recovery
    public List<double> RecoveryTimesMs { get; } = new();

    // Computed: end-to-end latency
    public long? DurationMilliseconds =>
        CompletedAtUtc.HasValue
            ? (long)(CompletedAtUtc.Value - StartedAtUtc).TotalMilliseconds
            : null;

    // Computed: percentage of tasks that succeeded
    public double SuccessRate
    {
        get
        {
            var total = TasksCompleted + TasksFailed;
            return total == 0 ? 0 : Math.Round((double)TasksCompleted / total * 100, 2);
        }
    }

    // Computed: average retries per task attempted
    public double RetryFrequency
    {
        get
        {
            var total = TasksCompleted + TasksFailed;
            return total == 0 ? 0 : Math.Round((double)RetryCount / total, 2);
        }
    }

    // Computed: Mean Time To Recovery in milliseconds
    public double? MeanTimeToRecoveryMs =>
        RecoveryTimesMs.Count == 0
            ? null
            : Math.Round(RecoveryTimesMs.Average(), 2);
}
