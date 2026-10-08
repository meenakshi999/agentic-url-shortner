using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public interface IWorkflowStore
{
    void Save(WorkflowContext context);

    WorkflowContext? Get(Guid workflowId);
}