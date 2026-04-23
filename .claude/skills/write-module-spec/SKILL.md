---
name: write-module-spec
description: Write or update a module specification doc under docs/modules/{module}/ with all mandatory Mermaid diagrams (ER, state, sequence, component, integration) per DOCUMENTATION_STANDARDS.md. Use when the user asks to "document module X", "write module spec", or starts a new module.
---

# Write Module Spec

Every module under `docs/modules/{module}/` needs a complete spec. All diagrams are **Mermaid, embedded inline**.

## Minimum files
```
docs/modules/{module}/
├── README.md              # Overview, scope, boundaries, owner
├── DOMAIN.md              # ER diagram + entity descriptions + invariants
├── WORKFLOWS.md           # State diagrams + sequence diagrams for key flows
├── ARCHITECTURE.md        # Component diagram + integration diagram
└── API.md                 # Endpoint list (or link to auto-generated docs)
```

## Required diagrams (all Mermaid)
1. **ER diagram** (`erDiagram`) — all entities, keys, cardinality.
2. **State diagram** (`stateDiagram-v2`) — per stateful entity (e.g., lead, donation, subscription).
3. **Sequence diagram** (`sequenceDiagram`) — for each cross-boundary flow (user → API → module → external service).
4. **Component diagram** (`flowchart` or `C4`) — module's internal Domain/Application/Infrastructure/Api layers.
5. **Integration diagram** — cross-module events (Kafka/Dapr pub/sub) and SharedKernel interfaces consumed.

## Content rules
- State the module's **bounded context** clearly — what it owns, what it does NOT own.
- List every integration event published/consumed with payload schema.
- List every permission: `{module}.{resource}.{action}`.
- List every lockey namespace used (`lockey_{module}_*`).
- List recurring Hangfire jobs with queue + schedule.
- Cache keys used: `{module}:{entity}:{identifier}` (tenant prefix is auto).
- Secrets required: `nexora/{category}/{name}`.

## Update triggers
Bump the spec whenever:
- A new entity, endpoint, event, or job is added.
- A state machine changes.
- Module dependencies change (new SharedKernel interface, new pub/sub topic).

## Verification
- All diagrams render in GitHub preview (use VS Code Mermaid preview).
- Every entity in ER diagram exists in `Domain/`.
- Every event in integration diagram matches a real pub/sub topic.
- Link the spec from `docs/modules/README.md` index (if present).
