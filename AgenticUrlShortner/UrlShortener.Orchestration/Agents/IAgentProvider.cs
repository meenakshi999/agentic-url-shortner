using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Agents;

public interface IAgentProvider
{
    Task<AgentResult> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}