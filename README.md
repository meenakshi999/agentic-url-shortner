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

    ┌─────────────────────────┐
    │       REST API           │
    │   UrlShortener.Api       │
    └────────────┬────────────┘
                 │
  ┌──────────────┼──────────────────┐
  │                                 │
  ▼                                 ▼
┌────────────────────────┐   ┌──────────────────────────┐
│  URL Shortener         │   │  Workflow Orchestrator    │
│  Application           │   │  UrlShortener.Orchestration│
└────────────┬───────────┘   └──────────────┬───────────┘
             │                              │
             ▼                              ▼
┌────────────────────────┐   ┌──────────────────────────┐
│  Domain                │   │  Agent Provider           │
│  UrlMapping entity     │   │  IAgentProvider           │
└────────────┬───────────┘   │  (Demo / Live LLM)        │
             │               └──────────────────────────┘
             ▼
┌────────────────────────┐
│  Infrastructure        │
│  EF Core + SQLite      │
└────────────────────────┘

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
    └── UrlShortener.IntegrationTests/ End-to-end API tests
    docs/
    ├── architecture.md
    ├── orchestration.md
    ├── scenarios.md
    ├── setup.md
    ├── testing.md
    └── engineering-summary.md

---

## Quick start

See [docs/setup.md](docs/setup.md) for full instructions.

    git clone https://github.com/meenakshi999/agentic-url-shortner.git
    cd agentic-url-shortner/AgenticUrlShortner
    dotnet build
    cd UrlShortener.Api
    dotnet run

Swagger UI: https://localhost:7212/swagger

---

## Run a full workflow

**Step 1 — Start a workflow**

    POST /api/workflow
    {
      "requirement": "Build a URL shortener with analytics and expiration",
      "scenario": "Greenfield",
      "approvalGranted": false
    }

**Step 2 — Grant approval** (when status is WaitingForApproval)

    POST /api/workflow/{workflowId}/approve

**Step 3 — Resume**

    POST /api/workflow/{workflowId}/resume

**Step 4 — View reliability metrics**

    GET /api/workflow/{workflowId}/metrics

**Step 5 — Trigger retry and rollback**

Add "simulateFailure": true to the Step 1 request body.

**Step 6 — Trigger dynamic re-planning**

    POST /api/workflow/{workflowId}/replan
    {
      "changedTaskId": "requirements",
      "newOutput": "Updated: add rate limiting to the shortener API"
    }

---

## Three scenarios

| Scenario | Pass in request body | What it demonstrates |
|----------|---------------------|----------------------|
| Greenfield | "scenario": "Greenfield" | New system built from scratch |
| Brownfield | "scenario": "Brownfield" | Enhancement with codebase impact analysis |
| Ambiguous | "scenario": "Ambiguous" | Under-specified requirement with clarification gate |

---

## Key API endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/workflow | Start a new workflow |
| GET | /api/workflow/{id} | Get workflow state and audit trail |
| POST | /api/workflow/{id}/approve | Grant human approval |
| POST | /api/workflow/{id}/resume | Resume after approval |
| POST | /api/workflow/{id}/stop | Safe stop |
| POST | /api/workflow/{id}/replan | Trigger dynamic re-planning |
| GET | /api/workflow/{id}/metrics | Reliability metrics and full audit trail |
| POST | /api/urls | Shorten a URL |
| GET | /{code} | Resolve — 302 redirect |
| PATCH | /api/urls/{code}/deactivate | Deactivate a URL (returns 410 Gone) |
| GET | /api/urls/{code}/analytics | Click analytics |
| GET | /health | Health check |

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
| [Architecture](docs/architecture.md) | System design, component diagram, key decisions |
| [Orchestration](docs/orchestration.md) | Lifecycle stages, dependency graph, governance model |
| [Scenarios](docs/scenarios.md) | Greenfield, Brownfield, Ambiguous walkthroughs |
| [Setup](docs/setup.md) | Prerequisites, build, run, API walkthrough |
| [Testing](docs/testing.md) | Strategy, limitations, trade-offs |
| [Engineering Summary](docs/engineering-summary.md) | Plan, artifacts, risks, assumptions, limitations |

---

## Swapping the agent provider

IAgentProvider is the only interface between the orchestration engine and the AI backend.
To use a live LLM, register a different implementation in Program.cs — no orchestration code changes required.

    // Current: deterministic simulation (no API key required)
    builder.Services.AddScoped<IAgentProvider, DemoAgentProvider>();

    // Production: live Claude AI
    builder.Services.AddScoped<IAgentProvider, ClaudeAgentProvider>();

---

## CI

[![CI](https://github.com/meenakshi999/agentic-url-shortner/actions/workflows/cd.yml/badge.svg)](https://github.com/meenakshi999/agentic-url-shortner/actions/workflows/cd.yml)

Build and test pipeline runs on every push and pull request to main.
