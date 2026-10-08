namespace UrlShortener.Orchestration.Models;

public sealed class WorkflowPlan
{
    public List<WorkflowTask> Tasks { get; } = new();

    public WorkflowTask? GetTask(string id)
    {
        return Tasks.FirstOrDefault(x => x.Id == id);
    }
}