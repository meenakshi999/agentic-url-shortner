using System.Net.Http.Json;
using System.Text.Json;
using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Agents;

/// <summary>
/// Agent provider backed by a locally running Ollama instance (https://ollama.com).
/// Falls back to DemoAgentProvider output if Ollama is unreachable.
/// </summary>
public sealed class OllamaAgentProvider : IAgentProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly DemoAgentProvider _fallback = new();

    public OllamaAgentProvider(HttpClient http, string model = "llama3")
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

        var prompt = BuildPrompt(request);

        try
        {
            var finalOutput = await RunReActLoopAsync(prompt, cancellationToken);

            return string.IsNullOrWhiteSpace(finalOutput)
                ? await _fallback.ExecuteAsync(request, cancellationToken)
                : new AgentResult(Success: true, Output: finalOutput.Trim());
        }
        catch (Exception)
        {
            // Ollama not running — degrade gracefully to demo outputs
            return await _fallback.ExecuteAsync(request, cancellationToken);
        }
    }

    private async Task<string> RunReActLoopAsync(string initialPrompt, CancellationToken cancellationToken)
    {
        const int maxIterations = 3;
        var conversationPrompt = initialPrompt + ToolInstructions();
        var accumulatedToolResults = new System.Text.StringBuilder();

        for (var i = 0; i < maxIterations; i++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var payload = new
            {
                model = _model,
                prompt = conversationPrompt,
                stream = false,
                options = new { temperature = 0.3, num_predict = 600 }
            };

            var response = await _http.PostAsJsonAsync("/api/generate", payload, cts.Token);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cts.Token);
            var output = json.GetProperty("response").GetString() ?? string.Empty;

            // Check if the LLM wants to call a tool
            var toolCallLine = output
                .Split('\n')
                .FirstOrDefault(l => l.TrimStart().StartsWith("TOOL_CALL:", StringComparison.OrdinalIgnoreCase));

            if (toolCallLine is null)
                return output; // No tool call — final answer

            if (!AgentTools.TryParse(toolCallLine, out var toolName, out var toolArg))
                return output;

            if (!AgentTools.All.TryGetValue(toolName, out var tool))
                return output;

            var toolResult = tool(toolArg);
            accumulatedToolResults.AppendLine($"\nTOOL_RESULT ({toolName}): {toolResult}");

            // Inject tool result and ask LLM to continue
            conversationPrompt = initialPrompt +
                accumulatedToolResults +
                "\nUsing the tool results above, now provide your complete final answer. Do NOT emit any more TOOL_CALL lines.";
        }

        // Fallback: one final call asking for the answer without tools
        return await CallOllamaAsync(
            initialPrompt + accumulatedToolResults + "\nProvide your final answer now.",
            cancellationToken);
    }

    private async Task<string> CallOllamaAsync(string prompt, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        var payload = new
        {
            model = _model,
            prompt,
            stream = false,
            options = new { temperature = 0.3, num_predict = 600 }
        };

        var response = await _http.PostAsJsonAsync("/api/generate", payload, cts.Token);
        if (!response.IsSuccessStatusCode) return string.Empty;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cts.Token);
        return json.GetProperty("response").GetString() ?? string.Empty;
    }

    private static string ToolInstructions() =>
        """


        You have access to the following tools. To use one, emit exactly one line in this format:
        TOOL_CALL: tool_name | argument

        Available tools:
        - estimate_complexity | <feature description>  — estimates effort and complexity
        - check_security      | <input or feature>     — identifies security risks
        - query_codebase      | <module name>          — returns facts about the existing codebase
        - list_modules        | (no argument needed)   — lists all solution modules
        - assess_risk         | <change description>   — assesses deployment risk

        Use a tool ONLY if it would meaningfully improve your answer.
        After the tool result is provided, give your complete final answer without any more TOOL_CALL lines.
        """;

    private static string BuildPrompt(AgentRequest request)
    {
        var context = request.PreviousOutputs.Count > 0
            ? "\n\nContext from previous steps:\n" +
              string.Join("\n", request.PreviousOutputs.Select(kv => $"[{kv.Key}]: {kv.Value}"))
            : string.Empty;

        var scenarioNote = request.Scenario switch
        {
            WorkflowScenario.Brownfield =>
                "This is a BROWNFIELD scenario — an existing URL shortener system already exists. " +
                "Focus on impact analysis, backward compatibility, and migration concerns.",
            WorkflowScenario.Ambiguous =>
                "This is an AMBIGUOUS scenario — the requirement is under-specified. " +
                "Surface assumptions, request clarifications, and present ranked interpretations.",
            _ =>
                "This is a GREENFIELD scenario — building a new URL shortener from scratch."
        };

        return request.TaskName switch
        {
            "Analyze and normalize requirement" =>
                $"""
                You are a senior software engineer performing requirement analysis for a URL shortener system.
                {scenarioNote}

                Requirement: "{request.Requirement}"
                {context}

                Analyze and normalize this requirement. Provide:
                1. Normalized requirement statement
                2. Key functional requirements (bullet list)
                3. Non-functional requirements (performance, security, scalability)
                4. Acceptance criteria (Given/When/Then format)
                5. Out of scope items

                Be concise and structured.
                """,

            "Identify ambiguity, assumptions and required clarifications" =>
                $"""
                You are a senior software engineer analyzing requirements for a URL shortener system.
                {scenarioNote}

                Requirement: "{request.Requirement}"
                {context}

                Identify:
                1. Ambiguities — unclear or missing details
                2. Assumptions made to proceed
                3. Clarification questions that should be asked
                4. Risk if assumptions are wrong
                5. Recommended interpretation to proceed with

                Be specific and actionable.
                """,

            "Analyze existing codebase and impacted modules" =>
                $"""
                You are a senior software engineer performing brownfield impact analysis for a URL shortener system.

                The existing system has these modules:
                - UrlShortener.Api: REST API layer (controllers, middleware)
                - UrlShortener.Application: Business logic and interfaces
                - UrlShortener.Domain: Domain model (UrlMapping entity with ShortCode, LongUrl, ClickCount, ExpiresAt)
                - UrlShortener.Infrastructure: EF Core + SQLite persistence
                - UrlShortener.Orchestration: Agentic workflow engine

                Requirement change: "{request.Requirement}"
                {context}

                Analyze:
                1. Modules directly impacted
                2. Modules indirectly affected
                3. Database schema changes required
                4. API contract changes (breaking vs non-breaking)
                5. Migration strategy
                6. Risk assessment

                Be specific about files and classes.
                """,

            "Decompose requirement into engineering tasks" =>
                $"""
                You are a senior software engineer decomposing requirements into engineering tasks for a URL shortener.
                {scenarioNote}

                Requirement: "{request.Requirement}"
                {context}

                Create a dependency-ordered task breakdown:
                1. List all tasks with IDs (T1, T2, etc.)
                2. For each task: description, estimated effort, dependencies
                3. Identify which tasks can run in parallel
                4. Note any approval gates or decision points
                5. Define exit criteria for the task set

                Format as a structured task list.
                """,

            "Produce architecture and design decisions" =>
                $"""
                You are a principal architect designing a URL shortener system.
                {scenarioNote}

                Requirement: "{request.Requirement}"
                {context}

                Document architecture decisions:
                1. Component design (what components, responsibilities)
                2. Data model (entities, relationships, indexes)
                3. API design (endpoints, request/response shapes)
                4. Key architectural decisions (ADRs) with rationale and trade-offs
                5. Technology choices with justification
                6. Scalability and reliability considerations

                Be precise and opinionated.
                """,

            "Implement required changes" =>
                $"""
                You are a senior software engineer implementing changes to a URL shortener system.
                {scenarioNote}

                Requirement: "{request.Requirement}"
                {context}

                Describe the implementation:
                1. Files to create or modify (with purpose)
                2. Key code changes (classes, methods, interfaces)
                3. Database migration steps
                4. Configuration changes
                5. Dependency updates
                6. Implementation notes and gotchas

                Be specific about the code changes.
                """,

            "Generate engineering documentation" =>
                $"""
                You are a technical writer generating engineering documentation for a URL shortener system.

                Requirement: "{request.Requirement}"
                {context}

                Generate a documentation plan:
                1. README sections (what to update)
                2. API documentation (endpoint descriptions, examples)
                3. Architecture decision records (ADRs) to write
                4. Runbook entries (operational procedures)
                5. Developer onboarding notes

                Keep it practical and maintainable.
                """,

            "Execute unit and integration validation" =>
                $"""
                You are a QA engineer validating a URL shortener system implementation.

                Requirement: "{request.Requirement}"
                {context}

                Describe the validation approach:
                1. Unit tests to write (class, method, scenario)
                2. Integration tests (API endpoints to test, expected responses)
                3. Edge cases and error scenarios
                4. Performance validation criteria
                5. Test results summary (assume all pass for this analysis)
                6. Coverage assessment

                Be specific about test cases.
                """,

            "Validate outputs, risks and quality gates" =>
                $"""
                You are a tech lead performing risk assessment and quality gate validation for a URL shortener.
                {scenarioNote}

                Requirement: "{request.Requirement}"
                {context}

                Assess:
                1. Risk register (risk, likelihood, impact, mitigation)
                2. Quality gate checklist (what must pass before release)
                3. Security considerations (OWASP top 10 relevant items)
                4. Performance benchmarks met
                5. Go/no-go recommendation with conditions

                Be conservative and thorough.
                """,

            "Human approval before release" =>
                $"""
                You are a release manager preparing a release approval summary for a URL shortener system.

                Requirement: "{request.Requirement}"
                {context}

                Prepare the approval package:
                1. Change summary (what is being released)
                2. Test coverage summary
                3. Risk summary (residual risks)
                4. Rollback plan
                5. Release checklist (all items must be checked)
                6. Approval recommendation

                This summary will be reviewed by a human approver before release proceeds.
                """,

            "Prepare release and deployment plan" =>
                $"""
                You are a DevOps engineer preparing the release and deployment plan for a URL shortener system.

                Requirement: "{request.Requirement}"
                {context}

                Document:
                1. Deployment steps (ordered checklist)
                2. Environment configuration changes
                3. Database migration execution plan
                4. Health check criteria (how to confirm successful deployment)
                5. Monitoring and alerting to set up
                6. Rollback procedure with trigger conditions

                Be operationally precise.
                """,

            _ =>
                $"""
                You are a senior software engineer working on a URL shortener system.
                {scenarioNote}

                Task: {request.TaskName}
                Requirement: "{request.Requirement}"
                {context}

                Complete this task with a structured, professional response that demonstrates
                engineering depth and attention to detail.
                """
        };
    }
}
