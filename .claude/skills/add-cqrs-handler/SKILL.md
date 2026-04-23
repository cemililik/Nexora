---
name: add-cqrs-handler
description: Add a MediatR Command or Query handler to a Nexora module with FluentValidation validator, Result<T> pattern, structured logging, and lockey messages. Use when the user asks to "add endpoint", "create command/query", "add handler", or "expose an API for X".
---

# Add CQRS Handler

Scaffolds a new Command or Query in `src/Modules/Nexora.Modules.{Module}/Application/` plus the corresponding API endpoint.

## Decide first
- **Command** (write): mutates state. Returns `Result` or `Result<TDto>`.
- **Query** (read): never mutates. Returns `Result<TDto>` or `Result<PagedResult<TDto>>`. Handler MUST use `.AsNoTracking()`.

## Files to create
```
Application/{Feature}/
├── {Verb}{Entity}Command.cs          # record Command(...) : IRequest<Result<TDto>>
├── {Verb}{Entity}CommandValidator.cs # : AbstractValidator<Command> — lockey_ WithMessage
├── {Verb}{Entity}CommandHandler.cs   # IRequestHandler<Command, Result<TDto>>
└── {Verb}{Entity}Response.cs         # DTO record (if not shared)
```

## Handler template rules
- Primary constructor DI: `public sealed class XHandler(IRepo repo, ILogger<XHandler> logger) : IRequestHandler<...>`
- Log `Information` on success creation/update/deletion with structured PascalCase params:
  `logger.LogInformation("Contact {ContactId} created for tenant {TenantId}", id, tenantId)`
- Log `Warning` on expected business-rule failures **before** returning `Result.Failure(...)`.
- Log `Error` only for external service failures (Keycloak, payment, etc.).
- **Never** `catch(Exception)` in handlers — let `GlobalExceptionHandler` take over.
- Return `Result.Success(dto, LocalizedMessage.Of("lockey_{module}_{action}_success"))`.
- Return `Result.Failure<T>(LocalizedMessage.Of("lockey_{module}_{error}"))` for expected errors.
- Query handlers: `Debug` for not-found, `Warning` when query >500ms.

## Validator rules
- Every Command MUST have a validator.
- `.WithMessage("lockey_validation_{rule}")` — never hardcoded strings.

## API endpoint rules
- Route: `/api/v{version}/{module}/{resource}` (kebab-case resource).
- DELETE returns `200 OK` with `ApiEnvelope.Success(result.Message)` — not `204`.
- Response always wrapped in `ApiEnvelope<T>`.
- Add permission attribute: `{module}.{resource}.{action}`.

## Tests
- Unit tests for handler (success + each failure path).
- Validator tests for every rule.
- Naming: `Method_Scenario_ExpectedResult`.
