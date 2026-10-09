using UrlShortener.Orchestration.Agents;

namespace UrlShortener.UnitTests.Orchestration;

public sealed class AgentToolsTests
{
    [Theory]
    [InlineData("TOOL_CALL: estimate_complexity | url shortener", "estimate_complexity", "url shortener")]
    [InlineData("  TOOL_CALL: check_security | sql injection", "check_security", "sql injection")]
    [InlineData("TOOL_CALL: list_modules | ", "list_modules", "")]
    public void TryParse_ValidLine_ExtractsToolNameAndArg(string line, string expectedTool, string expectedArg)
    {
        var parsed = AgentTools.TryParse(line, out var toolName, out var toolArg);

        Assert.True(parsed);
        Assert.Equal(expectedTool, toolName);
        Assert.Equal(expectedArg, toolArg);
    }

    [Theory]
    [InlineData("Just a regular line of text.")]
    [InlineData("")]
    [InlineData("TOOL: wrong prefix format")]
    public void TryParse_InvalidLine_ReturnsFalse(string line)
    {
        var parsed = AgentTools.TryParse(line, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void EstimateComplexity_AuthFeature_ReturnsHighComplexity()
    {
        var tool = AgentTools.All["estimate_complexity"];
        var result = tool("add authentication to the API");

        Assert.Contains("HIGH", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckSecurity_UrlRedirect_SurfacesOpenRedirectRisk()
    {
        var tool = AgentTools.All["check_security"];
        var result = tool("url redirect feature");

        Assert.Contains("redirect", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryCodebase_InfrastructureModule_ReturnsEfCoreInfo()
    {
        var tool = AgentTools.All["query_codebase"];
        var result = tool("infrastructure");

        Assert.Contains("EF Core", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListModules_ReturnsAllFiveModules()
    {
        var tool = AgentTools.All["list_modules"];
        var result = tool(string.Empty);

        Assert.Contains("UrlShortener.Domain", result);
        Assert.Contains("UrlShortener.Orchestration", result);
    }

    [Fact]
    public void AssessRisk_SchemaChange_ReturnsHighRisk()
    {
        var tool = AgentTools.All["assess_risk"];
        var result = tool("database schema migration");

        Assert.Contains("HIGH", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllTools_AreRegistered()
    {
        Assert.Contains("estimate_complexity", AgentTools.All.Keys);
        Assert.Contains("check_security",      AgentTools.All.Keys);
        Assert.Contains("query_codebase",      AgentTools.All.Keys);
        Assert.Contains("list_modules",        AgentTools.All.Keys);
        Assert.Contains("assess_risk",         AgentTools.All.Keys);
    }
}
