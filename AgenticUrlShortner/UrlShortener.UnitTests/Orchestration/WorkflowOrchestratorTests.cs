using Moq;
using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.UnitTests.Orchestration;

public class WorkflowOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WithFailure_RetriesAndFailsSafely()
    {
        var planner = new WorkflowPlanner();
        var agentProvider = new Mock<IAgentProvider>();

        agentProvider
            .Setup(x => x.ExecuteAsync(
                It.IsAny<AgentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(
                Success: false,
                Output: string.Empty,
                Error: "Simulated failure."));

        var store = new InMemoryWorkflowStore();

        var orchestrator = new WorkflowOrchestrator(
            planner,
            agentProvider.Object,
            store);

        var result = await orchestrator.ExecuteAsync(
            "Build a URL shortener.");

        Assert.Equal(
            WorkflowStatus.Failed,
            result.Status);

        Assert.Equal(
            WorkflowStage.Failed,
            result.CurrentStage);

        Assert.Equal(
            2,
            result.RetryCount);

        Assert.True(result.Metrics.TasksFailed > 0);

        Assert.Contains(
            result.AuditTrail,
            entry => entry.Contains("Retry 1"));

        Assert.Contains(
            result.AuditTrail,
            entry => entry.Contains("Retry 2"));
    }


    [Fact]
    public async Task ExecuteAsync_WithBrownfieldScenario_UsesBrownfieldPlan()
    {
        var planner = new WorkflowPlanner();
        var agentProvider = new Mock<IAgentProvider>();

        agentProvider
            .Setup(x => x.ExecuteAsync(
                It.IsAny<AgentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(
                Success: true,
                Output: "Task completed."));

        var store = new InMemoryWorkflowStore();

        var orchestrator = new WorkflowOrchestrator(
            planner,
            agentProvider.Object,
            store);

        var result = await orchestrator.ExecuteAsync(
            "Add expiration to the existing URL shortener.",
            scenario: WorkflowScenario.Brownfield);

        Assert.Equal(
            WorkflowScenario.Brownfield,
            result.Scenario);

        Assert.Contains(
            "brownfield-analysis",
            result.CompletedStages);

        Assert.Equal(
            WorkflowStatus.WaitingForApproval,
            result.Status);
    }


    [Fact]
    public async Task ExecuteAsync_WithAmbiguousScenario_UsesAmbiguityAnalysis()
    {
        var planner = new WorkflowPlanner();
        var agentProvider = new Mock<IAgentProvider>();

        agentProvider
            .Setup(x => x.ExecuteAsync(
                It.IsAny<AgentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(
                Success: true,
                Output: "Task completed."));

        var store = new InMemoryWorkflowStore();

        var orchestrator = new WorkflowOrchestrator(
            planner,
            agentProvider.Object,
            store);

        var result = await orchestrator.ExecuteAsync(
            "Make the URL shortener more secure.",
            scenario: WorkflowScenario.Ambiguous);

        Assert.Equal(
            WorkflowScenario.Ambiguous,
            result.Scenario);

        Assert.Contains(
            "ambiguity-analysis",
            result.CompletedStages);

        Assert.Equal(
            WorkflowStatus.WaitingForApproval,
            result.Status);
    }


    [Fact]
    public async Task Stop_StopsWorkflowAndRecordsSafetyControl()
    {
        var planner = new WorkflowPlanner();
        var agentProvider = new Mock<IAgentProvider>();

        var store = new InMemoryWorkflowStore();

        var orchestrator = new WorkflowOrchestrator(
            planner,
            agentProvider.Object,
            store);

        var result = await orchestrator.ExecuteAsync(
            "Build a URL shortener.");

        var stopped = orchestrator.Stop(
            result.WorkflowId);

        Assert.NotNull(stopped);

        Assert.Equal(
            WorkflowStatus.Stopped,
            stopped!.Status);

        Assert.Equal(
            WorkflowStage.Stopped,
            stopped.CurrentStage);

        Assert.True(stopped.StopRequested);

        Assert.Contains(
            stopped.AuditTrail,
            entry => entry.Contains(
                "safety control"));
    }
}