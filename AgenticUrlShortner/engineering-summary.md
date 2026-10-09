# Final Engineering Summary

## Plan and Rationale

The objective was to build a governed agentic software engineering system that transforms
a natural language requirement into a reviewable engineering outcome. The URL shortener
service is the domain vehicle — the primary demonstration is the orchestration layer that
governs the full SDLC lifecycle with controlled autonomy and human oversight.

Core design principle: agents generate reasoning and outputs; the deterministic
orchestration layer controls execution flow, enforces dependency ordering, manages retries
and rollbacks, evaluates policy guardrails, and gates human approval at high-impact stages.
No agent ever directly controls execution — the orchestrator owns that boundary.

## Artifacts Produced

| Artifact | Location | Description |
|----------|----------|-------------|
| URL Shortener API | UrlShortener.Api/ | REST API for create, resolve, expire, deactivate, analytics |
| Domain model | UrlShortener.Domain/ | UrlMapping entity with expiration and deactivation support |
| Application layer | UrlShortener.Application/ | Service interfaces and business logic |
| Infrastructure layer | UrlShortener.Infrastructure/ | EF Core and SQLite persistence |
| Workflow Orchestrator | UrlShortener.Orchestration/Services/WorkflowOrchestrator.cs | Stateful dependency-aware execution engine with parallel and sequential paths |
| Dynamic Replanner | UrlShortener.Orchestration/Services/DynamicReplanner.cs | Upstream change detection and transitive downstream invalidation |
| Rollback Service | UrlShortener.Orchestration/Services/RollbackService.cs | Downstream task rollback on permanent failure |
| Policy Engine | UrlShortener.Orchestration/Policies/PolicyEngine.cs | Security, compliance, and change-control guardrails |
| Workflow Metrics | UrlShortener.Orchestration/Models/WorkflowMetrics.cs | Success rate, MTTR, end-to-end latency, retry and rollback frequency |
| Agent abstraction | UrlShortener.Orchestration/Agents/IAgentProvider.cs | Pluggable agent interface — swap DemoAgentProvider for live LLM |
| Three scenarios | docs/scenarios.md | Greenfield, brownfield, ambiguous with decomposition and validation |
| Architecture overview | docs/architecture.md | Component diagram, control flow, key decisions |
| Orchestration model | docs/orchestration.md | Dependency graph, lifecycle stages, governance model |
| Setup instructions | docs/setup.md | Prerequisites, run steps, endpoint walkthrough |
| Testing approach | docs/testing.md | Strategy, limitations, trade-offs |

## Risks, Trade-offs, and Validation

### Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Agent produces malformed output | Medium | Medium | Output stored as string; policy engine validates before downstream use |
| Parallel tasks conflict on shared context | Low | High | Outputs keyed by task ID; no two tasks write the same key |
| Rollback leaves workflow in inconsistent state | Low | High | Rollback clears outputs and resets task state before re-queuing |
| Policy engine blocks valid high-impact tasks | Low | Medium | Change-control policy gates on ApprovalGranted flag; human can always approve |
| Re-plan loop caused by repeated output changes | Low | Medium | ReplanCount tracked in metrics; production would add a hard cap |
| In-memory state lost on restart | High | Medium | Acceptable for prototype; EF Core infrastructure is in place for persistence upgrade |

### Validation Approach

- Every task execution produces a timestamped audit entry with task ID and outcome
- Metrics are computed from observed events, not estimated values
- Human approval is enforced at two independent layers: orchestrator loop and ChangeControlPolicy
- SimulateFailure flag enables deterministic testing of retry, rollback, and recovery paths
- Policy evaluation runs before every task execution, not only at workflow start
- Dynamic re-planning verifies output has actually changed before invalidating downstream tasks

## Assumptions

1. The agent provider is trusted within its autonomy boundary — it returns outputs only,
   it cannot read or modify workflow context directly.

2. Requirements are provided in English natural language.

3. Human approvers are available within the same session — there is no persistent approval
   queue across restarts.

4. The URL shortener domain is the demonstration vehicle, not the primary deliverable.
   The orchestration layer is the engineering outcome being assessed.

5. DemoAgentProvider outputs are representative of what a real LLM would produce for each
   SDLC stage. The IAgentProvider abstraction allows a live provider to be substituted
   without changing any orchestration code.

## Limitations

1. Agent reasoning is simulated — DemoAgentProvider returns scenario-aware canned
   responses. Replace IAgentProvider with ClaudeAgentProvider to enable live AI reasoning
   with no changes to the orchestration layer.

2. Workflow state is in-memory — does not survive process restart. InMemoryWorkflowStore
   implements IWorkflowStore; a SQLite-backed store can be substituted using the EF Core
   infrastructure already present in the solution.

3. No distributed execution — parallel tasks run in-process via Task.WhenAll. A production
   system would use a distributed task queue for horizontal scale and fault isolation.

4. Metrics are per-workflow — there is no cross-workflow aggregation, trending, or
   alerting. A production system would emit metrics to a time-series store.

5. Policy rules are static — pattern lists are hardcoded. A production PolicyEngine would
   load rules dynamically from a configuration store.

6. No replan cycle limit — a runaway caller could trigger repeated replans. A production
   system would enforce a maximum replan count per workflow and alert on breach.

## Engineering Judgment Notes

- The IAgentProvider abstraction was the first design decision. It decouples orchestration
  from AI provider choice, making the system testable without API keys and upgradeable
  without touching orchestration code.

- Rollback uses a breadth-first graph traversal to find all transitive dependents, not
  just direct children. This is correct for a dependency graph where chains can be deep.

- Dynamic re-planning checks for actual output change before invalidating downstream work.
  A no-op replan request produces an audit entry but does not mutate workflow state.

- The PolicyEngine runs guards in sequence and stops at the first non-Allow result.
  This fail-fast approach is intentional — a Deny from SecurityPolicy should not be
  overridden by a subsequent Allow from CompliancePolicy.

- ExecuteAsync and ResumeAsync share a single RunTaskLoopAsync implementation. This was
  a deliberate refactor to eliminate duplication and ensure both paths behave identically
  under all conditions.
