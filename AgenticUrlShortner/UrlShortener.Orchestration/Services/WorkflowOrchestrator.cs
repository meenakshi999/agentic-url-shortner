using UrlShortener.Orchestration.Agents;
using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public sealed class WorkflowOrchestrator
{
    private const int MaxRetries = 2;

    private readonly IWorkflowPlanner _planner;
    private readonly IAgentProvider _agentProvider;
    private readonly IWorkflowStore _workflowStore;

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
            Metrics = new WorkflowMetrics
            {
                StartedAtUtc = DateTime.UtcNow
            }
        };
        _workflowStore.Save(context);
        context.AddAuditEntry(
            $"Workflow started: {context.WorkflowId}");

        var plan = _planner.CreatePlan(
    requirement,
    scenario);

        context.AddAuditEntry(
            $"Plan created with {plan.Tasks.Count} tasks.");

        while (plan.Tasks.Any(task => !task.IsCompleted))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.StopRequested)
            {
                context.Status = WorkflowStatus.Stopped;
                context.CurrentStage = WorkflowStage.Stopped;

                context.AddAuditEntry(
                    "Workflow stopped by safety control.");
                _workflowStore.Save(context);
                return context;
            }

            var approvalTask = plan.Tasks
                .FirstOrDefault(task =>
                    !task.IsCompleted &&
                    task.RequiresApproval &&
                    task.Dependencies.All(
                        dependency =>
                            plan.GetTask(dependency)?.IsCompleted == true));

            if (approvalTask is not null)
            {
                context.Status = WorkflowStatus.WaitingForApproval;
                context.CurrentStage = WorkflowStage.HumanApproval;
                context.ApprovalRequired = true;

                context.AddAuditEntry(
                    "Workflow paused for human approval.");

                if (!context.ApprovalGranted)
                {
                    _workflowStore.Save(context);
                    return context;
                }

                approvalTask.IsCompleted = true;

                context.CompletedStages.Add(approvalTask.Id);

                context.AddAuditEntry(
                    "Human approval granted.");

                continue;
            }

            var readyTasks = plan.Tasks
                .Where(task =>
                    !task.IsCompleted &&
                    !task.RequiresApproval &&
                    task.Dependencies.All(
                        dependency =>
                            plan.GetTask(dependency)?.IsCompleted == true))
                .ToList();

            if (readyTasks.Count == 0)
            {
                context.Status = WorkflowStatus.Failed;
                context.CurrentStage = WorkflowStage.Failed;

                context.AddAuditEntry(
                    "Workflow stopped because no executable task was available.");
                _workflowStore.Save(context);
                return context;
            }

            context.AddAuditEntry(
                $"Executing {readyTasks.Count} ready task(s).");

            var executions = readyTasks
                .Select(task =>
                    ExecuteTaskAsync(
                        task,
                        context,
                        cancellationToken));

            await Task.WhenAll(executions);

            _workflowStore.Save(context);

            if (context.Status == WorkflowStatus.Failed)
            {
                return context;
            }
        }

        context.Status = WorkflowStatus.Completed;
        context.CurrentStage = WorkflowStage.Completed;

        context.AddAuditEntry(
            "Workflow completed successfully.");
        context.Metrics.CompletedAtUtc = DateTime.UtcNow;
        context.Metrics.RetryCount = context.RetryCount;
        _workflowStore.Save(context);
        return context;
    }

    private async Task ExecuteTaskAsync(
    WorkflowTask task,
    WorkflowContext context,
    CancellationToken cancellationToken)
    {
        context.CurrentStage = task.Stage;

        context.AddAuditEntry(
            $"Starting task: {task.Id}");

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

                var result = await _agentProvider.ExecuteAsync(
                    request,
                    cancellationToken);

                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        result.Error ?? "Agent execution failed.");
                }

                context.Outputs[task.Id] = result.Output;

                task.IsCompleted = true;
                context.Metrics.TasksCompleted++;

                context.CompletedStages.Add(task.Id);

                context.Decisions.Add(
                    $"{task.Id}: {result.Output}");

                context.AddAuditEntry(
                    $"Completed task: {task.Id}");

                return;
            }
            catch (Exception ex)
            {
                if (task.RetryCount >= MaxRetries)
                {
                    context.Status = WorkflowStatus.Failed;
                    context.CurrentStage = WorkflowStage.Failed;
                    context.Metrics.TasksFailed++;
                    context.Metrics.RetryCount = context.RetryCount;
                    context.Metrics.CompletedAtUtc = DateTime.UtcNow;

                    context.AddAuditEntry(
                        $"Task failed after {MaxRetries + 1} attempts: {task.Id}");

                    context.AddAuditEntry(
                        $"Final error: {ex.Message}");

                    return;
                }

                task.RetryCount++;
                context.RetryCount++;

                context.AddAuditEntry(
                    $"Retry {task.RetryCount} for {task.Id}: {ex.Message}");
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
    public async Task<WorkflowContext?> ResumeAsync(
    Guid workflowId,
    CancellationToken cancellationToken = default)
    {
        var context = _workflowStore.Get(workflowId);

        if (context is null)
        {
            return null;
        }

        if (!context.ApprovalGranted)
        {
            return context;
        }

        var plan = _planner.CreatePlan(
    context.Requirement,
    context.Scenario);

        foreach (var task in plan.Tasks)
        {
            if (context.CompletedStages.Contains(task.Id))
            {
                task.IsCompleted = true;
            }
        }

        var approvalTask = plan.Tasks
            .FirstOrDefault(task => task.RequiresApproval);

        if (approvalTask is not null &&
            context.ApprovalGranted)
        {
            approvalTask.IsCompleted = true;

            context.CompletedStages.Add(approvalTask.Id);

            context.AddAuditEntry(
                "Human approval task completed.");
        }

        context.Status = WorkflowStatus.Running;
        context.ApprovalRequired = false;

        context.AddAuditEntry(
            "Workflow resumed after human approval.");

        while (plan.Tasks.Any(task => !task.IsCompleted))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.StopRequested)
            {
                context.Status = WorkflowStatus.Stopped;
                context.CurrentStage = WorkflowStage.Stopped;

                context.AddAuditEntry(
                    "Workflow stopped by safety control.");

                _workflowStore.Save(context);

                return context;
            }

            var readyTasks = plan.Tasks
                .Where(task =>
                    !task.IsCompleted &&
                    !task.RequiresApproval &&
                    task.Dependencies.All(
                        dependency =>
                            plan.GetTask(dependency)?.IsCompleted == true))
                .ToList();

            if (readyTasks.Count == 0)
            {
                context.Status = WorkflowStatus.Failed;
                context.CurrentStage = WorkflowStage.Failed;

                context.AddAuditEntry(
                    "Workflow could not resume because dependencies were not satisfied.");

                _workflowStore.Save(context);

                return context;
            }

            var executions = readyTasks.Select(task =>
                ExecuteTaskAsync(
                    task,
                    context,
                    cancellationToken));

            await Task.WhenAll(executions);

            _workflowStore.Save(context);

            if (context.Status == WorkflowStatus.Failed)
            {
                context.Metrics.CompletedAtUtc = DateTime.UtcNow;
                context.Metrics.RetryCount = context.RetryCount;

                _workflowStore.Save(context);

                return context;
            }
        }

        context.Status = WorkflowStatus.Completed;
        context.CurrentStage = WorkflowStage.Completed;

        context.Metrics.CompletedAtUtc = DateTime.UtcNow;
        context.Metrics.RetryCount = context.RetryCount;

        context.AddAuditEntry(
            "Workflow completed successfully after approval.");

        _workflowStore.Save(context);

        return context;
    }
}