using System.Text.Json;
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
    /// <summary>
    /// Server-Sent Events stream — pushes live workflow events as tasks complete.
    /// Connect with: curl -N https://localhost:7212/api/workflows/{id}/stream
    /// Each event is a JSON line prefixed with "data: ".
    /// </summary>
    [HttpGet("{workflowId:guid}/stream")]
    public async Task StreamEvents(Guid workflowId, CancellationToken cancellationToken)
    {
        Response.Headers["Content-Type"]  = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var seenAuditCount    = 0;
        var seenOutputCount   = 0;
        var lastStatus        = string.Empty;

        async Task SendAsync(string eventType, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            await Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var context = _workflowStore.Get(workflowId);
            if (context is null)
            {
                await SendAsync("error", new { message = "Workflow not found." });
                return;
            }

            // Stream new audit entries
            var newAuditEntries = context.AuditTrail.Skip(seenAuditCount).ToList();
            foreach (var entry in newAuditEntries)
            {
                await SendAsync("audit", new { entry });
                seenAuditCount++;
            }

            // Stream new task outputs
            var allOutputs = context.Outputs.ToList();
            var newOutputs = allOutputs.Skip(seenOutputCount).ToList();
            foreach (var (taskId, output) in newOutputs)
            {
                await SendAsync("task_output", new { taskId, preview = output[..Math.Min(300, output.Length)] });
                seenOutputCount++;
            }

            // Stream status changes
            var currentStatus = context.Status.ToString();
            if (currentStatus != lastStatus)
            {
                await SendAsync("status", new { status = currentStatus, stage = context.CurrentStage.ToString() });
                lastStatus = currentStatus;
            }

            // Terminal states — close the stream
            if (context.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Stopped)
            {
                await SendAsync("done", new
                {
                    status  = context.Status.ToString(),
                    tasksCompleted = context.Metrics.TasksCompleted,
                    successRate    = context.Metrics.SuccessRate
                });
                return;
            }

            // Waiting for approval — notify and keep streaming (approval may come via POST)
            if (context.Status == WorkflowStatus.WaitingForApproval)
            {
                await SendAsync("approval_required", new { message = $"POST /api/workflows/{workflowId}/approve to resume." });
            }

            await Task.Delay(750, cancellationToken);
        }
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