using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Services;

public sealed class WorkflowPlanner : IWorkflowPlanner
{
    public WorkflowPlan CreatePlan(
        string requirement,
        WorkflowScenario scenario)
    {
        var plan = new WorkflowPlan();

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "requirements",
            Name = "Analyze and normalize requirement",
            Stage = WorkflowStage.RequirementAnalysis
        });

        if (scenario == WorkflowScenario.Brownfield)
        {
            plan.Tasks.Add(new WorkflowTask
            {
                Id = "brownfield-analysis",
                Name = "Analyze existing codebase and impacted modules",
                Stage = WorkflowStage.Planning,
                Dependencies = ["requirements"]
            });
        }

        if (scenario == WorkflowScenario.Ambiguous)
        {
            plan.Tasks.Add(new WorkflowTask
            {
                Id = "ambiguity-analysis",
                Name = "Identify ambiguity, assumptions and required clarifications",
                Stage = WorkflowStage.RequirementAnalysis,
                Dependencies = ["requirements"]
            });
        }

        var planningDependencies = scenario switch
        {
            WorkflowScenario.Brownfield =>
                new List<string> { "brownfield-analysis" },

            WorkflowScenario.Ambiguous =>
                new List<string> { "ambiguity-analysis" },

            _ =>
                new List<string> { "requirements" }
        };

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "planning",
            Name = "Decompose requirement into engineering tasks",
            Stage = WorkflowStage.Planning,
            Dependencies = planningDependencies
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "architecture",
            Name = "Produce architecture and design decisions",
            Stage = WorkflowStage.Architecture,
            Dependencies = ["planning"]
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "implementation",
            Name = "Implement required changes",
            Stage = WorkflowStage.Implementation,
            Dependencies = ["architecture"]
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "documentation",
            Name = "Generate engineering documentation",
            Stage = WorkflowStage.Implementation,
            Dependencies = ["architecture"]
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "testing",
            Name = "Execute unit and integration validation",
            Stage = WorkflowStage.Testing,
            Dependencies = ["implementation"]
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "validation",
            Name = "Validate outputs, risks and quality gates",
            Stage = WorkflowStage.Validation,
            Dependencies = ["testing", "documentation"]
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "approval",
            Name = "Human approval before release",
            Stage = WorkflowStage.HumanApproval,
            Dependencies = ["validation"],
            RequiresApproval = true
        });

        plan.Tasks.Add(new WorkflowTask
        {
            Id = "release",
            Name = "Verify release readiness",
            Stage = WorkflowStage.ReleaseReadiness,
            Dependencies = ["approval"]
        });

        return plan;
    }
}