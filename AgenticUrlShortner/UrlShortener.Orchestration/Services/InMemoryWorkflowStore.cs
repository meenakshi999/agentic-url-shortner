using System.Collections.Concurrent;
using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly ConcurrentDictionary<Guid, WorkflowContext>
        _workflows = new();

    public void Save(WorkflowContext context)
    {
        _workflows[context.WorkflowId] = context;
    }

    public WorkflowContext? Get(Guid workflowId)
    {
        return _workflows.TryGetValue(
            workflowId,
            out var context)
            ? context
            : null;
    }
}