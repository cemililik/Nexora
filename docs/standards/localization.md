# Localization Standard

Derives from: CLAUDE.md §Mandatory Standards §4; legacy `LOCALIZATION_STANDARDS.md`.
No dedicated ADR yet. TODO(maintainer): raise an ADR formalising the two-tier locale
resolution and `DatabaseLocalizationService` contract.

## 1. Golden Rule

> **No hardcoded user-facing strings anywhere — not in the backend, not in the frontend.**
> Every string that can be displayed to a user MUST be a localization key.

Applies to: API response messages, UI labels/buttons/tooltips/placeholders, email / SMS /
WhatsApp templates, PDF receipts and reports, user-facing system messages.

## 2. Key Convention

```
lockey_{scope}_{context}_{descriptor}
```

| Part | Meaning | Example |
|------|---------|---------|
| `lockey_` | Mandatory prefix | `lockey_` |
| `{scope}` | Module or area (lowercase) | `crm`, `donations`, `common`, `error`, `validation` |
| `{context}` | Feature / entity | `lead`, `donation`, `contact`, `auth` |
| `{descriptor}` | Specific meaning | `created_success`, `not_found`, `amount_required` |

Examples:

```
lockey_common_save
lockey_common_cancel
lockey_common_no_results
lockey_common_no_results_filtered
lockey_error_not_found
lockey_error_unauthorized
lockey_validation_required
lockey_validation_email_invalid
lockey_crm_lead_created_success
lockey_crm_lead_not_found
lockey_donations_amount_minimum
lockey_donations_recurring_created
lockey_nav_dashboard
```

Placeholders use `{named}` syntax — never positional:

```
lockey_donations_amount_minimum → "Minimum donation is {amount} {currency}"
```

## 3. Backend Rules — Return Keys, Never Strings

The backend **NEVER** resolves translations (the sole exception is the Notification Engine
rendering email / SMS — it resolves through the tenant's configured locale).

### 3.1 API Envelope Shape

```json
// Success
{
  "data": { "id": "don-123" },
  "message": { "key": "lockey_donations_donation_confirmed", "params": {} }
}

// Error
{
  "error": {
    "code": "DONATION_AMOUNT_MINIMUM",
    "message": {
      "key": "lockey_donations_amount_minimum",
      "params": { "amount": "10", "currency": "TL" }
    },
    "details": []
  },
  "traceId": "00-abc123..."
}
```

### 3.2 Required Usage Patterns

```csharp
// ❌ Hardcoded — forbidden
return Ok("Donation created");
throw new Exception("Not found");
RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Must be positive");

// ✅ Required
return Result.Success(data, LocalizedMessage.Of("lockey_donations_created_success"));
throw new DomainException("lockey_donations_only_pending_can_confirm");
RuleFor(x => x.Amount).GreaterThan(0)
    .WithMessage("lockey_validation_amount_greater_than_zero");
```

Mandatory:

- `Result.Success(...)` and `Result.Failure<T>(...)` MUST use
  `LocalizedMessage.Of("lockey_...")`.
- `DomainException` MUST be thrown with a `lockey_` key.
- `FluentValidation` `.WithMessage(...)` MUST use a `lockey_` key.

### 3.3 Internal Logs Are Exempt

`ILogger<T>` structured log messages are for developers, not users. Use English literals
with structured parameters:

```csharp
logger.LogInformation("Donation {DonationId} confirmed", id); // fine
```

## 4. Frontend Rules

### 4.1 Resolvers

| Surface | Library | Hook |
|---------|---------|------|
| `nexora-admin` (React 19) | `react-i18next` | `const { t } = useTranslation('module');` |
| `nexora-portal` (Next.js 16) | `next-intl` | `const t = useTranslations('module');` |

Raw text in JSX is **forbidden** — always `{t('lockey_...')}`.

### 4.2 Namespace-per-Module Translation Files

```
locales/{lang}/{module}.json

locales/en/common.json
locales/en/crm.json
locales/en/donations.json
locales/tr/common.json
locales/tr/crm.json
locales/tr/donations.json
```

- Each module owns its namespace.
- Cross-module keys live in `common.json`.
- Both `en` and `tr` MUST exist for every key before merge — CI fails the PR otherwise.
  TODO(maintainer): confirm the CI check is in place; scaffold one if absent.

### 4.3 RTL

Use Tailwind logical properties: `ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`, `border-s`,
`border-e`. Never `ml-`, `mr-`, `left-`, `right-` in new code.

Locale-aware redirects MUST include `/{locale}/` in the URL.

## 5. Two-Tier Locale Resolution

The active locale for a given request is resolved through a two-tier lookup:

1. **Platform default** — the compile-time fallback set at `Nexora.Host` startup
   (currently `en`). Acts as the catch-all if nothing else is configured.
2. **Tenant override** — a per-tenant value surfaced via `DatabaseLocalizationService` from
   tenant configuration. Admins change this in the tenant settings UI.

Request-level resolution (user's preferred locale sent via `Accept-Language` or explicit
query) is handled on the frontend only; the backend treats `Accept-Language` as advisory
for the Notification Engine, never as a locale-resolution tier.

TODO(maintainer): promote these two tiers into an ADR so future additions (user-level
preference as a possible third tier) are tracked formally.

## 6. Adding a New Key

Use the `add-lockey-key` skill to keep all usage sites and translation files in sync. Manual
steps if the skill is unavailable:

1. Pick a key: `lockey_{scope}_{context}_{descriptor}`.
2. Add it to `locales/en/{module}.json` and `locales/tr/{module}.json`.
3. Reference it in backend (`LocalizedMessage.Of`, `WithMessage`, `DomainException`) and/or
   frontend (`t('...')`).
4. Add a `Lockey:` trailer to the commit (see [commit-style.md](commit-style.md) §2).

## 7. Linting / Enforcement Hooks

- Zero-tolerance rule in [code-review.md](code-review.md): hardcoded user-facing string is
  CRITICAL.
- Architecture test: string-literal scan over handler/validator assemblies for common
  "return OK" patterns with non-`lockey_` strings. TODO(maintainer): confirm the current
  test asset covers both `Result.Success` and `.WithMessage`.
