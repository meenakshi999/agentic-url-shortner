using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.UnitTests.Orchestration;

public sealed class RollbackServiceTests
{
    private readonly RollbackService _sut = new();

    private static WorkflowPlan BuildPlan(params WorkflowTask[] tasks)
    {
        var plan = new WorkflowPlan();
        plan.Tasks.AddRange(tasks);
        return plan;
    }

    private static WorkflowTask Task(string id, params string[] dependencies) =>
        new() { Id = id, Name = id, Dependencies = [.. dependencies] };

    [Fact]
    public void RollbackDownstream_DirectDependent_IsRolledBack()
    {
        var failed = Task("A");
        var dependent = Task("B", "A");
        dependent.IsCompleted = true;
        var plan = BuildPlan(failed, dependent);
        var context = new WorkflowContext();
        context.Outputs["B"] = "some output";
        context.CompletedStages.Add("B");

        _sut.RollbackDownstream(failed, plan, context);

        Assert.True(dependent.IsRolledBack);
        Assert.False(dependent.IsCompleted);
        Assert.DoesNotContain("B", context.Outputs.Keys);
        Assert.DoesNotContain("B", context.CompletedStages);
    }

    [Fact]
    public void RollbackDownstream_TransitiveDependent_IsRolledBack()
    {
        var failed = Task("A");
        var direct = Task("B", "A");
        direct.IsCompleted = true;
        var transitive = Task("C", "B");
        transitive.IsCompleted = true;
        var plan = BuildPlan(failed, direct, transitive);
        var context = new WorkflowContext();
        context.Outputs["B"] = "b output";
        context.Outputs["C"] = "c output";

        _sut.RollbackDownstream(failed, plan, context);

        Assert.True(direct.IsRolledBack);
        Assert.True(transitive.IsRolledBack);
    }

    [Fact]
    public void RollbackDownstream_UnrelatedTask_IsNotRolledBack()
    {
        var failed = Task("A");
        var unrelated = Task("X");
        unrelated.IsCompleted = true;
        var plan = BuildPlan(failed, unrelated);
        var context = new WorkflowContext();

        _sut.RollbackDownstream(failed, plan, context);

        Assert.False(unrelated.IsRolledBack);
        Assert.True(unrelated.IsCompleted);
    }

    [Fact]
    public void RollbackDownstream_IncrementsRollbackCount_PerTask()
    {
        var failed = Task("A");
        var dep1 = Task("B", "A");
        dep1.IsCompleted = true;
        var dep2 = Task("C", "A");
        dep2.IsCompleted = true;
        var plan = BuildPlan(failed, dep1, dep2);
        var context = new WorkflowContext();

        _sut.RollbackDownstream(failed, plan, context);

        Assert.Equal(2, context.Metrics.RollbackCount);
    }

    [Fact]
    public void RollbackDownstream_AddsAuditEntry_ForEachRolledBackTask()
    {
        var failed = Task("A");
        var dep = Task("B", "A");
        dep.IsCompleted = true;
        var plan = BuildPlan(failed, dep);
        var context = new WorkflowContext();

        _sut.RollbackDownstream(failed, plan, context);

        Assert.Contains(context.AuditTrail, e => e.Contains("Rolled back task 'B'", StringComparison.OrdinalIgnoreCase));

    }
}
