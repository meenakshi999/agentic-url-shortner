using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Policies;

namespace UrlShortener.Orchestration.Services;

public sealed class WorkflowOrchestrator
{
    private const int MaxRetries = 2;

    private readonly IWorkflowPlanner _planner;
    private readonly IAgentProvider _agentProvider;
    private readonly IWorkflowStore _workflowStore;
    private readonly RollbackService _rollbackService = new();
    private readonly DynamicReplanner _replanner = new();
    private readonly PolicyEngine _policyEngine = PolicyEngine.Default();
    private readonly AgentOutputAnalyzer _outputAnalyzer = new();

    public WorkflowOrchestrator(
    IWorkflowPlanner planner,
    IAgentProvider agentProvider,
    IWorkflowStore workflowStore)
    {
        _planner = planner;
        _agentProvider = agentProvider;
        _workflowStore = workflowStore;
    }

    public async Task<WorkflowContext> ExecuteAsync(
    string requirement,
    bool approvalGranted = false,
    bool simulateFailure = false,
    WorkflowScenario scenario = WorkflowScenario.Greenfield,
    CancellationToken cancellationToken = default)
    {
        var context = new WorkflowContext
        {
            Requirement = requirement,
            Status = WorkflowStatus.Running,
            CurrentStage = WorkflowStage.RequirementAnalysis,
            ApprovalGranted = approvalGranted,
            SimulateFailure = simulateFailure,
            Scenario = scenario,
            Metrics = new WorkflowMetrics { StartedAtUtc = DateTime.UtcNow }
        };

        _workflowStore.Save(context);
        context.AddAuditEntry($"Workflow started: {context.WorkflowId}");

        var plan = _planner.CreatePlan(requirement, scenario);
        context.AddAuditEntry($"Plan created with {plan.Tasks.Count} tasks.");

        await RunTaskLoopAsync(plan, context, cancellationToken);
        return context;
    }


    private async Task ExecuteTaskAsync(WorkflowTask task, WorkflowPlan plan, WorkflowContext context, CancellationToken cancellationToken)
    {
        context.CurrentStage = task.Stage;
        context.AddAuditEntry($"Starting task: {task.Id}");
        // Policy guardrail check before execution
        var policyResult = await _policyEngine.EvaluateAsync(task, context);
        if (policyResult.Action == PolicyAction.Deny)
        {
            context.Status = WorkflowStatus.Failed;
            context.CurrentStage = WorkflowStage.Failed;
            context.Metrics.TasksFailed++;
            context.Metrics.CompletedAtUtc = DateTime.UtcNow;
            return;
        }
        if (policyResult.Action == PolicyAction.RequireApproval && !context.ApprovalGranted)
        {
            context.Status = WorkflowStatus.WaitingForApproval;
            context.CurrentStage = WorkflowStage.HumanApproval;
            context.ApprovalRequired = true;
            _workflowStore.Save(context);
            return;
        }

        task.StartedAtUtc = DateTime.UtcNow;
        DateTime? lastFailedAt = null;

        while (task.RetryCount <= MaxRetries)
        {
            try
            {
                var request = new AgentRequest(
                    task.Name,
                    context.Requirement,
                    context.Outputs,
                    context.Scenario,
                    context.SimulateFailure);

                var result = await _agentProvider.ExecuteAsync(request, cancellationToken);

                if (!result.Success)
                    throw new InvalidOperationException(result.Error ?? "Agent execution failed.");

                task.CompletedAtUtc = DateTime.UtcNow;

                // Record per-task latency
                var latencyMs = (long)(task.CompletedAtUtc.Value - task.StartedAtUtc!.Value).TotalMilliseconds;
                context.Metrics.TaskLatenciesMs[task.Id] = latencyMs;

                // Record MTTR if this task recovered from a failure
                if (lastFailedAt.HasValue)
                {
                    var recoveryMs = (task.CompletedAtUtc.Value - lastFailedAt.Value).TotalMilliseconds;
                    context.Metrics.RecoveryTimesMs.Add(recoveryMs);
                }

                context.Outputs[task.Id] = result.Output;
                task.IsCompleted = true;
                context.Metrics.TasksCompleted++;
                context.CompletedStages.Add(task.Id);
                context.Decisions.Add($"{task.Id}: {result.Output}");
                context.AddAuditEntry($"Completed task: {task.Id} in {latencyMs}ms");

                // Intelligent routing — let the LLM's output influence execution flow
                var routing = _outputAnalyzer.Analyze(task.Id, result.Output, context);
                if (routing.Decision == RoutingDecision.Block)
                {
                    context.Status = WorkflowStatus.Failed;
                    context.CurrentStage = WorkflowStage.Failed;
                    context.Metrics.TasksFailed++;
                    context.Metrics.CompletedAtUtc = DateTime.UtcNow;
                    context.AddAuditEntry($"Workflow blocked by agent output analysis: {routing.Reason}");
                    return;
                }
                if (routing.Decision == RoutingDecision.RequireApproval && !context.ApprovalGranted)
                {
                    context.Status = WorkflowStatus.WaitingForApproval;
                    context.CurrentStage = WorkflowStage.HumanApproval;
                    context.ApprovalRequired = true;
                    context.AddAuditEntry($"Approval required by agent output analysis: {routing.Reason}");
                    _workflowStore.Save(context);
                    return;
                }

                return;
            }
            catch (Exception ex)
            {
                if (task.RetryCount >= MaxRetries)
                {
                    task.FailedAtUtc = DateTime.UtcNow;
                    context.Status = WorkflowStatus.Failed;
                    context.CurrentStage = WorkflowStage.Failed;
                    context.Metrics.TasksFailed++;
                    context.Metrics.RetryCount = context.RetryCount;
                    context.Metrics.CompletedAtUtc = DateTime.UtcNow;
                    context.AddAuditEntry($"Task failed after {MaxRetries + 1} attempts: {task.Id}");
                    context.AddAuditEntry($"Final error: {ex.Message}");

                    // Rollback any downstream tasks that depended on this one
                    var plans = _planner.CreatePlan(context.Requirement, context.Scenario);
                    _rollbackService.RollbackDownstream(task, plans, context);

                    return;
                }


                lastFailedAt = DateTime.UtcNow;
                task.RetryCount++;
                context.RetryCount++;
                context.AddAuditEntry($"Retry {task.RetryCount} for {task.Id}: {ex.Message}");
            }
        }
    }

    public WorkflowContext? Approve(Guid workflowId)
    {
        var context = _workflowStore.Get(workflowId);

        if (context is null)
        {
            return null;
        }

        if (context.Status != WorkflowStatus.WaitingForApproval)
        {
            return context;
        }

        context.ApprovalGranted = true;
        context.ApprovalRequired = false;

        context.AddAuditEntry(
            "Human approval granted.");

        _workflowStore.Save(context);

        return context;
    }
    public WorkflowContext? Stop(Guid workflowId)
    {
        var context = _workflowStore.Get(workflowId);

        if (context is null)
        {
            return null;
        }

        context.StopRequested = true;
        context.Status = WorkflowStatus.Stopped;
        context.CurrentStage = WorkflowStage.Stopped;

        context.AddAuditEntry(
            "Workflow stopped by human safety control.");

        _workflowStore.Save(context);

        return context;
    }
    public async Task<WorkflowContext?> ResumeAsync(Guid workflowId,CancellationToken cancellationToken = default)
    {
        var context = _workflowStore.Get(workflowId);
        if (context is null) return null;
        if (!context.ApprovalGranted) return context;

        var plan = _planner.CreatePlan(context.Requirement, context.Scenario);

        foreach (var task in plan.Tasks)
        {
            if (context.CompletedStages.Contains(task.Id))
                task.IsCompleted = true;
        }

        var approvalTask = plan.Tasks.FirstOrDefault(t => t.RequiresApproval);
        if (approvalTask is not null && context.ApprovalGranted)
        {
            approvalTask.IsCompleted = true;
            context.CompletedStages.Add(approvalTask.Id);
            context.AddAuditEntry("Human approval task completed.");
        }

        context.Status = WorkflowStatus.Running;
        context.ApprovalRequired = false;
        context.AddAuditEntry("Workflow resumed after human approval.");

        await RunTaskLoopAsync(plan, context, cancellationToken);
        return context;
    }

    public async Task<WorkflowContext?> ReplanAndResumeAsync(Guid workflowId,string changedTaskId,string newOutput,CancellationToken cancellationToken = default)
    {
        var context = _workflowStore.Get(workflowId);
        if (context is null) return null;

        var plan = _planner.CreatePlan(context.Requirement, context.Scenario);

        // Restore already-completed task state
        foreach (var task in plan.Tasks)
        {
            if (context.CompletedStages.Contains(task.Id))
                task.IsCompleted = true;
        }

        var invalidated = _replanner.Replan(changedTaskId, newOutput, plan, context);

        if (invalidated.Count == 0)
        {
            context.AddAuditEntry("Re-plan requested but no output change detected. Workflow unchanged.");
            return context;
        }

        _workflowStore.Save(context);

        // Re-execute only the invalidated tasks by resuming the workflow
        return await ResumeAsync(workflowId, cancellationToken);
    }
    private async Task RunTaskLoopAsync(WorkflowPlan plan,WorkflowContext context,CancellationToken cancellationToken)
    {
        while (plan.Tasks.Any(task => !task.IsCompleted))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.StopRequested)
            {
                context.Status = WorkflowStatus.Stopped;
                context.CurrentStage = WorkflowStage.Stopped;
                context.AddAuditEntry("Workflow stopped by safety control.");
                _workflowStore.Save(context);
                return;
            }

            var approvalTask = plan.Tasks
                .FirstOrDefault(task =>
                    !task.IsCompleted &&
                    task.RequiresApproval &&
                    task.Dependencies.All(dep => plan.GetTask(dep)?.IsCompleted == true));

            if (approvalTask is not null)
            {
                context.Status = WorkflowStatus.WaitingForApproval;
                context.CurrentStage = WorkflowStage.HumanApproval;
                context.ApprovalRequired = true;
                context.AddAuditEntry("Workflow paused for human approval.");

                if (!context.ApprovalGranted)
                {
                    _workflowStore.Save(context);
                    return;
                }

                approvalTask.IsCompleted = true;
                context.CompletedStages.Add(approvalTask.Id);
                context.AddAuditEntry("Human approval granted.");
                continue;
            }

            var readyTasks = plan.Tasks
                .Where(task =>
                    !task.IsCompleted &&
                    !task.RequiresApproval &&
                    task.Dependencies.All(dep => plan.GetTask(dep)?.IsCompleted == true))
                .ToList();

            if (readyTasks.Count == 0)
            {
                context.Status = WorkflowStatus.Failed;
                context.CurrentStage = WorkflowStage.Failed;
                context.AddAuditEntry("Workflow stopped: no executable task available.");
                _workflowStore.Save(context);
                return;
            }

            context.AddAuditEntry($"Executing {readyTasks.Count} ready task(s) in parallel.");

            await Task.WhenAll(readyTasks.Select(task =>
                ExecuteTaskAsync(task, plan, context, cancellationToken)));

            _workflowStore.Save(context);

            if (context.Status is WorkflowStatus.Failed or WorkflowStatus.WaitingForApproval)
                return;

        }

        context.Status = WorkflowStatus.Completed;
        context.CurrentStage = WorkflowStage.Completed;
        context.Metrics.CompletedAtUtc = DateTime.UtcNow;
        context.Metrics.RetryCount = context.RetryCount;
        context.AddAuditEntry("Workflow completed successfully.");
        _workflowStore.Save(context);
    }

}