using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public sealed class RollbackService
{
    public void RollbackDownstream(
        WorkflowTask failedTask,
        WorkflowPlan plan,
        WorkflowContext context)
    {
        var toRollback = FindDownstreamTasks(failedTask.Id, plan);

        foreach (var task in toRollback)
        {
            if (!task.IsCompleted && !task.IsRolledBack)
                continue;

            task.IsCompleted = false;
            task.IsRolledBack = true;
            task.RollbackReason = $"Upstream task '{failedTask.Id}' failed permanently.";

            context.Outputs.Remove(task.Id);
            context.CompletedStages.Remove(task.Id);
            context.Metrics.RollbackCount++;

            context.AddAuditEntry(
                $"Rolled back task '{task.Id}': upstream failure in '{failedTask.Id}'.");
        }
    }

    private static List<WorkflowTask> FindDownstreamTasks(
        string failedTaskId,
        WorkflowPlan plan)
    {
        var downstream = new List<WorkflowTask>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(failedTaskId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var dependents = plan.Tasks
                .Where(t => t.Dependencies.Contains(current) && !visited.Contains(t.Id));

            foreach (var dependent in dependents)
            {
                downstream.Add(dependent);
                visited.Add(dependent.Id);
                queue.Enqueue(dependent.Id);
            }
        }

        return downstream;
    }
}
