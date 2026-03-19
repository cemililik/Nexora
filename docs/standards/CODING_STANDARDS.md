# Nexora - Coding Standards

## 1. General Principles

- **SOLID** principles are mandatory, not optional
- **YAGNI** (You Aren't Gonna Need It) — don't build abstractions for hypothetical futures
- **DRY** — but only when duplication is true duplication (same reason to change), not coincidental
- **Favor composition over inheritance**
- **Make illegal states unrepresentable** — use the type system to enforce business rules

## 2. Solution Structure

```
Nexora/
├── src/
│   ├── Nexora.Host/                    # Main application host (ASP.NET)
│   ├── Nexora.SharedKernel/            # Shared types, base classes, interfaces
│   ├── Nexora.Infrastructure/          # Cross-cutting: EF, Caching, Messaging
│   ├── Modules/
│   │   ├── Nexora.Modules.Identity/
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   ├── Nexora.Modules.Contacts/
│   │   ├── Nexora.Modules.CRM/
│   │   ├── Nexora.Modules.Donations/
│   │   └── ... (one project per module)
│   └── Clients/
│       ├── nexora-admin/               # React admin dashboard
│       └── nexora-portal/              # Next.js public portal
├── tests/
│   ├── Nexora.Modules.CRM.UnitTests/
│   ├── Nexora.Modules.CRM.IntegrationTests/
│   ├── Nexora.Architecture.Tests/      # ArchUnit-style tests
│   └── Nexora.E2E.Tests/
├── deploy/
│   ├── docker/
│   ├── k8s/
│   └── helm/
├── docs/
├── tools/                              # Scripts, code generators
└── Nexora.sln
```

## 3. C# Coding Conventions

### Naming
| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | PascalCase | `Nexora.Modules.CRM.Domain` |
| Class / Record | PascalCase | `DonationService`, `CreateLeadCommand` |
| Interface | IPascalCase | `IDonationRepository` |
| Method | PascalCase | `GetActiveSponsors()` |
| Property | PascalCase | `FirstName`, `IsActive` |
| Private field | _camelCase | `_donationRepository` |
| Parameter | camelCase | `donationId`, `cancellationToken` |
| Constant | PascalCase | `MaxRetryCount` |
| Enum | PascalCase (singular) | `DonationType.Zakat` |
| Generic type | T + Descriptor | `TEntity`, `TResponse` |

### File Organization
- One type per file (exceptions: related records/enums used only by parent type)
- File name matches type name: `CreateLeadCommand.cs`
- Directory structure mirrors namespace

### Code Style
```csharp
// Use file-scoped namespaces
namespace Nexora.Modules.CRM.Application.Commands;

// Use primary constructors for DI
public sealed class CreateLeadHandler(
    ILeadRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<CreateLeadHandler> logger)
    : IRequestHandler<CreateLeadCommand, LeadResponse>
{
    public async Task<LeadResponse> Handle(
        CreateLeadCommand request,
        CancellationToken cancellationToken)
    {
        // Guard clauses first
        ArgumentNullException.ThrowIfNull(request);

        // Business logic
        var lead = Lead.Create(
            request.ContactId,
            request.Source,
            request.PipelineStageId);

        repository.Add(lead);
        await unitOfWork.CommitAsync(cancellationToken);

        logger.LogInformation("Lead {LeadId} created for contact {ContactId}",
            lead.Id, request.ContactId);

        return lead.Adapt<LeadResponse>();
    }
}
```

### Domain Entities
```csharp
// Rich domain model — behavior lives on the entity
public sealed class Donation : AuditableEntity, IAggregateRoot
{
    public DonationId Id { get; private set; }
    public ContactId DonorId { get; private set; }
    public DonationType Type { get; private set; }
    public Money Amount { get; private set; }
    public DonationStatus Status { get; private set; }

    private Donation() { } // EF constructor

    public static Donation Create(
        ContactId donorId,
        DonationType type,
        Money amount)
    {
        var donation = new Donation
        {
            Id = DonationId.New(),
            DonorId = donorId,
            Type = type,
            Amount = amount,
            Status = DonationStatus.Pending
        };

        donation.AddDomainEvent(new DonationCreatedEvent(donation.Id));
        return donation;
    }

    public void Confirm(PaymentReference reference)
    {
        if (Status != DonationStatus.Pending)
            throw new DomainException("Only pending donations can be confirmed.");

        Status = DonationStatus.Confirmed;
        AddDomainEvent(new DonationConfirmedEvent(Id, reference));
    }
}
```

### Strongly Typed IDs
```csharp
// Always use strongly-typed IDs, never raw Guid/int
[StronglyTypedId]
public readonly partial struct DonationId;

[StronglyTypedId]
public readonly partial struct ContactId;
```

### Value Objects
```csharp
public sealed record Money(decimal Amount, Currency Currency)
{
    public static Money Zero(Currency currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Cannot add different currencies.");
        return this with { Amount = Amount + other.Amount };
    }
}
```

## 4. CQRS Pattern

### Commands (Write Operations)
```csharp
// Command — always returns a result
public sealed record CreateDonationCommand(
    Guid DonorId,
    string Type,
    decimal Amount,
    string Currency) : IRequest<Result<DonationResponse>>;

// Validator — always validate commands
public sealed class CreateDonationCommandValidator
    : AbstractValidator<CreateDonationCommand>
{
    public CreateDonationCommandValidator()
    {
        RuleFor(x => x.DonorId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
```

### Queries (Read Operations)
```csharp
// Query — read-only, can use optimized read models
public sealed record GetDonationsByDonorQuery(
    Guid DonorId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<DonationSummary>>;
```

## 5. API Conventions

### Endpoint Structure
```
/api/v{version}/{module}/{resource}

Examples:
GET    /api/v1/crm/leads
POST   /api/v1/crm/leads
GET    /api/v1/crm/leads/{id}
PUT    /api/v1/crm/leads/{id}
DELETE /api/v1/crm/leads/{id}
POST   /api/v1/crm/leads/{id}/convert

GET    /api/v1/donations/donations
POST   /api/v1/donations/donations
GET    /api/v1/donations/campaigns/{id}/progress
```

### Response Envelope
```json
// Success
{
  "data": { ... },
  "meta": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 142
  }
}

// Error
{
  "error": {
    "code": "DONATION_NOT_FOUND",
    "message": "Donation with ID 'xxx' was not found.",
    "details": []
  },
  "traceId": "00-abc123..."
}
```

### HTTP Status Codes
| Code | Usage |
|------|-------|
| 200 | Successful GET/PUT |
| 201 | Successful POST (resource created) |
| 204 | Successful DELETE |
| 400 | Validation error |
| 401 | Not authenticated |
| 403 | Not authorized |
| 404 | Resource not found |
| 409 | Conflict (duplicate, state violation) |
| 422 | Business rule violation |
| 500 | Unexpected server error |

## 6. Testing Standards

### Test Naming
```csharp
// Pattern: Method_Scenario_ExpectedResult
[Fact]
public async Task CreateDonation_WithValidData_ReturnsDonationId()

[Fact]
public async Task CreateDonation_WithZeroAmount_ThrowsValidationException()

[Fact]
public async Task ConfirmDonation_WhenAlreadyConfirmed_ThrowsDomainException()
```

### Test Structure (AAA)
```csharp
[Fact]
public async Task CreateDonation_WithValidData_ReturnsDonationId()
{
    // Arrange
    var command = new CreateDonationCommand(...);

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Id.Should().NotBeEmpty();
}
```

### Coverage Requirements
| Layer | Minimum Coverage |
|-------|-----------------|
| Domain | 90% |
| Application (Handlers) | 85% |
| Infrastructure | 70% |
| API | Integration tests cover all endpoints |

### Architecture Tests
```csharp
// Enforce module boundaries — modules cannot reference each other directly
[Fact]
public void CRM_Module_Should_Not_Reference_Donation_Module()
{
    Types.InAssembly(typeof(CrmModule).Assembly)
        .Should()
        .NotHaveDependencyOn("Nexora.Modules.Donations")
        .GetResult()
        .IsSuccessful.Should().BeTrue();
}
```

## 7. Git Conventions

### Branch Naming
```
feature/NEX-123-lead-pipeline
bugfix/NEX-456-donation-amount-rounding
hotfix/NEX-789-payment-timeout
chore/NEX-101-update-dependencies
docs/NEX-202-api-documentation
```

### Commit Messages (Conventional Commits)
```
feat(crm): add lead pipeline stage management
fix(donations): correct multi-currency rounding
refactor(contacts): extract address value object
test(sponsorship): add installment payment tests
docs(api): update donation endpoint documentation
chore(deps): update EF Core to 10.0.1
```

### PR Requirements
- Linked to issue/task
- All CI checks pass
- At least 1 approval
- No unresolved conversations
- Squash merge to main

## 8. Logging Standards

```csharp
// Use structured logging with semantic parameters
logger.LogInformation("Donation {DonationId} confirmed for donor {DonorId}, amount {Amount} {Currency}",
    donation.Id, donation.DonorId, donation.Amount.Amount, donation.Amount.Currency);

// Log levels
// Trace:       Detailed diagnostic info (never in production)
// Debug:       Development diagnostics
// Information: Business events (donation created, user logged in)
// Warning:     Recoverable issues (retry, fallback used)
// Error:       Failures requiring attention (payment failed, API timeout)
// Critical:    System failures (database down, out of memory)
```

## 9. Error Handling

```csharp
// Use Result pattern instead of exceptions for expected failures
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public LocalizedMessage Message { get; }  // lockey_ key
    public Error Error { get; }
}

// All user-facing messages MUST use LocalizedMessage
return Result.Success(data, LocalizedMessage.Of("lockey_donations_donation_confirmed"));
return Result.Failure<T>(LocalizedMessage.Of("lockey_error_donor_not_found"));

// Reserve exceptions for unexpected/programming errors
// Domain exceptions for invariant violations — always with lockey_ key
throw new DomainException("lockey_donations_only_pending_can_confirm");

// Global exception handler converts unhandled exceptions to ProblemDetails
```

## 10. Localization (CRITICAL)

**See full standard**: `docs/standards/LOCALIZATION_STANDARDS.md`

### Key Format
```
lockey_{scope}_{context}_{descriptor}
```

### Mandatory Rules
```csharp
// ❌ FORBIDDEN — hardcoded strings in any user-facing context
return Ok("Donation created");
throw new Exception("Not found");
RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Must be positive");

// ✅ REQUIRED — lockey_ keys everywhere
return Result.Success(data, LocalizedMessage.Of("lockey_donations_created_success"));
throw new DomainException("lockey_error_not_found");
RuleFor(x => x.Amount).GreaterThan(0).WithMessage("lockey_validation_amount_greater_than_zero");
```

### Backend Response Format
```json
{
  "data": { ... },
  "message": {
    "key": "lockey_donations_donation_confirmed",
    "params": {}
  }
}
```

The backend **NEVER** resolves translations (except Notification Engine for emails/SMS).
The frontend resolves `lockey_` keys using `react-i18next` / `next-intl`.

### Internal Logs Are Exempt
Structured logs (ILogger) use English strings — they are not user-facing:
```csharp
// This is fine — logs are for developers, not users
logger.LogInformation("Donation {DonationId} confirmed", id);
```

## 11. Module Plugin Rules

**See full spec**: `docs/architecture/MODULE_SYSTEM.md`

### Every Module Must
1. Implement `IModule` interface
2. Declare its `Dependencies` (other modules it requires)
3. Own its `DbContext` — prefix tables as `{module}_{table}`
4. Register its own endpoints via `MapEndpoints()`
5. Register its event handlers via `ConfigureEventHandlers()`
6. Implement `OnInstallAsync()` (create tables, seed data)
7. Implement `OnUninstallAsync()` (archive data, cleanup)
8. Register its permissions via `OnStartupAsync()`
9. Contribute to shared views via `IContactActivityContributor` (if applicable)

### Cross-Module Communication
```csharp
// ✅ Via integration events (Kafka)
builder.Subscribe<ContactMergedEvent, UpdateDonorReferencesHandler>();

// ✅ Via SharedKernel interfaces
public sealed class MyHandler(IContactQueryService contacts) { ... }

// ❌ NEVER direct reference to another module's internals
using Nexora.Modules.Donations.Domain.Entities; // FORBIDDEN
```
