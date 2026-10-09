using UrlShortener.Orchestration.Models;
using UrlShortener.Orchestration.Services;

namespace UrlShortener.UnitTests.Orchestration;

public sealed class AgentOutputAnalyzerTests
{
    private readonly AgentOutputAnalyzer _analyzer = new();

    private static WorkflowContext Context() => new() { Requirement = "Build URL shortener" };

    [Fact]
    public void Analyze_CleanOutput_ReturnsContinue()
    {
        var result = _analyzer.Analyze("requirements", "Requirement normalized successfully.", Context());

        Assert.Equal(RoutingDecision.Continue, result.Decision);
    }

    [Theory]
    [InlineData("RISK: HIGH — schema change requires migration script.")]
    [InlineData("This is a BREAKING CHANGE to the public API contract.")]
    [InlineData("DATABASE MIGRATION required before deployment.")]
    [InlineData("REQUIRES APPROVAL from security reviewer.")]
    public void Analyze_ApprovalSignal_ReturnsRequireApproval(string output)
    {
        var result = _analyzer.Analyze("architecture", output, Context());

        Assert.Equal(RoutingDecision.RequireApproval, result.Decision);
    }

    [Theory]
    [InlineData("CRITICAL VULNERABILITY detected in dependency chain.")]
    [InlineData("PII DETECTED in the requirement — GDPR review required.")]
    [InlineData("COMPLIANCE VIOLATION: data retention policy breached.")]
    public void Analyze_BlockSignal_ReturnsBlock(string output)
    {
        var result = _analyzer.Analyze("validation", output, Context());

        Assert.Equal(RoutingDecision.Block, result.Decision);
    }

    [Fact]
    public void Analyze_BlockSignal_AddsAuditEntry()
    {
        var context = Context();

        _analyzer.Analyze("validation", "CRITICAL VULNERABILITY found.", context);

        Assert.Contains(context.AuditTrail, e =>
            e.Contains("BLOCK", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_ApprovalSignal_AddsAuditEntry()
    {
        var context = Context();

        _analyzer.Analyze("architecture", "RISK: HIGH — migration required.", context);

        Assert.Contains(context.AuditTrail, e =>
            e.Contains("APPROVAL REQUIRED", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_EmptyOutput_ReturnsContinue()
    {
        var result = _analyzer.Analyze("planning", string.Empty, Context());

        Assert.Equal(RoutingDecision.Continue, result.Decision);
    }

    [Fact]
    public void Analyze_BlockSignal_TakesPriorityOverApprovalSignal()
    {
        // Output contains both a block signal and an approval signal
        var output = "RISK: HIGH — also CRITICAL VULNERABILITY found.";

        var result = _analyzer.Analyze("validation", output, Context());

        Assert.Equal(RoutingDecision.Block, result.Decision);
    }
}
