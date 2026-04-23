# NNNN — <Short decision title>

## Status

Proposed  <!-- Proposed | Accepted | Superseded by NNNN -->

## Date

YYYY-MM-DD

## Context

What problem are we solving? What forces, constraints, or prior decisions set the stage? Keep it
factual — describe the situation, not the solution. Reference earlier ADRs, specs, or incidents
that led to this decision.

## Decision drivers

- Driver 1 (e.g., "must preserve tenant isolation")
- Driver 2 (e.g., "must not increase p99 latency by > 10 ms")
- Driver 3 (...)

## Considered options

List at least two. Rejected options matter as much as the chosen one.

1. **Option A — <name>**
   - Summary
   - Pros
   - Cons

2. **Option B — <name>**
   - Summary
   - Pros
   - Cons

3. *(optional)* **Option C — <name>**

## Decision outcome

State the chosen option clearly in one or two sentences. Then explain **why** — tie it back to
the decision drivers and to the trade-offs of the rejected options.

## Consequences

### Positive

- What gets better, easier, faster, safer.

### Negative

- What gets worse, harder, more expensive. Include operational cost (monitoring, on-call),
  migration cost, and any locked-in assumptions.

### Neutral

- Side effects that are neither wins nor losses but worth recording (new tooling, renamed
  concept, etc.).

## Implementation notes

Concrete pointers for implementers:

- Packages / modules affected
- Migration or rollout plan (feature flag? staged? fleet-wide?)
- Observability: new metrics, logs, dashboards, alerts
- Testing: what a reviewer should check

## References

- Related ADRs (links)
- Module specs, standards docs, RFC links, issue numbers
- External resources (blog posts, papers) that influenced the decision
