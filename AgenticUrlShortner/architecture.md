# Architecture Overview

## 1. System Goal

The system demonstrates a governed agentic software engineering workflow that transforms a software requirement into a reviewable engineering outcome.

The prototype combines:

- Requirement understanding
- Task decomposition
- Architecture reasoning
- Implementation
- Testing
- Validation
- Documentation
- Human approval
- Release readiness

The key design principle is that AI agents provide reasoning and engineering outputs, while a deterministic orchestration layer controls execution, dependencies, retries, approvals and safety boundaries.

---

## 2. Architecture

```text
                         +----------------------+
                         |      REST API        |
                         |  UrlShortener.Api    |
                         +----------+-----------+
                                    |
                    +---------------+----------------+
                    |                                |
                    v                                v
          +-------------------+             +----------------------+
          | URL Shortener     |             | Workflow             |
          | Application       |             | Orchestrator         |
          +---------+---------+             +----------+-----------+
                    |                                  |
                    v                                  v
          +-------------------+             +----------------------+
          | Domain            |             | Agent Provider        |
          | UrlMapping        |             | Demo / AI abstraction |
          +-------------------+             +----------------------+
                    |
                    v
          +-------------------+
          | Infrastructure    |
          | EF Core + SQLite  |
          +-------------------+