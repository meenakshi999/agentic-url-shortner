using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public interface IWorkflowPlanner
{
    WorkflowPlan CreatePlan(
    string requirement,
    WorkflowScenario scenario);
}