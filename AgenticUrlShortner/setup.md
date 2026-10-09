# Setup Instructions

## Prerequisites

- .NET 10 SDK or later
- Visual Studio 2022 / 2026 or VS Code
- No external database required — SQLite is bundled
- Ollama (optional, recommended) — enables live LLM reasoning, free, no API key

---

## 1. Clone and Build

    git clone https://github.com/meenakshi999/agentic-url-shortner.git
    cd agentic-url-shortner/AgenticUrlShortner
    dotnet build

---

## 2. Enable Live AI Reasoning (Recommended)

The system ships with `OllamaAgentProvider` — it calls a real LLM locally for every SDLC stage.
Ollama is **free**, runs on your machine, and requires no API key.

### Install Ollama

Download and install from https://ollama.com (Windows / Mac / Linux).

### Pull a model (one-time, ~4 GB)

    ollama pull llama3

Ollama starts automatically in the background after installation.
You can verify it is running:

    curl http://localhost:11434/api/tags

### Configuration

`UrlShortener.Api/appsettings.json` controls Ollama:

    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3",
      "Enabled": true
    }

| Setting | Effect |
|---------|--------|
| `Enabled: true` | Calls Ollama for every agent task — **real AI reasoning** |
| `Enabled: false` | Falls back to `DemoAgentProvider` — deterministic hardcoded outputs |

If Ollama is enabled but not running, the system **automatically falls back** to `DemoAgentProvider` — no crash, no configuration change needed.

### Smaller / faster models (optional)

If Llama 3 is too large for your machine, these work well:

    ollama pull phi3        # ~2 GB, very fast
    ollama pull mistral     # ~4 GB, good quality

Update `"Model": "phi3"` in `appsettings.json` to match.

---

## 3. Run the API

    cd UrlShortener.Api
    dotnet run

Swagger UI: https://localhost:7212/swagger

If the HTTPS certificate is not trusted:

    dotnet dev-certs https --trust

---

## 4. Run Tests

    dotnet test UrlShortener.UnitTests
    dotnet test UrlShortener.IntegrationTests

All 47 unit tests pass without Ollama — tests use `DemoAgentProvider` directly.

---

## 5. Walkthrough: Full Workflow (Greenfield Scenario)

### Step 1 — Start a workflow

    POST /api/workflows
    Content-Type: application/json

    {
      "requirement": "Build a URL shortener with analytics and expiration",
      "scenario": "Greenfield"
    }

Copy the `workflowId` from the response.

When Ollama is running, each agent task produces real LLM-generated reasoning.
Watch the `outputs` map in the status response fill up with AI-generated content.

### Step 2 — Check status

    GET /api/workflows/{workflowId}

### Step 3 — Grant approval (when status is WaitingForApproval)

    POST /api/workflows/{workflowId}/approve

### Step 4 — View reliability metrics

    GET /api/workflows/{workflowId}/metrics

### Step 5 — Stop a running workflow

    POST /api/workflows/{workflowId}/stop

### Step 6 — Trigger dynamic re-planning

    POST /api/workflows/{workflowId}/replan
    Content-Type: application/json

    {
      "changedTaskId": "requirements",
      "newOutput": "Updated: add rate limiting to the shortener API"
    }

### Step 7 — Simulate failure (to observe retry and rollback)

Add `"simulateFailure": true` to the POST /api/workflows request body.
The orchestrator will retry up to 2 times, then trigger transitive rollback on permanent failure.

---

## 6. Three Scenarios

| Scenario | Pass in request | What it demonstrates |
|----------|-----------------|----------------------|
| Greenfield | `"scenario": "Greenfield"` | New system, full SDLC pipeline |
| Brownfield | `"scenario": "Brownfield"` | Enhancement with codebase impact analysis |
| Ambiguous | `"scenario": "Ambiguous"` | Under-specified requirement with clarification gate |

With Ollama enabled, each scenario produces distinct AI-generated outputs tailored to the scenario type.

---

## 7. Configuration Reference

| Key | Default | Purpose |
|-----|---------|---------|
| `Ollama:Enabled` | `true` | Enable live LLM via Ollama |
| `Ollama:BaseUrl` | `http://localhost:11434` | Ollama API endpoint |
| `Ollama:Model` | `llama3` | Model to use |
| `ConnectionStrings:DefaultConnection` | `Data Source=urlshortener.db` | SQLite database path |

---

## 8. Swapping the Agent Provider

`IAgentProvider` is the only interface between the orchestration engine and the AI backend.
Register a different implementation in `Program.cs` to use any LLM — no orchestration changes required.

| Provider | When to use |
|----------|-------------|
| `OllamaAgentProvider` | Default — free, local, no API key |
| `DemoAgentProvider` | Set `Ollama:Enabled: false` — deterministic outputs for testing |
| `ClaudeAgentProvider` | Wire in if you have an Anthropic API key |
| `OpenAiAgentProvider` | Wire in if you have an OpenAI API key |
