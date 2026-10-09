# Testing Approach, Limitations, and Trade-offs

## Testing Strategy

### Unit Tests (UrlShortener.UnitTests)

| Area | What is tested |
|------|----------------|
| WorkflowOrchestrator | Happy path completion, retry exhaustion, stop control, approval gate pause and resume |
| RollbackService | Downstream task invalidation on failure, transitive dependency rollback |
| DynamicReplanner | Output change detection, downstream invalidation, no-op when output unchanged |
| PolicyEngine | Security pattern blocking, change-control approval enforcement, compliance PII detection |
| WorkflowMetrics | SuccessRate, MTTR, RetryFrequency computed correctly from observed events |

### Integration Tests (UrlShortener.IntegrationTests)

| Area | What is tested |
|------|----------------|
| URL Shortener API | Create, resolve, expire, and deactivate short URLs end-to-end |
| Workflow API | Full greenfield, brownfield, and ambiguous scenario runs via HTTP |
| Approval flow | Pause then approve then resume produces correct final workflow state |
| Re-plan flow | Output change invalidates and re-executes downstream tasks correctly |
| Policy enforcement | Requests with dangerous patterns are blocked before task execution |

### How to Trigger Each Path

| Path | How to trigger |
|------|----------------|
| Happy path | POST /api/workflow with simulateFailure: false |
| Retry path | POST /api/workflow with simulateFailure: true |
| Rollback path | simulateFailure: true — exhausts retries, triggers downstream rollback |
| Approval gate | approvalGranted: false — workflow pauses at high-impact stage |
| Re-plan path | POST /api/workflow/{id}/replan with a changed task output |
| Safe stop | POST /api/workflow/{id}/stop at any point during execution |
| Policy block | Include a dangerous pattern such as DROP TABLE in the requirement text |

## Limitations

1. DemoAgentProvider returns deterministic canned outputs — agent reasoning is simulated,
   not live AI. Output realism is limited by the quality of the pre-written responses.
   Swap IAgentProvider for ClaudeAgentProvider to enable genuine AI reasoning.

2. In-memory workflow store — state does not survive process restart. A production
   deployment would replace InMemoryWorkflowStore with a database-backed implementation
   using the existing EF Core and SQLite infrastructure already in the solution.

3. Single-process parallel execution — parallel tasks use Task.WhenAll within one process.
   A production system would use a distributed task queue such as Azure Service Bus or
   Hangfire for true horizontal scale and fault isolation.

4. Policy rules are hardcoded — SecurityPolicy, CompliancePolicy, and ChangeControlPolicy
   use static pattern lists. A production PolicyEngine would load rules from a
   configuration store and support runtime updates without redeployment.

5. MTTR is per-task scoped — measures time from first failure to recovery via retry within
   a single task. Cross-workflow recovery time would require persistent failure event
   storage beyond the current in-memory model.

6. No replan cycle limit — DynamicReplanner has no cap on how many times a workflow can
   be replanned. A production system would enforce a maximum replan count per workflow.

## Trade-offs

| Decision | Chosen approach | Alternative | Reason for choice |
|----------|----------------|-------------|-------------------|
| Agent simulation | Deterministic DemoAgentProvider | Live LLM API | Reproducible, testable, zero API cost for prototype |
| Workflow store | In-memory | SQLite via EF Core | Zero infrastructure overhead for demonstration |
| Policy evaluation | Synchronous in-process | Async external rules engine | Simple and predictable; sufficient for prototype scope |
| Re-planning | Full plan recreation | Incremental diff | Simpler to reason about; correctness over efficiency |
| Retry limit | MaxRetries = 2 | Configurable per task | Prevents infinite loops; sufficient for prototype |
| Parallel execution | Task.WhenAll | Distributed queue | Demonstrates concurrency without infrastructure dependency |
