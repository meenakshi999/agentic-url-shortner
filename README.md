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
| Agent tool calling | ReAct loop — agents call tools (security check, complexity estimate, codebase query) mid-reasoning |
| Intelligent routing | Agent output scanned for risk signals; orchestrator auto-pauses or blocks based on LLM reasoning |
| Policy guardrails | Security, compliance, and change-control enforcement before every task |
| Human approval gates | Workflow pauses at high-impact stages until approval is granted |
| Bounded retries | Per-task retry with exponential opportunity; max 2 retries |
| Rollback | Transitive downstream task rollback on permanent failure |
| Dynamic re-planning | Upstream output change invalidates and re-queues downstream tasks |
| Reliability metrics | Success rate, MTTR, retry frequency, rollback count, per-task latency |
| Audit trail | Timestamped, immutable event log for every decision and transition |
| SSE streaming | Live event stream — task outputs and status pushed to client as they happen |
| Workflow persistence | SQLite-backed store — workflows survive server restarts |
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
Domain (UrlMapping)                 +----> PolicyEngine
    |                               |
    v                               +----> AgentOutputAnalyzer (intelligent routing)
Infrastructure                      |
(EF Core + SQLite)                  +----> Agent Provider (IAgentProvider)
                                               |
                                    +----------+----------+
                                    |          |          |
                                    v          v          v
                                 Groq       Ollama     Demo
                               (cloud)    (local)  (+ ReAct traces)
```

**Core design principle:** agents generate reasoning and call tools; the deterministic
orchestration layer owns execution flow, dependency ordering, retries, rollback,
policy evaluation, approval gating, and intelligent routing. No agent ever directly controls execution.

---

## Agent providers — three options, zero cost

| Provider | How to enable | What it does |
|----------|--------------|-------------|
| `GroqAgentProvider` | Set `Groq:ApiKey` in appsettings | Real Llama 3 via Groq cloud — free, no credit card |
| `OllamaAgentProvider` | Install Ollama + `ollama pull llama3` | Real Llama 3 locally — free, no API key |
| `DemoAgentProvider` | Default (no setup) | Deterministic outputs with simulated ReAct tool-call traces |

**Priority:** Groq (if key set) → Ollama (if enabled) → Demo

All three demonstrate the ReAct tool-calling pattern. Groq and Ollama produce real LLM reasoning.

---

## Agent tool calling (ReAct loop)

Agents can call tools mid-reasoning. The orchestrator executes the tool and injects the result back before the agent produces its final answer.

Available tools:

| Tool | What it does |
|------|-------------|
| `estimate_complexity` | Estimates effort and complexity for a feature |
| `check_security` | Identifies security risks (open redirect, injection, auth surface) |
| `query_codebase` | Returns facts about existing modules and classes |
| `list_modules` | Lists all solution modules |
| `assess_risk` | Assesses deployment risk for a change |

Sample output from a task (visible in every agent response):

```
[ReAct] TOOL_CALL: check_security | url redirect
[ReAct] TOOL_RESULT: SECURITY_RISKS: Open redirect risk — validate and whitelist redirect targets
[ReAct] TOOL_CALL: assess_risk | database schema change
[ReAct] TOOL_RESULT: RISK: HIGH — requires migration script and rollback plan
[ReAct] Reasoning: schema risk is HIGH — flagging for approval gate.
[ReAct] Final Answer:
... (structured engineering output)
```

---

## Intelligent routing

After each task completes, `AgentOutputAnalyzer` scans the agent's output for risk signals.
The orchestrator reacts automatically — no human intervention required:

| Signal in agent output | Orchestrator action |
|------------------------|-------------------|
| `RISK: HIGH`, `BREAKING CHANGE`, `SCHEMA CHANGE` | Auto-pause for human approval |
| `CRITICAL VULNERABILITY`, `PII DETECTED`, `COMPLIANCE VIOLATION` | Block workflow immediately |
| No risk signals | Continue to next task |

This means the LLM's own reasoning directly influences execution flow.

---

## Orchestration lifecycle

```
requirements
      |
      v
   planning
      |
      v
  architecture
      |
      +------------------+
      v                  v
implementation      documentation
      |                  |
      +--------+---------+
               |
               v
            testing
               |
               v
           validation  <-- AgentOutputAnalyzer checks output here
               |
               v
      human approval  <-- workflow pauses here
               |
               v
            release
```

Parallel paths (implementation + documentation) execute concurrently via Task.WhenAll.
Every stage is gated by the policy engine AND the agent output analyzer before proceeding.

---

## Project structure

```
AgenticUrlShortner/
+-- UrlShortener.Api/              REST API + SSE streaming + Swagger
+-- UrlShortener.Application/      Application services and interfaces
+-- UrlShortener.Domain/           Domain model -- UrlMapping entity
+-- UrlShortener.Infrastructure/   EF Core, SQLite, repositories
+-- UrlShortener.Orchestration/    Agentic orchestration engine
|   +-- Agents/                    IAgentProvider, GroqAgentProvider, OllamaAgentProvider, DemoAgentProvider, AgentTools
|   +-- Models/                    WorkflowContext, WorkflowTask, WorkflowMetrics
|   +-- Policies/                  PolicyEngine, SecurityPolicy, ChangeControlPolicy, CompliancePolicy
|   +-- Services/                  WorkflowOrchestrator, RollbackService, DynamicReplanner,
|                                  AgentOutputAnalyzer, SqliteWorkflowStore, InMemoryWorkflowStore
+-- UrlShortener.UnitTests/        57 tests -- orchestration, policy, metrics, rollback, re-planning,
|                                  agent tools, output analyzer
+-- UrlShortener.IntegrationTests/ End-to-end API tests
+-- architecture.md
+-- orchestration.md
+-- scenarios.md
+-- setup.md
+-- testing.md
+-- engineering-summary.md
```

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

**Step 2 — Stream live events (open in a second terminal)**

```bash
curl -N https://localhost:7212/api/workflows/{workflowId}/stream
```

Events pushed in real time: `audit`, `task_output`, `status`, `approval_required`, `done`.

**Step 3 — Check workflow status**

```http
GET /api/workflows/{workflowId}
```

**Step 4 — Approve the workflow**

```http
POST /api/workflows/{workflowId}/approve
```

**Step 5 — View reliability metrics**

```http
GET /api/workflows/{workflowId}/metrics
```

**Step 6 — Stop a workflow**

```http
POST /api/workflows/{workflowId}/stop
```

**Step 7 — Trigger dynamic re-planning**

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

---

## Three scenarios

| Scenario | Pass in request body | What it demonstrates |
|----------|---------------------|----------------------|
| Greenfield | `"scenario": "Greenfield"` | New system built from scratch |
| Brownfield | `"scenario": "Brownfield"` | Enhancement with codebase impact analysis |
| Ambiguous | `"scenario": "Ambiguous"` | Under-specified requirement with clarification gate |

---

## Key API endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/workflows` | Start a new workflow |
| GET | `/api/workflows/{workflowId}` | Get workflow state |
| GET | `/api/workflows/{workflowId}/stream` | Live SSE event stream |
| POST | `/api/workflows/{workflowId}/approve` | Approve and resume workflow |
| POST | `/api/workflows/{workflowId}/stop` | Stop a workflow |
| POST | `/api/workflows/{workflowId}/replan` | Trigger dynamic re-planning |
| GET | `/api/workflows/{workflowId}/metrics` | Reliability metrics and audit trail |
| POST | `/api/urls` | Create a short URL |
| GET | `/api/urls/{shortCode}` | Resolve a short URL |
| GET | `/api/urls/{shortCode}/analytics` | Get click analytics |
| DELETE | `/api/urls/{shortCode}` | Delete a short URL |
| GET | `/api/health` | Health check |

---

## Reliability metrics

`GET /api/workflows/{id}/metrics` returns:

```json
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
  "auditTrail": ["..."]
}
```

---

## Policy guardrails

Every task is evaluated by three policies before execution:

| Policy | What it checks | Action on violation |
|--------|---------------|---------------------|
| SecurityPolicy | SQL injection, XSS, dangerous patterns | Deny — workflow halted |
| ChangeControlPolicy | High-impact stages without approval | Require approval |
| CompliancePolicy | PII patterns (SSN, credit card, password) | Deny — workflow halted |

---

## Workflow persistence

Workflows are stored in a SQLite database (`workflows.db`) and survive server restarts.
Configure the path in `appsettings.json`:

```json
"ConnectionStrings": {
  "WorkflowStore": "Data Source=workflows.db"
}
```

Set `"WorkflowStore": ""` to fall back to the in-memory store (no persistence).

---

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](AgenticUrlShortner/architecture.md) | System design, component diagram, key decisions |
| [Orchestration](AgenticUrlShortner/orchestration.md) | Lifecycle stages, dependency graph, governance model |
| [Scenarios](AgenticUrlShortner/scenarios.md) | Greenfield, Brownfield, Ambiguous walkthroughs |
| [Setup](AgenticUrlShortner/setup.md) | Prerequisites, build, run, Groq/Ollama setup |
| [Testing](AgenticUrlShortner/testing.md) | Strategy, limitations, trade-offs |
| [Engineering Summary](AgenticUrlShortner/engineering-summary.md) | Plan, artifacts, risks, assumptions |

---

## CI

[![CI](https://github.com/meenakshi999/agentic-url-shortner/actions/workflows/cd.yml/badge.svg)](https://github.com/meenakshi999/agentic-url-shortner/actions/workflows/cd.yml)

Build and test pipeline runs on every push and pull request to main.
