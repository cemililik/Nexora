# Testing Standards

Derives from: [ADR-001 Modular Monolith](../decisions/ADR-001-modular-monolith.md) (module
boundary enforcement via architecture tests).
Source: legacy `CODING_STANDARDS.md` §7.

## 1. Three Test Tiers

| Tier | Project(s) | Scope | Real dependencies |
|------|-----------|-------|-------------------|
| **Host / Unit** | `Nexora.Modules.{X}.UnitTests` | Pure handler / domain logic with mocked ports | None — mocks only |
| **Integration** | `Nexora.Modules.{X}.IntegrationTests` | Handler + EF + real DB via Testcontainers | Real PostgreSQL, Redis; mocked external services |
| **E2E** | `Nexora.E2E.Tests` | HTTP → API → DB through the full host | Full stack via docker-compose |

Architecture tests live in `Nexora.Architecture.Tests` and enforce module boundaries.

## 2. Test Naming

Pattern: `Method_Scenario_ExpectedResult`.

```csharp
[Fact] public async Task CreateDonation_WithValidData_ReturnsDonationId();
[Fact] public async Task CreateDonation_WithZeroAmount_ReturnsValidationFailure();
[Fact] public async Task ConfirmDonation_WhenAlreadyConfirmed_ThrowsDomainException();
```

Frontend Vitest tests use the equivalent pattern as `describe` + `it`:

```ts
describe('ContactList', () => {
  it('renders empty state when no contacts', async () => { ... });
  it('navigates to detail on row click', async () => { ... });
});
```

## 3. AAA Structure

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

## 4. Architecture Tests (Mandatory)

Architecture tests enforce invariants the compiler cannot:

### 4.1 Module Boundary

```csharp
[Fact]
public void CRM_Module_Should_Not_Reference_Donations_Module()
{
    Types.InAssembly(typeof(CrmModule).Assembly)
        .Should()
        .NotHaveDependencyOn("Nexora.Modules.Donations")
        .GetResult()
        .IsSuccessful.Should().BeTrue();
}
```

### 4.2 Tier Boundary

Per [ADR-016](../decisions/ADR-016-module-tier-classification.md): Tier 1 modules MUST NOT
depend on Tier 2+, Tier 2 MUST NOT depend on Tier 3a/3b, etc. One tier-boundary test per
forbidden direction.

### 4.3 Other Architecture Invariants

- No `catch (Exception)` outside `GlobalExceptionHandler` and `NexoraJob`.
- No direct `DbContext` injection into API endpoints.
- All commands have a matching validator.
- All aggregate roots inherit `AggregateRoot<T>` or equivalent.

## 5. Coverage Targets

| Layer | Minimum |
|-------|---------|
| Domain | 90% |
| Application (handlers) | 85% |
| Infrastructure | 70% |
| API | All endpoints covered by an integration test |

## 6. Mocks vs Real DB Policy

- **Unit tests**: Mock everything below the handler (repositories, external services, cache).
  Handler logic only.
- **Integration tests**: Real PostgreSQL via Testcontainers. Real `DbContext`. Real
  `ICacheService` backed by in-memory L1 (L2 mocked). External services (Keycloak, Stripe,
  MinIO) mocked with a test double that records calls.
- **E2E**: Full stack — no mocks. Uses the shared `docker-compose` test environment.

Minimise mocks: if a test needs more than ~3 mock setups, it is likely an integration test.

## 7. Specific Rules

- Every command handler MUST have tests for:
  - success path,
  - each business-rule failure,
  - at least one external-service-failure path if applicable (see TEST-15 in
    [code-review.md](code-review.md)).
- Integration event handlers MUST have idempotency tests (same message twice → single
  side-effect).
- Auth-gated endpoints MUST have both authenticated and `401`-unauthenticated cases.
- Middleware / auth-gate frontend tests MUST cover `token.error === 'RefreshAccessTokenError'`.
- Permission-restricted routes tested with both authorized and unauthorized users.
- Use stable selectors in React tests: `getByRole`, `getByTestId`, text — never raw DOM
  structure.

## 8. Current Counts (as of 2026-04-22)

| Suite | Count |
|-------|-------|
| Backend tests (unit + integration + architecture) | **1,808** |
| `nexora-admin` frontend tests | **358** |
| `nexora-portal` frontend tests | **65** |

These counts are a snapshot — they are not an SLO. New code is expected to add tests,
not preserve a fixed number.

## 9. Forbidden Patterns

- Tests that assert log output strings.
- Tests that depend on current date/time without freezing a clock.
- `Thread.Sleep` / `await Task.Delay` to "wait for async work" — use explicit
  completion signals.
- Duplicate test scenarios — if two tests would exercise the same branches, delete one.
- Over-mocked tests (mocking internals of the unit under test).
