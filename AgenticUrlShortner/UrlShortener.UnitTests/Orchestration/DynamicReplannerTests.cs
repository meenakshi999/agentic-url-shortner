using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.UnitTests.Orchestration;

public sealed class DynamicReplannerTests
{
    private readonly DynamicReplanner _sut = new();

    private static WorkflowPlan BuildPlan(params WorkflowTask[] tasks)
    {
        var plan = new WorkflowPlan();
        plan.Tasks.AddRange(tasks);
        return plan;
    }

    private static WorkflowTask Task(string id, params string[] dependencies) =>
        new() { Id = id, Name = id, Dependencies = [.. dependencies] };

    [Fact]
    public void Replan_OutputChanged_InvalidatesDownstreamTasks()
    {
        var changed = Task("A");
        var downstream = Task("B", "A");
        downstream.IsCompleted = true;
        downstream.RetryCount = 1;
        var plan = BuildPlan(changed, downstream);
        var context = new WorkflowContext();
        context.Outputs["A"] = "old output";
        context.Outputs["B"] = "b output";
        context.CompletedStages.Add("B");

        _sut.Replan("A", "new output", plan, context);

        Assert.False(downstream.IsCompleted);
        Assert.Equal(0, downstream.RetryCount);
        Assert.DoesNotContain("B", context.Outputs.Keys);
        Assert.DoesNotContain("B", context.CompletedStages);
    }

    [Fact]
    public void Replan_OutputUnchanged_ReturnsEmptyAndDoesNotMutate()
    {
        var changed = Task("A");
        var downstream = Task("B", "A");
        downstream.IsCompleted = true;
        var plan = BuildPlan(changed, downstream);
        var context = new WorkflowContext();
        context.Outputs["A"] = "same output";

        var invalidated = _sut.Replan("A", "same output", plan, context);

        Assert.Empty(invalidated);
        Assert.True(downstream.IsCompleted);
    }

    [Fact]
    public void Replan_OutputChanged_IncrementsReplanCount()
    {
        var plan = BuildPlan(Task("A"));
        var context = new WorkflowContext();
        context.Outputs["A"] = "old";

        _sut.Replan("A", "new", plan, context);

        Assert.Equal(1, context.ReplanCount);
        Assert.Equal(1, context.Metrics.ReplanCount);
    }

    [Fact]
    public void Replan_OutputChanged_InvalidatesTransitiveDependents()
    {
        var root = Task("A");
        var direct = Task("B", "A");
        direct.IsCompleted = true;
        var transitive = Task("C", "B");
        transitive.IsCompleted = true;
        var plan = BuildPlan(root, direct, transitive);
        var context = new WorkflowContext();
        context.Outputs["A"] = "old";
        context.Outputs["B"] = "b";
        context.Outputs["C"] = "c";

        var invalidated = _sut.Replan("A", "new", plan, context);

        Assert.Contains("B", invalidated);
        Assert.Contains("C", invalidated);
    }

    [Fact]
    public void Replan_OutputChanged_AddsReplanLogEntry()
    {
        var taskB = Task("B", "A");
        taskB.IsCompleted = true;
        var plan = BuildPlan(Task("A"), taskB);
        var context = new WorkflowContext();
        context.Outputs["A"] = "old";

        _sut.Replan("A", "new", plan, context);

        Assert.NotEmpty(context.ReplanLog);
    }
}
