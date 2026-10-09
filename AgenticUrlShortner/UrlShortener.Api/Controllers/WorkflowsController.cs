using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Models;
using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowOrchestrator _orchestrator;
    private readonly IWorkflowStore _workflowStore;

    public WorkflowsController(
    WorkflowOrchestrator orchestrator,
    IWorkflowStore workflowStore)
    {
        _orchestrator = orchestrator;
        _workflowStore = workflowStore;
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowContext>> Execute(
        [FromBody] WorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Requirement))
        {
            return BadRequest(new
            {
                error = "Requirement is required."
            });
        }

        var result = await _orchestrator.ExecuteAsync(
    request.Requirement,
    request.ApprovalGranted,
    request.SimulateFailure,
    request.Scenario,
    cancellationToken);

        return Ok(result);
    }

    [HttpGet("{workflowId:guid}")]
    public ActionResult<WorkflowContext> GetStatus(
    Guid workflowId)
    {
        var workflow = _workflowStore.Get(workflowId);

        if (workflow is null)
        {
            return NotFound(new
            {
                error = "Workflow was not found."
            });
        }

        return Ok(workflow);
    }
    [HttpPost("{workflowId:guid}/approve")]
    public async Task<ActionResult<WorkflowContext>> Approve(
    Guid workflowId,
    CancellationToken cancellationToken)
    {
        var workflow = _orchestrator.Approve(workflowId);

        if (workflow is null)
        {
            return NotFound(new
            {
                error = "Workflow was not found."
            });
        }

        if (!workflow.ApprovalGranted)
        {
            return Ok(workflow);
        }

        var result = await _orchestrator.ResumeAsync(
            workflowId,
            cancellationToken);

        return Ok(result);
    }
    [HttpPost("{workflowId:guid}/stop")]
    public ActionResult<WorkflowContext> Stop(
    Guid workflowId)
    {
        var workflow = _orchestrator.Stop(workflowId);

        if (workflow is null)
        {
            return NotFound(new
            {
                error = "Workflow was not found."
            });
        }

        return Ok(workflow);
    }
    [HttpPost("{workflowId:guid}/replan")]
    public async Task<IActionResult> Replan(
    Guid workflowId,
    [FromBody] ReplanRequest request,
    CancellationToken cancellationToken)
    {
        var context = await _orchestrator.ReplanAndResumeAsync(
            workflowId, request.ChangedTaskId, request.NewOutput, cancellationToken);

        if (context is null) return NotFound();
        return Ok(context);
    }
    [HttpGet("{workflowId:guid}/metrics")]
    public IActionResult GetMetrics(Guid workflowId)
    {
        var context = _workflowStore.Get(workflowId);
        if (context is null) return NotFound();

        var dto = new WorkflowMetricsDto(
            WorkflowId: context.WorkflowId,
            Status: context.Status.ToString(),
            Scenario: context.Scenario.ToString(),
            SuccessRatePercent: context.Metrics.SuccessRate,
            TasksCompleted: context.Metrics.TasksCompleted,
            TasksFailed: context.Metrics.TasksFailed,
            RetryCount: context.Metrics.RetryCount,
            RetryFrequency: context.Metrics.RetryFrequency,
            RollbackCount: context.Metrics.RollbackCount,
            ReplanCount: context.Metrics.ReplanCount,
            MeanTimeToRecoveryMs: context.Metrics.MeanTimeToRecoveryMs,
            EndToEndLatencyMs: context.Metrics.DurationMilliseconds,
            StartedAtUtc: context.Metrics.StartedAtUtc,
            CompletedAtUtc: context.Metrics.CompletedAtUtc,
            TaskLatenciesMs: context.Metrics.TaskLatenciesMs,
            AuditTrail: context.AuditTrail);

        return Ok(dto);
    }

}