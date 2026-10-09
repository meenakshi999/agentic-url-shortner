using UrlShortener.Orchestration.Models;

namespace UrlShortener.UnitTests.Orchestration;

public sealed class WorkflowMetricsTests
{
    [Fact]
    public void SuccessRate_NoTasks_ReturnsZero()
    {
        var metrics = new WorkflowMetrics();
        Assert.Equal(0, metrics.SuccessRate);
    }

    [Fact]
    public void SuccessRate_AllCompleted_Returns100()
    {
        var metrics = new WorkflowMetrics { TasksCompleted = 5, TasksFailed = 0 };
        Assert.Equal(100, metrics.SuccessRate);
    }

    [Fact]
    public void SuccessRate_HalfCompleted_Returns50()
    {
        var metrics = new WorkflowMetrics { TasksCompleted = 3, TasksFailed = 3 };
        Assert.Equal(50, metrics.SuccessRate);
    }

    [Fact]
    public void RetryFrequency_NoTasks_ReturnsZero()
    {
        var metrics = new WorkflowMetrics();
        Assert.Equal(0, metrics.RetryFrequency);
    }

    [Fact]
    public void RetryFrequency_TwoRetriesAcrossFourTasks_Returns0Point5()
    {
        var metrics = new WorkflowMetrics
        {
            RetryCount = 2,
            TasksCompleted = 3,
            TasksFailed = 1
        };
        Assert.Equal(0.5, metrics.RetryFrequency);
    }

    [Fact]
    public void MeanTimeToRecoveryMs_NoRecoveryEvents_ReturnsNull()
    {
        var metrics = new WorkflowMetrics();
        Assert.Null(metrics.MeanTimeToRecoveryMs);
    }

    [Fact]
    public void MeanTimeToRecoveryMs_MultipleEvents_ReturnsAverage()
    {
        var metrics = new WorkflowMetrics();
        metrics.RecoveryTimesMs.Add(100);
        metrics.RecoveryTimesMs.Add(200);
        metrics.RecoveryTimesMs.Add(300);

        Assert.Equal(200, metrics.MeanTimeToRecoveryMs);
    }

    [Fact]
    public void DurationMilliseconds_NotCompleted_ReturnsNull()
    {
        var metrics = new WorkflowMetrics { StartedAtUtc = DateTime.UtcNow };
        Assert.Null(metrics.DurationMilliseconds);
    }

    [Fact]
    public void DurationMilliseconds_Completed_ReturnsElapsedMs()
    {
        var start = DateTime.UtcNow;
        var metrics = new WorkflowMetrics
        {
            StartedAtUtc = start,
            CompletedAtUtc = start.AddSeconds(5)
        };

        Assert.Equal(5000, metrics.DurationMilliseconds);
    }

    [Fact]
    public void TaskLatenciesMs_TracksPerTaskTiming()
    {
        var metrics = new WorkflowMetrics();
        metrics.TaskLatenciesMs["requirements"] = 120;
        metrics.TaskLatenciesMs["planning"] = 340;

        Assert.Equal(120, metrics.TaskLatenciesMs["requirements"]);
        Assert.Equal(2, metrics.TaskLatenciesMs.Count);
    }
}
