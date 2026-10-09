# Agentic URL Shortener

A production-minded prototype demonstrating a governed agentic software engineering system.
The system transforms natural language requirements into reviewable engineering outcomes
using a stateful, dependency-aware orchestration engine with controlled autonomy and human oversight.

---

## What this demonstrates

| Capability | Implementation |
|------------|---------------|
| Requirement understanding | Normalises intent, identifies ambiguity, defines acceptance criteria |
| Task decomposition | Dependency-ordered task graph with sequential and parallel paths |
| Codebase reasoning | Brownfield impact analysis across modules, APIs, and data flows |
| Workflow orchestration | Stateful SDLC lifecycle engine — the critical differentiator |
| Policy guardrails | Security, compliance, and change-control enforcement before every task |
| Human approval gates | Workflow pauses at high-impact stages until approval is granted |
| Bounded retries | Per-task retry with exponential opportunity; max 2 retries |
| Rollback | Transitive downstream task rollback on permanent failure |
| Dynamic re-planning | Upstream output change invalidates and re-queues downstream tasks |
| Reliability metrics | Success rate, MTTR, retry frequency, rollback count, per-task latency |
| Audit trail | Timestamped, immutable event log for every decision and transition |
| Safe stop | Human-triggered workflow halt at any point with full state preserved |
| Three scenarios | Greenfield, Brownfield, Ambiguous — each with distinct decomposition |

---

## Architecture

```
REST API  (UrlShortener.Api)
    |
    +-------------------------------+
    |                               |
    v                               v
URL Shortener Application    Workflow Orchestrator
    |                         (UrlShortener.Orchestration)
    v                               |
Domain (UrlMapping)                 v
    |                         Agent Provider
    v                         (IAgentProvider)
Infrastructure
(EF Core + SQLite)
```

**Core design principle:** agents generate reasoning and outputs; the deterministic
orchestration layer owns execution flow, dependency ordering, retries, rollback,
policy evaluation, and approval gating. No agent ever directly controls execution.

---

## Orchestration lifecycle

    requirements
          │
          ▼
       planning
          │
          ▼
      architecture
          │
          ├──────────────────┐
          ▼                  ▼
    implementation      documentation
          │                  │
          └────────┬─────────┘
                   ▼
                testing
                   │
                   ▼
               validation
                   │
                   ▼
          human approval  ◄── workflow pauses here
                   │
                   ▼
                release

Parallel paths (implementation + documentation) execute concurrently via Task.WhenAll.
Every stage transition is gated by the policy engine before execution begins.

---

## Project structure

    AgenticUrlShortner/
    ├── UrlShortener.Api/              REST API — controllers, middleware, Swagger
    ├── UrlShortener.Application/      Application services and interfaces
    ├── UrlShortener.Domain/           Domain model — UrlMapping entity
    ├── UrlShortener.Infrastructure/   EF Core, SQLite, repositories
    ├── UrlShortener.Orchestration/    Agentic orchestration engine
    │   ├── Agents/                    IAgentProvider, DemoAgentProvider
    │   ├── Models/                    WorkflowContext, WorkflowTask, WorkflowMetrics
    │   ├── Policies/                  PolicyEngine, SecurityPolicy, ChangeControlPolicy, CompliancePolicy
    │   └── Services/                  WorkflowOrchestrator, RollbackService, DynamicReplanner
    ├── UrlShortener.UnitTests/        47 tests — orchestration, policy, metrics, rollback, re-planning
    ├── UrlShortener.IntegrationTests/ End-to-end API tests
    ├── architecture.md
    ├── orchestration.md
    ├── scenarios.md
    ├── setup.md
    ├── testing.md
    └── engineering-summary.md

---

## Quick start

See [setup.md](AgenticUrlShortner/setup.md) for full instructions.

    git clone https://github.com/meenakshi999/agentic-url-shortner.git
    cd agentic-url-shortner/AgenticUrlShortner
    dotnet build
    cd UrlShortener.Api
    dotnet run

Swagger UI: https://localhost:7212/swagger

---

## Run a full workflow

**Step 1 — Start a workflow**

```http
POST /api/workflows
Content-Type: application/json
```

```json
{
  "requirement": "Build a URL shortener with analytics and expiration",
  "scenario": "Greenfield"
}
```

**Step 2 — Check workflow status**

```http
GET /api/workflows/{workflowId}
```

**Step 3 — Approve the workflow**

```http
POST /api/workflows/{workflowId}/approve
```

The approval endpoint grants approval and resumes the workflow.

**Step 4 — View reliability metrics**

```http
GET /api/workflows/{workflowId}/metrics
```

**Step 5 — Stop a workflow**

```http
POST /api/workflows/{workflowId}/stop
```

**Step 6 — Trigger dynamic re-planning**

```http
POST /api/workflows/{workflowId}/replan
Content-Type: application/json
```

```json
{
  "changedTaskId": "requirements",
  "newOutput": "Updated: add rate limiting to the shortener API"
}
```

Check the workflow status and metrics endpoints to inspect execution and outcomes.

---

## Three scenarios

| Scenario | Pass in request body | What it demonstrates |
|----------|---------------------|----------------------|
| Greenfield | "scenario": "Greenfield" | New system built from scratch |
| Brownfield | "scenario": "Brownfield" | Enhancement with codebase impact analysis |
| Ambiguous | "scenario": "Ambiguous" | Under-specified requirement with clarification gate |

---

## Key API endpoints

| Method | Endpoint                              | Description                                   |
| ------ | ------------------------------------- | --------------------------------------------- |
| POST   | `/api/workflows`                      | Start a new workflow                          |
| GET    | `/api/workflows/{workflowId}`         | Get workflow state                            |
| POST   | `/api/workflows/{workflowId}/approve` | Approve and resume workflow                   |
| POST   | `/api/workflows/{workflowId}/stop`    | Stop a workflow                               |
| POST   | `/api/workflows/{workflowId}/replan`  | Trigger dynamic re-planning                   |
| GET    | `/api/workflows/{workflowId}/metrics` | Get reliability metrics and audit information |
| POST   | `/api/urls`                           | Create a short URL                            |
| GET    | `/api/urls/{shortCode}`               | Resolve a short URL                           |
| GET    | `/api/urls/{shortCode}/analytics`     | Get click analytics                           |
| DELETE | `/api/urls/{shortCode}`               | Delete a short URL                            |
| GET    | `/api/health`                         | Health check                                  |


---

## Reliability metrics

GET /api/workflow/{id}/metrics returns:

    {
      "successRatePercent": 100,
      "tasksCompleted": 8,
      "tasksFailed": 0,
      "retryCount": 0,
      "retryFrequency": 0,
      "rollbackCount": 0,
      "replanCount": 0,
      "meanTimeToRecoveryMs": null,
      "endToEndLatencyMs": 35578,
      "taskLatenciesMs": { "requirements": 0, "planning": 0 },
      "auditTrail": [...]
    }

---

## Policy guardrails

Every task is evaluated by three policies before execution:

| Policy | What it checks | Action on violation |
|--------|---------------|---------------------|
| SecurityPolicy | SQL injection, XSS, dangerous patterns in requirement | Deny — workflow halted |
| ChangeControlPolicy | High-impact stages (Implementation, Release) without approval | Require approval |
| CompliancePolicy | PII patterns (SSN, password, credit card) in requirement | Deny — workflow halted |

---

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](AgenticUrlShortner/architecture.md) | System design, component diagram, key decisions |
| [Orchestration](AgenticUrlShortner/orchestration.md) | Lifecycle stages, dependency graph, governance model |
| [Scenarios](AgenticUrlShortner/scenarios.md) | Greenfield, Brownfield, Ambiguous walkthroughs |
| [Setup](AgenticUrlShortner/setup.md) | Prerequisites, build, run, API walkthrough |
| [Testing](AgenticUrlShortner/testing.md) | Strategy, limitations, trade-offs |
| [Engineering Summary](AgenticUrlShortner/engineering-summary.md) | Plan, artifacts, risks, assumptions, limitations |

---

## Agent provider — live LLM via Ollama (no API key required)

The system ships with `OllamaAgentProvider` wired in by default.
Ollama runs open-source LLMs (Llama 3, Phi-3, Mistral, etc.) **locally on your machine — free, no API key**.

**To enable live AI reasoning:**

    # 1. Install Ollama  (https://ollama.com)
    ollama pull llama3          # ~4 GB download, one-time

    # 2. Ollama starts automatically — no extra step needed

    # 3. Run the API — it will call Ollama for every agent task
    cd UrlShortener.Api
    dotnet run

Configuration is in `appsettings.json`:

    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3",
      "Enabled": true
    }

Set `"Enabled": false` to fall back to the deterministic `DemoAgentProvider` (no Ollama needed).

`IAgentProvider` is the only interface between the orchestration engine and the AI backend.
Swap to any other LLM (Claude, OpenAI, Azure OpenAI) by registering a different implementation — no orchestration code changes required.

---

## CI

[![CI](https://github.com/meenakshi999/agentic-url-shortner/actions/workflows/cd.yml/badge.svg)](https://github.com/meenakshi999/agentic-url-shortner/actions/workflows/cd.yml)

Build and test pipeline runs on every push and pull request to main.
