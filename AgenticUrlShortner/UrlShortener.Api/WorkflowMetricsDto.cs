namespace UrlShortener.Api.Models;

public sealed record WorkflowMetricsDto(
    Guid WorkflowId,
    string Status,
    string Scenario,
    double SuccessRatePercent,
    int TasksCompleted,
    int TasksFailed,
    int RetryCount,
    double RetryFrequency,
    int RollbackCount,
    int ReplanCount,
    double? MeanTimeToRecoveryMs,
    long? EndToEndLatencyMs,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyDictionary<string, long> TaskLatenciesMs,
    IReadOnlyList<string> AuditTrail);
