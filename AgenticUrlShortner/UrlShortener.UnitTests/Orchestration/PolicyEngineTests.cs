using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Policies;

namespace UrlShortener.UnitTests.Orchestration;

public sealed class PolicyEngineTests
{
    private static WorkflowTask TaskAt(WorkflowStage stage) =>
        new() { Id = "t1", Name = "t1", Stage = stage };

    private static WorkflowContext ContextWith(string requirement, bool approvalGranted = false) =>
        new()
        {
            Requirement = requirement,
            ApprovalGranted = approvalGranted
        };

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("DROP TABLE users; --")]
    [InlineData("exec(rm -rf /)")]
    public async Task SecurityPolicy_DangerousPattern_ReturnsDeny(string requirement)
    {
        var policy = new SecurityPolicy();
        var result = await policy.EvaluateAsync(TaskAt(WorkflowStage.Planning), ContextWith(requirement));

        Assert.Equal(PolicyAction.Deny, result.Action);
    }

    [Fact]
    public async Task SecurityPolicy_CleanRequirement_ReturnsAllow()
    {
        var policy = new SecurityPolicy();
        var result = await policy.EvaluateAsync(
            TaskAt(WorkflowStage.Planning),
            ContextWith("Build a URL shortener with analytics"));

        Assert.Equal(PolicyAction.Allow, result.Action);
    }

    [Theory]
    [InlineData(WorkflowStage.Implementation)]
    [InlineData(WorkflowStage.ReleaseReadiness)]
    public async Task ChangeControlPolicy_HighImpactStage_NoApproval_ReturnsRequireApproval(WorkflowStage stage)
    {
        var policy = new ChangeControlPolicy();
        var result = await policy.EvaluateAsync(TaskAt(stage), ContextWith("req", approvalGranted: false));

        Assert.Equal(PolicyAction.RequireApproval, result.Action);
    }

    [Fact]
    public async Task ChangeControlPolicy_HighImpactStage_ApprovalGranted_ReturnsAllow()
    {
        var policy = new ChangeControlPolicy();
        var result = await policy.EvaluateAsync(
            TaskAt(WorkflowStage.Implementation),
            ContextWith("req", approvalGranted: true));

        Assert.Equal(PolicyAction.Allow, result.Action);
    }

    [Fact]
    public async Task ChangeControlPolicy_LowImpactStage_ReturnsAllow()
    {
        var policy = new ChangeControlPolicy();
        var result = await policy.EvaluateAsync(
            TaskAt(WorkflowStage.Planning),
            ContextWith("req", approvalGranted: false));

        Assert.Equal(PolicyAction.Allow, result.Action);
    }

    [Theory]
    [InlineData("need to store user ssn in the database")]
    [InlineData("save credit card details")]
    [InlineData("store plain text password")]
    public async Task CompliancePolicy_SensitivePattern_ReturnsDeny(string requirement)
    {
        var policy = new CompliancePolicy();
        var result = await policy.EvaluateAsync(TaskAt(WorkflowStage.Planning), ContextWith(requirement));

        Assert.Equal(PolicyAction.Deny, result.Action);
    }

    [Fact]
    public async Task CompliancePolicy_CleanRequirement_ReturnsAllow()
    {
        var policy = new CompliancePolicy();
        var result = await policy.EvaluateAsync(
            TaskAt(WorkflowStage.Planning),
            ContextWith("Build a URL shortener with click analytics"));

        Assert.Equal(PolicyAction.Allow, result.Action);
    }

    [Fact]
    public async Task PolicyEngine_AllPoliciesPass_ReturnsAllow()
    {
        var engine = PolicyEngine.Default();
        var result = await engine.EvaluateAsync(
            TaskAt(WorkflowStage.Planning),
            ContextWith("Build a URL shortener with analytics"));

        Assert.Equal(PolicyAction.Allow, result.Action);
    }

    [Fact]
    public async Task PolicyEngine_SecurityViolation_ReturnsDenyWithoutRunningOtherPolicies()
    {
        var engine = PolicyEngine.Default();
        var result = await engine.EvaluateAsync(
            TaskAt(WorkflowStage.Planning),
            ContextWith("DROP TABLE users; --"));

        Assert.Equal(PolicyAction.Deny, result.Action);
        Assert.Equal("SecurityPolicy", result.PolicyName);
    }
}
