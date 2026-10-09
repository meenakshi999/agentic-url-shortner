using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public sealed class DynamicReplanner
{
    // Called when the output of an already-completed task changes.
    // Invalidates all downstream tasks so the orchestrator re-executes them.
    public IReadOnlyList<string> Replan(
        string changedTaskId,
        string newOutput,
        WorkflowPlan plan,
        WorkflowContext context)
    {
        var previousOutput = context.Outputs.GetValueOrDefault(changedTaskId, string.Empty);

        if (previousOutput == newOutput)
            return [];

        // Update the output in context
        context.Outputs[changedTaskId] = newOutput;

        // Find all downstream tasks (direct and transitive dependents)
        var invalidated = FindDownstream(changedTaskId, plan);

        foreach (var task in invalidated)
        {
            task.IsCompleted = false;
            task.IsRolledBack = false;
            task.RollbackReason = null;
            task.StartedAtUtc = null;
            task.CompletedAtUtc = null;
            task.FailedAtUtc = null;
            task.RetryCount = 0;

            context.Outputs.Remove(task.Id);
            context.CompletedStages.Remove(task.Id);

            context.ReplanLog.Add(
                $"{DateTime.UtcNow:O} | Task '{task.Id}' invalidated because upstream '{changedTaskId}' output changed.");

            context.AddAuditEntry(
                $"Re-plan: invalidated '{task.Id}' due to changed output in '{changedTaskId}'.");
        }

        context.ReplanCount++;
        context.Metrics.ReplanCount++;
        context.Status = WorkflowStatus.Running;

        context.AddAuditEntry(
            $"Re-plan #{context.ReplanCount}: {invalidated.Count} task(s) invalidated. Workflow re-queued.");

        return invalidated.Select(t => t.Id).ToList();
    }

    private static List<WorkflowTask> FindDownstream(string changedTaskId, WorkflowPlan plan)
    {
        var result = new List<WorkflowTask>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(changedTaskId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var dependents = plan.Tasks
                .Where(t => t.Dependencies.Contains(current) && !visited.Contains(t.Id));

            foreach (var dep in dependents)
            {
                result.Add(dep);
                visited.Add(dep.Id);
                queue.Enqueue(dep.Id);
            }
        }

        return result;
    }
}
