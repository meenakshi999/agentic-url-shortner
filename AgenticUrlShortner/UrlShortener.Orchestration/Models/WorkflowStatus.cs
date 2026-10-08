namespace UrlShortener.Orchestration.Models;

public enum WorkflowStatus
{
    NotStarted,
    Running,
    WaitingForApproval,
    Completed,
    Failed,
    Stopped
}