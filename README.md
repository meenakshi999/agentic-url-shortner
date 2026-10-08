# Agentic URL Shortener

A production-minded prototype demonstrating a governed agentic software engineering system for transforming software requirements into reviewable engineering outcomes.

The system demonstrates:

- Requirement understanding
- Task decomposition
- Dependency-aware workflow orchestration
- Architecture reasoning
- Implementation
- Testing
- Validation
- Human approval
- Release readiness
- Bounded retries
- Safe-stop controls
- Auditability
- Scenario-aware planning

The implementation uses deterministic orchestration around an agent abstraction so that the prototype can run without requiring an external AI API key.

---

# 1. Solution Overview

The system contains two related capabilities.

## URL Shortener

A working REST API providing:

- Create short URLs
- Resolve short URLs
- Click analytics
- URL expiration
- URL deactivation
- Health endpoint

## Agentic Engineering Workflow

A governed workflow that transforms a natural-language requirement into a structured engineering execution plan.

The workflow coordinates:

```text
Requirement
    ↓
Requirement Analysis
    ↓
Planning
    ↓
Architecture
    ↓
+-------------------------+
|                         |
Implementation       Documentation
    |
    ↓
Testing
    ↓
Validation
    ↓
Human Approval
    ↓
Release Readiness
    ↓
Completed