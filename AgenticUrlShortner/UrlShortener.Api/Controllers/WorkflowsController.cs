using Microsoft.AspNetCore.Mvc;
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
}