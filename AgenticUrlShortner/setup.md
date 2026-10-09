# Setup Instructions

## Prerequisites

- .NET 9 SDK or later
- Visual Studio 2022 / 2026 or VS Code
- No external database setup required — SQLite is bundled

## Clone and Build

Open a terminal in the solution root and run:

    git clone https://github.com/meenakshi999/agentic-url-shortner.git
    cd agentic-url-shortner/AgenticUrlShortner
    dotnet build

## Run the API

    cd UrlShortener.Api
    dotnet run

The API starts on https://localhost:7001 (port shown in terminal output).
Swagger UI is available at https://localhost:7001/swagger

## Run Tests

    dotnet test UrlShortener.UnitTests
    dotnet test UrlShortener.IntegrationTests

## Walkthrough: Full Workflow (Greenfield Scenario)

### Step 1 — Start a workflow

    POST /api/workflow
    Content-Type: application/json

    {
      "requirement": "Build a URL shortener with analytics and expiration",
      "scenario": "Greenfield",
      "approvalGranted": false
    }

Copy the workflowId from the response.

### Step 2 — Check status

    GET /api/workflow/{workflowId}

### Step 3 — Grant approval (when status is WaitingForApproval)

    POST /api/workflow/{workflowId}/approve

### Step 4 — Resume after approval

    POST /api/workflow/{workflowId}/resume

### Step 5 — Stop a running workflow

    POST /api/workflow/{workflowId}/stop

### Step 6 — Trigger dynamic re-planning

    POST /api/workflow/{workflowId}/replan
    Content-Type: application/json

    {
      "changedTaskId": "requirements",
      "newOutput": "Updated: add rate limiting to the shortener API"
    }

### Step 7 — Simulate failure (to observe retry and rollback)

Add "simulateFailure": true to the POST /api/workflow request body.
The orchestrator will trigger retries, then rollback on permanent failure.

## Scenarios

| Scenario    | Description                                      |
|-------------|--------------------------------------------------|
| Greenfield  | New system built from scratch                    |
| Brownfield  | Enhancement to existing codebase                 |
| Ambiguous   | Underspecified requirement with clarification    |

Pass "scenario": "Greenfield", "Brownfield", or "Ambiguous" in the request body.

## Configuration

| Key                  | Purpose                                              |
|----------------------|------------------------------------------------------|
| Anthropic:ApiKey     | Optional — enables live Claude AI agent provider     |
| SimulateFailure      | Pass true in request body to test retry/rollback     |

To use the live Claude provider, add to appsettings.Development.json:

    {
      "Anthropic": {
        "ApiKey": "your-key-here"
      }
    }

By default the system uses DemoAgentProvider which runs without any API key.
