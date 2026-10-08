namespace UrlShortener.Orchestration.Models;

public enum WorkflowStage
{
    RequirementAnalysis,
    Planning,
    Architecture,
    Implementation,
    Testing,
    Validation,
    HumanApproval,
    ReleaseReadiness,
    Completed,
    Failed,
    Stopped
}