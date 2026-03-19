# ADR-001: Modular Monolith Architecture

## Status
Accepted

## Date
2026-03-19

## Context
We need to choose an architecture style for Nexora, an enterprise platform with 14+ business modules (CRM, Donations, Sponsorship, Education, etc.). The team is small (starting), and the module boundaries are not yet fully validated by production usage.

Microservices offer independent deployment and scaling, but at the cost of significant operational complexity (service discovery, distributed transactions, network latency, debugging difficulty). Starting with microservices when the team is small and boundaries are unclear often leads to a "distributed monolith" — the worst of both worlds.

## Decision
We will build Nexora as a **Modular Monolith** with the following characteristics:

1. **Single deployable unit** — one ASP.NET application hosting all modules
2. **Strong module boundaries** — each module is a separate .NET project with its own Domain, Application, Infrastructure, and API layers
3. **No direct cross-module references** — modules communicate via:
   - MediatR notifications (domain events, in-process)
   - Shared interfaces defined in SharedKernel
   - Integration events via Dapr pub/sub (Kafka)
4. **Independent database schemas** — each module owns its tables, accessed only through its own DbContext
5. **Module registration** — modules register themselves via a `IModule` interface, making them pluggable
6. **Architecture tests** — automated tests enforce module boundary rules (no circular dependencies, no direct cross-module access)

### Evolutionary Path
When a module needs independent scaling or a different deployment cadence, it can be extracted into a separate service with minimal code changes because:
- Dapr service invocation abstracts in-process vs. network calls
- Kafka events already decouple producers from consumers
- Each module has its own DbContext (no shared tables across modules)

## Consequences

### Positive
- Simpler deployment and operations (one service to monitor, deploy, debug)
- Lower latency for cross-module communication (in-process vs. network)
- Easier refactoring of module boundaries (rename/move within solution)
- Single transaction across modules when needed (without distributed transactions)
- Faster development velocity with small team
- Module boundaries are enforced by architecture tests, not network isolation

### Negative
- Cannot scale modules independently (must scale the whole application)
- One module's crash can bring down the entire application
- Technology lock-in (all modules must use .NET)
- Must be disciplined about module boundaries (no shortcuts through shared DB)

### Risks
- Team might take shortcuts and couple modules tightly; mitigated by architecture tests
- Performance bottleneck in one module affects all; mitigated by async processing via Kafka

## Alternatives Considered

| Alternative | Pros | Cons | Why Rejected |
|------------|------|------|-------------|
| Microservices from start | Independent scaling, tech diversity | Operational overhead, distributed debugging, team too small | Premature for current team size and module maturity |
| Traditional monolith | Simple | No module boundaries, hard to evolve | Doesn't support pluggable module architecture |
| Serverless (Functions) | Auto-scaling, pay-per-use | Cold starts, vendor lock-in, complex orchestration | Not suitable for stateful business logic and complex workflows |
