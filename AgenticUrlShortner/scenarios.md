# Engineering Scenarios

The prototype demonstrates three different engineering contexts:

1. Greenfield
2. Brownfield
3. Ambiguous requirement

Each scenario uses the same governed orchestration engine but adapts its planning and reasoning to the context.

---

## 1. Greenfield Scenario

### Requirement

Build a URL shortener with:

- URL creation
- Short-code generation
- URL resolution
- Analytics
- Expiration
- Reliability
- Validation
- Documentation

### Requirement Understanding

The requirement is normalized into a service that must:

- Accept a valid HTTP/HTTPS URL.
- Generate a unique short code.
- Persist the mapping.
- Resolve the short code.
- Track click counts.
- Support optional expiration.
- Reject expired or inactive URLs.
- Provide analytics.

### Decomposition

The workflow identifies:

1. API design
2. Domain model
3. Persistence
4. URL generation
5. Expiration handling
6. Analytics
7. Validation
8. Testing
9. Documentation

### Orchestration

```text
Requirements
    ↓
Planning
    ↓
Architecture
    ↓
+----------------------+
|                      |
Implementation    Documentation
    |
    ↓
Testing
    ↓
Validation
    ↓
Human Approval
    ↓
Release Readiness