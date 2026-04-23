# Code Style

Derives from: [ADR-001 Modular Monolith](../decisions/ADR-001-modular-monolith.md).
Source: CLAUDE.md §When Writing Code, §When Writing Frontend Code; legacy
`CODING_STANDARDS.md`, `FRONTEND_STANDARDS.md`, `API_INTEGRATION_STANDARDS.md`.

## 1. C# Conventions

### 1.1 Naming

| Element | Convention | Example |
|---------|------------|---------|
| Namespace | PascalCase | `Nexora.Modules.CRM.Domain` |
| Class / Record | PascalCase | `DonationService`, `CreateLeadCommand` |
| Interface | `I` + PascalCase | `IDonationRepository` |
| Method | PascalCase | `GetActiveSponsors()` |
| Property | PascalCase | `FirstName`, `IsActive` |
| Private field | `_camelCase` | `_donationRepository` |
| Parameter | camelCase | `donationId`, `cancellationToken` |
| Constant | PascalCase | `MaxRetryCount` |
| Enum (singular) | PascalCase | `DonationType.Zakat` |
| Generic type | `T` + descriptor | `TEntity`, `TResponse` |

### 1.2 File & Type Rules

- File-scoped namespaces.
- Primary constructors for DI.
- `sealed` by default — opt into inheritance, don't opt out.
- Records for DTOs, commands, queries, and value objects.
- One type per file (exception: small related records/enums used only by a parent type).
- File name matches the type name; directory structure mirrors the namespace.
- XML documentation on all public types and methods.

### 1.3 Canonical Handler Shape

```csharp
namespace Nexora.Modules.CRM.Application.Commands;

public sealed class CreateLeadHandler(
    ILeadRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<CreateLeadHandler> logger)
    : IRequestHandler<CreateLeadCommand, Result<LeadResponse>>
{
    public async Task<Result<LeadResponse>> Handle(
        CreateLeadCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lead = Lead.Create(request.ContactId, request.Source, request.PipelineStageId);
        repository.Add(lead);
        await unitOfWork.CommitAsync(cancellationToken);

        logger.LogInformation("Lead {LeadId} created for contact {ContactId}",
            lead.Id, request.ContactId);

        return Result.Success(
            lead.Adapt<LeadResponse>(),
            LocalizedMessage.Of("lockey_crm_lead_created_success"));
    }
}
```

### 1.4 Domain Entities & Value Objects

- Rich behavior on entities (no public setters for state transitions).
- Factory methods (`Create`, `Register`, ...) return the aggregate.
- Value objects as `sealed record` with private constructors and guards.
- `Entity<T>.Equals` is null-safe; use it or `==`/`!=` without extra null checks.

### 1.5 Conventional Commits Mapping

See [commit-style.md](commit-style.md). C# commit prefixes align with module names
(`feat(crm):`, `fix(donations):`, etc.).

## 2. TypeScript Conventions

Source: legacy `FRONTEND_STANDARDS.md`; CLAUDE.md §When Writing Frontend Code.

### 2.1 Core Rules

- **Strict mode** mandatory. **Never** use `any`.
- **Never** `as unknown as T` — it indicates a genuine type mismatch to resolve.
- Functional components only. No class components (except `ErrorBoundary`).
- Hooks named `use{Name}`, components `PascalCase.tsx`.
- Props interface: `{ComponentName}Props` in the same file.
- Shared components: named export. Page components: default export.
- Tests co-located: `ContactList.test.tsx` next to `ContactList.tsx`.

### 2.2 State Management

| Category | Tool |
|----------|------|
| Server state | TanStack Query v5 (`useQuery`, `useMutation`) |
| Client state | Zustand — minimal stores only (auth, theme, sidebar) |
| Form state | React Hook Form + Zod |
| URL state | query params (search, filters, pagination) |

Forbidden: Redux, Context for frequently changing data, direct `fetch`/`useEffect` for API
calls.

### 2.3 Styling

- Tailwind CSS 4 + shadcn/ui.
- `cn()` utility for conditional classes.
- RTL via Tailwind `rtl:` utilities.
- No inline `style={}`, CSS modules, or styled-components.
- No hardcoded hex/rgb — use shadcn CSS variables.

### 2.4 No Raw Text in JSX

Every user-visible string goes through `t('lockey_...')` — see
[localization.md](localization.md).

## 3. API Endpoint Convention

### 3.1 URL Shape

```
/api/v{n}/{module}/{resource}
```

Examples:

```
GET    /api/v1/crm/leads
POST   /api/v1/crm/leads
GET    /api/v1/crm/leads/{id}
PUT    /api/v1/crm/leads/{id}
DELETE /api/v1/crm/leads/{id}
POST   /api/v1/crm/leads/{id}/convert
```

`{module}` matches the module name in `Nexora.Modules.{Module}` (lowercase kebab where
multi-word).

### 3.2 `ApiEnvelope<T>` (Mandatory Response Wrapper)

Every backend response is wrapped. Bare object returns are a review block.

```json
// Success
{
  "data": { "id": "lea-123" },
  "meta": { "page": 1, "pageSize": 20, "totalCount": 142 },
  "message": { "key": "lockey_crm_lead_created_success", "params": {} }
}

// Error
{
  "error": {
    "code": "LEAD_NOT_FOUND",
    "message": { "key": "lockey_crm_lead_not_found", "params": {} },
    "details": []
  },
  "traceId": "00-abc123..."
}
```

### 3.3 HTTP Status Codes

| Code | Usage |
|------|-------|
| 200 | Successful GET / PUT / DELETE (DELETE returns envelope with message, no data) |
| 201 | Successful POST (resource created) |
| 400 | Validation error |
| 401 | Not authenticated |
| 403 | Not authorized |
| 404 | Resource not found |
| 409 | Conflict (duplicate, state violation) |
| 422 | Business rule violation |
| 500 | Unexpected server error |

### 3.4 Frontend Consumption

- Always unwrap `data`.
- Error messages are `lockey_` keys — resolve via `t(key, params)`.
- Validation errors set field-level errors on forms via `setError()`.
- Use `useApiError()` hook — no manual error parsing.
- File uploads via presigned URL pattern — no direct multipart uploads.

## 4. Security Hygiene (Code-Level)

- **Never** use `dangerouslySetInnerHTML`.
- **Never** store tokens in `localStorage` — httpOnly cookies or secure memory.
- UI permission checks are for UX only — backend enforces.
- `VITE_*` and `NEXT_PUBLIC_*` vars are **public**. No secrets there ever.
- Auth gate MUST check `!token || token.error === 'RefreshAccessTokenError'`.

## 5. Pre-Commit Linters

Changes under `src/Clients/` MUST pass lint before commit:

```bash
cd src/Clients/nexora-admin && npm run lint
cd src/Clients/nexora-portal && npm run lint
```
