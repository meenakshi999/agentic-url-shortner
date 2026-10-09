using UrlShortener.Orchestration.Models;

namespace UrlShortener.Orchestration.Agents;

public sealed class DemoAgentProvider : IAgentProvider
{
    public Task<AgentResult> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SimulateFailure &&
            request.TaskName == "Execute unit and integration validation")
        {
            return Task.FromResult(new AgentResult(
                Success: false,
                Output: string.Empty,
                Error: "Simulated validation failure: integration test suite reported 3 failures " +
                       "in UrlResolutionService under high-concurrency load. Retry scheduled."));
        }

        var output = request.TaskName switch
        {
            "Analyze and normalize requirement" =>
                BuildRequirementOutput(request.Scenario),

            "Identify ambiguity, assumptions and required clarifications" =>
                BuildAmbiguityOutput(request.Scenario),

            "Analyze existing codebase and impacted modules" =>
                BuildBrownfieldAnalysisOutput(),

            "Decompose requirement into engineering tasks" =>
                BuildPlanningOutput(request.Scenario),

            "Produce architecture and design decisions" =>
                BuildArchitectureOutput(request.Scenario),

            "Implement required changes" =>
                BuildImplementationOutput(request.Scenario),

            "Generate engineering documentation" =>
                BuildDocumentationOutput(),

            "Execute unit and integration validation" =>
                BuildValidationOutput(),

            "Validate outputs, risks and quality gates" =>
                BuildRiskOutput(request.Scenario),

            "Human approval before release" =>
                "APPROVAL GATE — Awaiting explicit human sign-off. " +
                "Summary prepared for reviewer: implementation complete, 94 tests passing, " +
                "0 critical risks open, documentation up to date. " +
                "Deployment blocked until ApprovalGranted is set by an authorised reviewer.",

            "Verify release readiness" =>
                BuildReleaseReadinessOutput(),

            _ =>
                $"Task '{request.TaskName}' completed. " +
                $"Scenario: {request.Scenario}. No specialised handler — generic completion recorded."
        };

        return Task.FromResult(new AgentResult(Success: true, Output: output));
    }

    // -------------------------------------------------------------------------
    // Requirement Analysis
    // -------------------------------------------------------------------------

    private static string BuildRequirementOutput(WorkflowScenario scenario) => scenario switch
    {
        WorkflowScenario.Brownfield => """
            REQUIREMENT NORMALISED — Brownfield Enhancement

            Original request: extend an existing URL shortener service.

            Normalised scope:
            - Add click-analytics endpoint (GET /api/urls/{code}/analytics) returning total clicks,
              unique visitors, referrer breakdown, and time-series data grouped by day.
            - Add soft-delete / deactivation (PATCH /api/urls/{code}/deactivate) without purging history.
            - Extend UrlMapping entity with IsActive flag and DeactivatedAtUtc nullable timestamp.
            - Preserve all existing API contracts — no breaking changes to POST /api/urls or GET /{code}.

            Compatibility constraints identified:
            - Existing consumers rely on 301 redirect behaviour; must remain unchanged.
            - SQLite schema migration required; rollback script must be prepared.
            - Two downstream services consume the resolution endpoint; both must be regression-tested.

            Acceptance criteria:
            1. Deactivated URLs return HTTP 410 Gone, not 404.
            2. Analytics endpoint returns data within 200ms at p95 under 100 rps.
            3. All 47 existing tests continue to pass after migration.
            """,

        WorkflowScenario.Ambiguous => """
            REQUIREMENT NORMALISED — Ambiguous Request

            Original request: "make the URL shortener more secure".

            Ambiguity detected — request does not specify which security dimension to address.
            Possible interpretations ranked by risk exposure:

            1. Input validation — reject malicious or malformed URLs at ingestion (HIGH priority)
            2. Rate limiting — prevent abuse via repeated shortening or resolution calls (HIGH)
            3. Authentication — restrict shortening to authenticated users only (MEDIUM)
            4. Audit logging — record who created/resolved/deactivated each URL (MEDIUM)
            5. URL scanning — check destination URLs against threat intelligence feeds (LOW, high cost)

            Normalised scope (pending human approval of interpretation):
            - Implement interpretations 1 and 2 in this iteration.
            - Interpretations 3–5 deferred to backlog with documented rationale.

            Clarifications required before implementation:
            - Is anonymous shortening to be blocked or just rate-limited?
            - What rate limit threshold is acceptable (suggested: 10 creates/minute per IP)?
            - Is audit log PII-sensitive (requires GDPR review)?
            """,

        _ => """
            REQUIREMENT NORMALISED — Greenfield System

            Original request: build a URL shortener service.

            Normalised capabilities:
            1. URL ingestion — accept a valid long URL, validate format, generate a unique short code.
            2. Resolution — resolve a short code to the original URL with redirect (HTTP 301).
            3. Expiration — support optional TTL per URL; expired URLs return HTTP 410 Gone.
            4. Deactivation — allow explicit deactivation without deleting history.
            5. Click analytics — track total clicks, unique IPs, and referrer per short URL.
            6. Health check — expose GET /health for infrastructure monitoring.

            Out of scope for this iteration:
            - Custom vanity codes (deferred — requires uniqueness enforcement strategy)
            - Multi-tenant isolation (deferred — single-tenant sufficient for prototype)
            - Real-time analytics streaming (deferred — batch analytics sufficient)

            Non-functional targets:
            - Resolution latency: < 50ms p99
            - Availability: 99.9% (single-region)
            - Short code collision probability: < 0.001% at 1M URLs
            """
    };

    // -------------------------------------------------------------------------
    // Ambiguity Analysis
    // -------------------------------------------------------------------------

    private static string BuildAmbiguityOutput(WorkflowScenario scenario) => scenario switch
    {
        WorkflowScenario.Ambiguous => """
            AMBIGUITY ANALYSIS

            Requirement: "make the URL shortener more secure" — under-specified.

            Identified ambiguities:
            1. SCOPE — "secure" spans input validation, auth, rate limiting, audit, and threat intel.
               Each has different cost, complexity, and user-impact trade-offs.

            2. AUDIENCE — unclear whether shortening should remain public or become authenticated.
               Blocking anonymous access eliminates a significant abuse vector but breaks public use cases.

            3. RATE LIMITS — no threshold specified. Too low = false positives; too high = ineffective.

            4. AUDIT LOGGING — if logs contain creator IP or identity, GDPR Article 30 applies.
               A Data Protection Impact Assessment may be required before implementation.

            5. THREAT INTEL SCANNING — feasible but introduces a third-party dependency and latency.
               Not recommended for this iteration without SLA guarantees from the feed provider.

            Recommended clarifications (must be resolved before implementation):
            - Q1: Should unauthenticated URL shortening be blocked or rate-limited?
            - Q2: Agreed rate limit: 10 creates/minute per IP — confirm or adjust.
            - Q3: Is audit log data subject to GDPR? If yes, DPA review required.

            Assumptions recorded (approved by default if human approval granted):
            - A1: Anonymous shortening allowed, rate-limited to 10/min per IP.
            - A2: Audit logs store IP only, not user identity — no GDPR trigger.
            - A3: URL threat scanning deferred to next iteration.
            """,

        WorkflowScenario.Brownfield => """
            AMBIGUITY ANALYSIS — Brownfield

            Requirement is well-scoped. Minor ambiguities identified:

            1. MIGRATION STRATEGY — ALTER TABLE vs recreate: SQLite does not support column drops.
               Decision required: use a shadow table migration or add nullable column only.
               Recommendation: add nullable IsActive BIT column with DEFAULT 1 (non-breaking).

            2. DEACTIVATION SEMANTICS — should deactivated URLs return 410 Gone or 404 Not Found?
               RFC 7231 §6.5.9 recommends 410 for intentionally removed resources.
               Recommendation: HTTP 410 Gone.

            3. ANALYTICS RETENTION — how long should click data be retained?
               No regulatory requirement identified; recommend 90-day rolling window as default.

            Assumptions recorded:
            - A1: Migration adds nullable columns only — no destructive schema change.
            - A2: Deactivated URLs return HTTP 410 Gone.
            - A3: Analytics retention default = 90 days; configurable via appsettings.
            """,

        _ => """
            AMBIGUITY ANALYSIS — Greenfield

            Requirement is well-defined. Ambiguities are low-risk:

            1. SHORT CODE LENGTH — not specified. Recommendation: 7 characters (Base62) gives
               3.5 trillion combinations — effectively collision-free below 100M URLs.

            2. REDIRECT TYPE — 301 (permanent) vs 302 (temporary).
               301 is cached by browsers, reducing server load. Recommended for stable URLs.
               302 required if click tracking must capture every hit. Decision: 302 for analytics accuracy.

            3. COLLISION HANDLING — if generated code already exists, retry with a new code.
               Max 3 retries before returning HTTP 503. Probability of 3 consecutive collisions
               at 1M URLs is < 0.000001% — acceptable.

            Assumptions recorded:
            - A1: Short codes are 7 characters, Base62 alphabet.
            - A2: Resolution uses HTTP 302 to enable per-hit click tracking.
            - A3: Collision retry limit = 3; beyond that return 503 Service Unavailable.
            """
    };

    // -------------------------------------------------------------------------
    // Brownfield Codebase Analysis
    // -------------------------------------------------------------------------

    private static string BuildBrownfieldAnalysisOutput() => """
        CODEBASE ANALYSIS — Brownfield Impact Assessment

        Modules impacted:

        1. UrlShortener.Domain / UrlMapping.cs
           - Add: IsActive (bool, default true), DeactivatedAtUtc (DateTime?)
           - No existing properties removed or renamed — backward compatible.

        2. UrlShortener.Infrastructure / UrlMappingRepository.cs
           - GetByCodeAsync: add filter WHERE IsActive = 1 to resolution query.
           - AddDeactivateAsync: new method — sets IsActive = false, DeactivatedAtUtc = UtcNow.
           - Migration: 0002_AddIsActiveAndDeactivation.sql (adds two nullable columns).

        3. UrlShortener.Application / UrlShortenerService.cs
           - ResolveAsync: return null (→ 410 Gone) when IsActive = false.
           - DeactivateAsync: new method — validates code exists, delegates to repository.

        4. UrlShortener.Api / Controllers / UrlsController.cs
           - Add PATCH /api/urls/{code}/deactivate endpoint.
           - Add GET /api/urls/{code}/analytics endpoint.
           - Existing POST and GET/{code} endpoints: no changes required.

        5. UrlShortener.UnitTests + IntegrationTests
           - New test cases: deactivation returns 410, resolution of deactivated URL returns 410,
             analytics returns correct click count, migration runs clean on empty and populated DB.
           - Existing tests: no changes expected; all should remain green.

        Regression risk: LOW
        - No existing API contracts changed.
        - Schema change is additive (nullable columns with defaults).
        - Two downstream consumers: confirm with consumer teams before deploying.
        """;

    // -------------------------------------------------------------------------
    // Planning / Task Decomposition
    // -------------------------------------------------------------------------

    private static string BuildPlanningOutput(WorkflowScenario scenario) => scenario switch
    {
        WorkflowScenario.Brownfield => """
            TASK DECOMPOSITION — Brownfield

            Dependency-ordered task list:

            [T1] Schema migration — add IsActive, DeactivatedAtUtc columns to UrlMappings table.
                 Exit gate: migration runs clean; rollback script verified.

            [T2] Domain model update — extend UrlMapping entity with new fields.
                 Depends on: T1. Exit gate: no compilation errors; existing unit tests green.

            [T3] Repository changes — update GetByCodeAsync filter; add DeactivateAsync.
                 Depends on: T2. Exit gate: repository integration tests pass.

            [T4] Application service — add DeactivateAsync; update ResolveAsync for 410 case.
                 Depends on: T3. Exit gate: service unit tests pass including 410 path.

            [T5] API endpoints — PATCH /deactivate, GET /analytics.
                 Depends on: T4. Exit gate: Swagger docs updated; HTTP 410 and 200 responses verified.

            [T6] Test coverage — new unit + integration tests for deactivation and analytics.
                 Depends on: T5. Exit gate: all 47 + N new tests pass; coverage >= 80%.

            [T7] Documentation — update API docs, migration runbook, rollback procedure.
                 Depends on: T6. Exit gate: docs reviewed and merged.

            [T8] Human approval — reviewer sign-off before deployment.
                 Depends on: T7. Exit gate: ApprovalGranted = true.

            [T9] Release readiness — deployment checklist, smoke test, rollback verified.
                 Depends on: T8. Exit gate: smoke test passes in staging.
            """,

        WorkflowScenario.Ambiguous => """
            TASK DECOMPOSITION — Ambiguous Requirement

            Note: scope locked to interpretations 1 (input validation) and 2 (rate limiting)
            per requirement normalisation. All other interpretations deferred.

            [T1] Input validation — reject URLs failing RFC 3986 format check; block private IP ranges.
                 Exit gate: validation unit tests cover 20+ malformed and malicious URL patterns.

            [T2] Rate limiting middleware — sliding window, 10 creates/minute per IP.
                 Depends on: T1. Exit gate: load test confirms 429 returned at threshold.

            [T3] Audit log — record code, creator IP, timestamp on every create and resolve.
                 Depends on: T2. Exit gate: audit entries persisted; no PII beyond IP stored.

            [T4] Integration — wire validation and rate limiter into request pipeline.
                 Depends on: T1, T2, T3. Exit gate: end-to-end request with malicious URL rejected.

            [T5] Security test coverage — automated tests for each attack vector identified.
                 Depends on: T4. Exit gate: all security test cases pass.

            [T6] Human approval — security-sensitive change requires explicit sign-off.
                 Depends on: T5. Exit gate: security reviewer approves.

            [T7] Release readiness — staging deployment, penetration test summary, rollback plan.
                 Depends on: T6. Exit gate: staging smoke test passes; pentest findings triaged.
            """,

        _ => """
            TASK DECOMPOSITION — Greenfield

            Dependency-ordered task list:

            [T1] Domain model — UrlMapping entity: Id, LongUrl, ShortCode, CreatedAtUtc,
                 ExpiresAtUtc (nullable), IsActive, ClickCount.
                 Exit gate: entity compiles; no EF Core mapping errors.

            [T2] Persistence — EF Core DbContext, SQLite provider, initial migration.
                 Depends on: T1. Exit gate: migration applies cleanly; seed data resolves correctly.

            [T3] Short code generation — Base62, 7 characters, collision retry (max 3).
                 Depends on: T1. Exit gate: 10,000 generated codes have zero collisions.

            [T4] Application services — ShortenAsync, ResolveAsync, DeactivateAsync, GetAnalyticsAsync.
                 Depends on: T2, T3. Exit gate: service unit tests pass all happy and error paths.

            [T5] REST API — POST /api/urls, GET /{code}, PATCH /api/urls/{code}/deactivate,
                 GET /api/urls/{code}/analytics, GET /health.
                 Depends on: T4. Exit gate: Swagger UI shows all endpoints; integration tests green.

            [T6] Agentic orchestration layer — workflow engine, agent abstraction, policy engine,
                 metrics, rollback, dynamic re-planning.
                 Depends on: T5. Exit gate: all three scenario workflows complete end-to-end.

            [T7] Test coverage — unit + integration tests; coverage >= 80%.
                 Depends on: T5, T6. Exit gate: all tests pass in CI.

            [T8] Documentation — architecture, orchestration, setup, testing, engineering summary.
                 Depends on: T7. Exit gate: all docs present and accurate.

            [T9] Human approval. Depends on: T8.

            [T10] Release readiness — CI green, Swagger verified, health check passing.
                  Depends on: T9.
            """
    };

    // -------------------------------------------------------------------------
    // Architecture
    // -------------------------------------------------------------------------

    private static string BuildArchitectureOutput(WorkflowScenario scenario) => scenario switch
    {
        WorkflowScenario.Brownfield => """
            ARCHITECTURE DECISIONS — Brownfield

            Decision 1: Additive schema migration only.
            Rationale: SQLite does not support DROP COLUMN. Adding nullable columns with defaults
            is the only non-destructive path. All existing queries remain valid.
            Trade-off: schema carries historical nullable columns; acceptable for prototype scope.

            Decision 2: Deactivation as soft delete, not hard delete.
            Rationale: analytics and audit history must be preserved after deactivation.
            Hard delete would destroy click records. Soft delete with IsActive flag preserves lineage.

            Decision 3: 410 Gone for deactivated URLs.
            Rationale: RFC 7231 §6.5.9 — 410 signals intentional, permanent removal.
            Browsers and CDNs do not cache 410 indefinitely, unlike 301.

            Decision 4: No changes to existing API contracts.
            Rationale: two known downstream consumers. Breaking changes require coordinated release.
            All new functionality is additive (new endpoints, new optional fields).

            Decision 5: Analytics stored in same SQLite DB as UrlMappings.
            Rationale: reduces infrastructure complexity for prototype.
            Migration path to a dedicated time-series store (InfluxDB, TimescaleDB) is documented.
            """,

        WorkflowScenario.Ambiguous => """
            ARCHITECTURE DECISIONS — Security Enhancement

            Decision 1: Input validation at the API boundary, not the service layer.
            Rationale: fail fast — reject malformed requests before they touch business logic.
            Implementation: FluentValidation middleware on POST /api/urls.

            Decision 2: Sliding window rate limiter using in-memory token bucket per IP.
            Rationale: simple, zero infrastructure overhead for prototype.
            Trade-off: rate limit state lost on restart; distributed deployments need Redis-backed store.
            Production path: swap IpRateLimiter implementation for Redis-backed version.

            Decision 3: Audit log appended to existing SQLite DB.
            Rationale: no new infrastructure. AuditLog table: Id, Event, ShortCode, IpAddress, Timestamp.
            PII boundary: IP address only — no user identity stored.

            Decision 4: Defence in depth — validation, rate limiting, and audit are independent layers.
            Rationale: each layer fails independently. A rate limiter bug does not disable input validation.

            Decision 5: No authentication in this iteration.
            Rationale: authentication changes the public API contract. Deferred pending product decision
            on whether shortening remains public. Documented as known gap.
            """,

        _ => """
            ARCHITECTURE DECISIONS — Greenfield

            Decision 1: Vertical slice — Clean Architecture with four layers.
            Domain → Application → Infrastructure → API.
            Rationale: clear dependency direction; each layer is independently testable.

            Decision 2: SQLite with EF Core for persistence.
            Rationale: zero-infrastructure prototype. EF Core abstracts the provider;
            swapping to PostgreSQL or SQL Server requires only a connection string and provider package.

            Decision 3: Base62 short codes, 7 characters.
            Rationale: 62^7 = 3.5 trillion combinations. At 1M URLs the collision probability
            is ~0.014% per attempt — acceptable with a 3-retry ceiling.

            Decision 4: HTTP 302 redirect for resolution.
            Rationale: 301 is browser-cached, preventing click tracking on repeat visits.
            302 ensures every resolution hits the server, enabling accurate analytics.

            Decision 5: Agentic orchestration as a separate project (UrlShortener.Orchestration).
            Rationale: decouples the SDLC automation layer from the URL shortener domain.
            IAgentProvider abstraction allows swapping DemoAgentProvider for a live LLM provider
            with no changes to orchestration logic.

            Decision 6: Policy engine runs before every task execution.
            Rationale: security and compliance checks must not be bypassable by task ordering.
            A deny at any point halts the workflow with a full audit trail.
            """
    };

    // -------------------------------------------------------------------------
    // Implementation
    // -------------------------------------------------------------------------

    private static string BuildImplementationOutput(WorkflowScenario scenario) => scenario switch
    {
        WorkflowScenario.Brownfield => """
            IMPLEMENTATION COMPLETE — Brownfield

            Changes delivered:

            UrlShortener.Domain/UrlMapping.cs
            + IsActive: bool = true
            + DeactivatedAtUtc: DateTime?

            UrlShortener.Infrastructure/Migrations/0002_AddIsActiveAndDeactivation.sql
            + ALTER TABLE UrlMappings ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1
            + ALTER TABLE UrlMappings ADD COLUMN DeactivatedAtUtc TEXT NULL
            + Rollback script: 0002_Rollback.sql prepared and verified.

            UrlShortener.Infrastructure/Repositories/UrlMappingRepository.cs
            + GetByCodeAsync: WHERE IsActive = 1 added to resolution query.
            + DeactivateAsync(string code): sets IsActive = 0, DeactivatedAtUtc = UtcNow.

            UrlShortener.Application/Services/UrlShortenerService.cs
            + ResolveAsync: returns null when IsActive = false (caller maps to 410 Gone).
            + DeactivateAsync: validates code exists, delegates to repository.

            UrlShortener.Api/Controllers/UrlsController.cs
            + PATCH /api/urls/{code}/deactivate → 204 No Content on success, 404 if not found.
            + GET /api/urls/{code}/analytics → 200 with AnalyticsDto, 404 if not found.
            + GET /{code}: now returns 410 Gone for deactivated URLs (was returning 404).

            All changes are backward compatible. Existing 47 tests remain green.
            """,

        WorkflowScenario.Ambiguous => """
            IMPLEMENTATION COMPLETE — Security Enhancement

            Changes delivered:

            UrlShortener.Api/Validation/UrlCreateRequestValidator.cs (new)
            + Validates URL format against RFC 3986.
            + Rejects private IP ranges (10.x, 192.168.x, 127.x, ::1).
            + Rejects javascript: and data: URI schemes.
            + Returns 400 Bad Request with structured error detail.

            UrlShortener.Api/Middleware/IpRateLimiterMiddleware.cs (new)
            + Sliding window: 10 POST /api/urls requests per IP per 60 seconds.
            + Returns 429 Too Many Requests with Retry-After header.
            + In-memory token bucket; documented upgrade path to Redis for distributed deployments.

            UrlShortener.Infrastructure/AuditLog/AuditLogRepository.cs (new)
            + AuditLog table: Id, Event, ShortCode, IpAddress, OccurredAtUtc.
            + Events: UrlCreated, UrlResolved, UrlDeactivated, RateLimitExceeded.
            + No user identity stored — IP only.

            UrlShortener.Api/Program.cs
            + Middleware registered in order: RateLimit → Validate → Handle.
            + AuditLogRepository registered as scoped dependency.

            All existing 47 tests pass. 12 new security-focused tests added.
            """,

        _ => """
            IMPLEMENTATION COMPLETE — Greenfield

            All planned components delivered:

            UrlShortener.Domain
            + UrlMapping entity with all planned fields.
            + Value object: ShortCode (encapsulates Base62 generation and validation).

            UrlShortener.Infrastructure
            + UrlShortenerDbContext with EF Core SQLite provider.
            + Migration 0001_Initial.sql applied and verified.
            + UrlMappingRepository implementing IUrlMappingRepository.

            UrlShortener.Application
            + UrlShortenerService: ShortenAsync, ResolveAsync, DeactivateAsync, GetAnalyticsAsync.
            + Interface-based — all dependencies injected; no static references.

            UrlShortener.Api
            + POST /api/urls — shorten a URL; returns 201 Created with Location header.
            + GET /{code} — resolve; 302 redirect on hit, 404 on miss, 410 on expired/deactivated.
            + PATCH /api/urls/{code}/deactivate — 204 No Content.
            + GET /api/urls/{code}/analytics — click count, created date, expiry.
            + GET /health — 200 OK with uptime and DB connectivity status.

            UrlShortener.Orchestration
            + Full agentic workflow engine with dependency graph, parallel execution,
              human approval gates, policy engine, rollback, dynamic re-planning, and metrics.

            94 tests total across unit and integration suites. All passing.
            """
    };

    // -------------------------------------------------------------------------
    // Documentation
    // -------------------------------------------------------------------------

    private static string BuildDocumentationOutput() => """
        DOCUMENTATION COMPLETE

        Artifacts produced:

        docs/architecture.md
        - System component diagram (ASCII)
        - Layer responsibilities and dependency direction
        - Key architecture decisions with rationale and trade-offs

        docs/orchestration.md
        - Agentic workflow lifecycle (10 stages)
        - Dependency graph (ASCII)
        - Governance model: agent autonomy boundaries, human approval gates

        docs/scenarios.md
        - Three scenarios: Greenfield, Brownfield, Ambiguous
        - Each scenario: requirement, decomposition, orchestration trace, validation result

        docs/setup.md
        - Prerequisites, build steps, run steps
        - Full API walkthrough with sample requests/responses
        - Scenario selection guide

        docs/testing.md
        - Testing strategy: unit, integration, scenario
        - How to trigger each execution path (happy, retry, rollback, policy block, re-plan)
        - Limitations and trade-offs

        docs/engineering-summary.md
        - Plan and rationale
        - Full artifact inventory
        - Risk register with likelihood, impact, and mitigation
        - Validation approach
        - Assumptions and limitations
        - Engineering judgment notes

        All documents cross-referenced and consistent with implementation.
        """;

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    private static string BuildValidationOutput() => """
        VALIDATION COMPLETE

        Unit test results: 47 tests, 47 passed, 0 failed, 0 skipped.
        Integration test results: 12 tests, 12 passed, 0 failed.
        New tests (rollback, re-planning, policy engine, metrics): 27 passed.
        Total: 86 tests, 86 passed.

        Functional checks:
        ✓ POST /api/urls creates a short URL and returns 201 with Location header.
        ✓ GET /{code} resolves to original URL with 302 redirect.
        ✓ Expired URL returns 410 Gone.
        ✓ Deactivated URL returns 410 Gone.
        ✓ Click count increments on each resolution.
        ✓ GET /health returns 200 with DB connectivity confirmed.

        Orchestration checks:
        ✓ Greenfield scenario completes all 10 stages end-to-end.
        ✓ Brownfield scenario identifies impacted modules and completes without regression.
        ✓ Ambiguous scenario pauses at clarification gate; resumes after approval.
        ✓ SimulateFailure triggers retry (x2) then rollback on permanent failure.
        ✓ Policy engine blocks dangerous patterns before execution.
        ✓ Dynamic re-planning invalidates downstream tasks on output change.

        Reliability metrics:
        - Success rate: 96.4% (task-level, including simulated failure scenario)
        - Mean retry count: 0.08 per task across non-simulated runs
        - MTTR: 142ms average across retry-recovery events
        - End-to-end latency: 38ms average per workflow (demo provider, in-process)

        No critical risks open. Ready for human approval gate.
        """;

    // -------------------------------------------------------------------------
    // Risk and Quality Gate
    // -------------------------------------------------------------------------

    private static string BuildRiskOutput(WorkflowScenario scenario) => scenario switch
    {
        WorkflowScenario.Ambiguous => """
            RISK AND QUALITY GATE — Ambiguous / Security Scenario

            Risks identified and mitigated:

            RISK-01: Rate limit bypass via IP spoofing (X-Forwarded-For header manipulation).
            Likelihood: MEDIUM. Impact: HIGH.
            Mitigation: read IP from connection RemoteIpAddress only; ignore X-Forwarded-For
            unless behind a trusted reverse proxy (configurable via TrustedProxies setting).
            Status: MITIGATED.

            RISK-02: In-memory rate limit state lost on restart — burst possible post-restart.
            Likelihood: HIGH. Impact: LOW (short window).
            Mitigation: documented; Redis-backed implementation path available.
            Accepted for prototype scope.

            RISK-03: Audit log growth unbounded.
            Likelihood: HIGH (long-running). Impact: MEDIUM.
            Mitigation: 90-day retention policy documented; cleanup job not yet implemented.
            Deferred to next iteration.

            RISK-04: URL validation does not perform DNS resolution — phishing URLs may pass.
            Likelihood: MEDIUM. Impact: MEDIUM.
            Mitigation: URL threat intelligence scanning documented as a deferred capability.
            Accepted for this iteration.

            Quality gates:
            ✓ All planned tasks completed.
            ✓ Security test cases pass (400 on malformed URL, 429 on rate limit breach).
            ✓ Audit log entries verified for all event types.
            ✓ No breaking changes to existing API.
            ✓ Documentation updated.
            """,

        _ => """
            RISK AND QUALITY GATE

            Risks identified and mitigated:

            RISK-01: Short code collision under high volume.
            Likelihood: LOW. Impact: MEDIUM.
            Mitigation: 3-retry ceiling with Base62 7-char codes — collision probability
            < 0.000001% at 3 consecutive retries at 1M URLs. 503 returned if all retries exhausted.
            Status: MITIGATED.

            RISK-02: SQLite write contention under concurrent load.
            Likelihood: MEDIUM. Impact: MEDIUM.
            Mitigation: EF Core provider abstraction allows migration to PostgreSQL.
            For prototype load levels (< 100 rps) SQLite WAL mode is sufficient.
            Status: ACCEPTED (documented upgrade path).

            RISK-03: Agent output not schema-validated before storage.
            Likelihood: LOW. Impact: LOW (demo provider outputs are deterministic).
            Mitigation: policy engine validates inputs; output validation hook documented
            in IAgentProvider contract for production use.
            Status: ACCEPTED for demo provider; required for live LLM provider.

            RISK-04: In-memory workflow store loses state on restart.
            Likelihood: HIGH. Impact: MEDIUM.
            Mitigation: IWorkflowStore abstraction in place; SQLite-backed store implementation
            path documented and EF Core infrastructure already present.
            Status: ACCEPTED for prototype scope.

            Quality gates:
            ✓ All planned tasks completed with no wrong-path findings.
            ✓ 86 tests passing across unit, integration, and scenario suites.
            ✓ Policy engine verified across all three policy types.
            ✓ Rollback and re-planning paths tested and passing.
            ✓ All required deliverables present in repository.
            ✓ No open critical risks.
            """
    };

    // -------------------------------------------------------------------------
    // Release Readiness
    // -------------------------------------------------------------------------

    private static string BuildReleaseReadinessOutput() => """
        RELEASE READINESS VERIFIED

        Pre-release checklist:

        ✓ Implementation complete and aligned with approved architecture.
        ✓ All 86 tests passing (unit, integration, orchestration).
        ✓ CI pipeline: build and test stages green.
        ✓ No open critical or high risks.
        ✓ API documentation (Swagger) up to date.
        ✓ All five required deliverable documents present in docs/.
        ✓ Three scenarios (Greenfield, Brownfield, Ambiguous) verified end-to-end.
        ✓ Human approval granted (ApprovalGranted = true recorded in audit trail).
        ✓ Health endpoint returning 200 with DB connectivity confirmed.
        ✓ Rollback procedure documented and migration rollback script verified.

        Deployment recommendation: APPROVED FOR RELEASE.

        Post-release monitoring:
        - Watch GET /{code} p99 latency — alert if > 100ms.
        - Watch workflow success rate — alert if < 90%.
        - Review audit trail for unexpected policy denials in first 24 hours.

        Known limitations accepted for this release:
        - In-memory workflow store (restart loses state).
        - Demo agent provider (no live LLM — swap IAgentProvider for production).
        - SQLite (single-node only — migrate to PostgreSQL for horizontal scale).
        """;
}
