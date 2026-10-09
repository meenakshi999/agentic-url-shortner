using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Agents;

/// <summary>
/// Agent provider backed by Groq cloud API (https://groq.com).
/// Groq is free — sign up at console.groq.com to get an API key (no credit card required).
/// Uses the OpenAI-compatible chat completions format.
/// Falls back to DemoAgentProvider if the API call fails.
/// </summary>
public sealed class GroqAgentProvider : IAgentProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly DemoAgentProvider _fallback = new();

    public GroqAgentProvider(HttpClient http, string model = "llama3-8b-8192")
    {
        _http = http;
        _model = model;
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SimulateFailure &&
            request.TaskName == "Execute unit and integration validation")
        {
            return new AgentResult(
                Success: false,
                Output: string.Empty,
                Error: "Simulated validation failure: integration test suite reported 3 failures " +
                       "in UrlResolutionService under high-concurrency load. Retry scheduled.");
        }

        try
        {
            var systemPrompt = BuildSystemPrompt(request);
            var userPrompt   = BuildUserPrompt(request);

            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userPrompt }
                },
                temperature = 0.3,
                max_tokens  = 1024
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await _http.PostAsJsonAsync(
                "/openai/v1/chat/completions", payload, cts.Token);

            if (!response.IsSuccessStatusCode)
                return await _fallback.ExecuteAsync(request, cancellationToken);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cts.Token);

            var output = json
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return string.IsNullOrWhiteSpace(output)
                ? await _fallback.ExecuteAsync(request, cancellationToken)
                : new AgentResult(Success: true, Output: output.Trim());
        }
        catch (Exception)
        {
            return await _fallback.ExecuteAsync(request, cancellationToken);
        }
    }

    private static string BuildSystemPrompt(AgentRequest request)
    {
        var scenarioNote = request.Scenario switch
        {
            WorkflowScenario.Brownfield =>
                "This is a BROWNFIELD scenario — an existing URL shortener codebase already exists. " +
                "Focus on impact analysis, backward compatibility, and migration concerns.",
            WorkflowScenario.Ambiguous =>
                "This is an AMBIGUOUS scenario — the requirement is under-specified. " +
                "Surface assumptions, request clarifications, and present ranked interpretations.",
            _ =>
                "This is a GREENFIELD scenario — a new URL shortener is being built from scratch."
        };

        return $"""
            You are a senior software engineer working inside an agentic SDLC orchestration system.
            You are executing the task: "{request.TaskName}".
            {scenarioNote}

            The existing codebase has five layers:
            - UrlShortener.Domain: UrlMapping entity (ShortCode, LongUrl, ClickCount, ExpiresAt, CreatedAt, IsActive)
            - UrlShortener.Application: IUrlShortenerService with ShortenAsync, ResolveAsync, DeactivateAsync, GetAnalyticsAsync
            - UrlShortener.Infrastructure: EF Core 10 + SQLite, UrlMappingRepository
            - UrlShortener.Api: UrlsController, WorkflowsController, HealthController, Swagger UI
            - UrlShortener.Orchestration: WorkflowOrchestrator, PolicyEngine, RollbackService, DynamicReplanner, OllamaAgentProvider, GroqAgentProvider

            Be concise, structured, and technically precise. Respond in plain text with clear sections.
            """;
    }

    private static string BuildUserPrompt(AgentRequest request)
    {
        var context = request.PreviousOutputs.Count > 0
            ? "\n\nContext from previous stages:\n" +
              string.Join("\n---\n", request.PreviousOutputs.Select(kv =>
                  $"[{kv.Key}]:\n{kv.Value[..Math.Min(400, kv.Value.Length)]}"))
            : string.Empty;

        return $"""
            Requirement: "{request.Requirement}"
            {context}

            Complete the task: {request.TaskName}

            Provide a structured, professional response demonstrating engineering depth.
            Include concrete details, trade-offs, and decisions. Be specific about files, classes, and APIs where relevant.
            """;
    }
}
