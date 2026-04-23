# Multi-Currency Standard

Derives from: CLAUDE.md §Solution Structure (`SharedKernel`). No dedicated ADR yet.
TODO(maintainer): raise an ADR for "Money value object + exchange-rate provider contract"
before Finance / Subscription / Fundraising ship multi-currency features.

Multiple Nexora modules transact in money (Finance, Subscription, Fundraising, partially
Education and Events). They MUST share one `Money` value object and one exchange-rate
strategy. This document defines both.

## 1. The `Money` Value Object

`Money` lives in `Nexora.SharedKernel`. Every module that handles monetary values uses this
type — **no module defines its own**.

```csharp
namespace Nexora.SharedKernel.Money;

public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency) { Amount = amount; Currency = currency; }

    public static Money Of(decimal amount, Currency currency)
    {
        if (decimal.Round(amount, currency.DecimalDigits) != amount)
            throw new DomainException("lockey_money_precision_exceeded");
        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("lockey_money_currency_mismatch");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other) { /* same currency check */ }
    public Money Multiply(decimal factor) { /* rounded to Currency.DecimalDigits */ }
}
```

### Rules

- Amount is `decimal` — never `double` / `float`.
- `Currency` is a value object (ISO 4217 code + decimal digits).
- Same-currency operations only; cross-currency requires explicit conversion via
  `IExchangeRateProvider`.
- `Money.Amount` is always rounded to `Currency.DecimalDigits` at construction (§4).
- `Money` is serialized as `{ "amount": "12.50", "currency": "USD" }` — amount as string to
  avoid floating-point artifacts.

## 2. `Currency`

```csharp
public sealed record Currency(string Code, int DecimalDigits, string Symbol)
{
    public static readonly Currency USD = new("USD", 2, "$");
    public static readonly Currency EUR = new("EUR", 2, "€");
    public static readonly Currency TRY = new("TRY", 2, "₺");
    public static readonly Currency GBP = new("GBP", 2, "£");
    public static readonly Currency JPY = new("JPY", 0, "¥");
    // Extend cautiously; every addition needs a seeded exchange rate.
}
```

Rules:

- ISO 4217 code — never invent codes.
- `DecimalDigits` matches ISO. Most currencies are 2; JPY, KRW are 0; BHD is 3.
- Non-decimal currencies (JPY, KRW) round to whole units — `Money.Of(12.5m, JPY)` throws.

## 3. Exchange Rate Strategy

### 3.1 Contract

```csharp
public interface IExchangeRateProvider
{
    Task<ExchangeRate> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken ct);
}

public sealed record ExchangeRate(Currency From, Currency To, decimal Rate, DateOnly AsOf, string Source);
```

### 3.2 Daily Snapshot + Configurable Provider

- Rates are **daily**, not real-time — a single rate for `(from, to, date)` is used for all
  transactions on that date within a tenant. Eliminates drift between parts of a
  transaction.
- **Default providers:**
  - **TRY pairs** — Turkish Central Bank (TCMB), `https://www.tcmb.gov.tr/kurlar/today.xml`. TCMB publishes one rate per business day at ~15:30 Europe/Istanbul; weekend/holiday queries use the last published rate.
  - **EUR pairs** — European Central Bank (ECB), `https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml`. ECB publishes at ~16:00 CET on TARGET business days.
  - **Fallback provider** — OpenExchangeRates, used for any pair neither TCMB nor ECB cover. API key stored as `nexora/openexchangerates/api-key` in the secret provider.
- Provider is swappable per tenant via `ITenantConfiguration` key
  `finance:exchange-rate-provider` (values: `tcmb`, `ecb`, `openexchangerates`, `custom`).
- **Daily batch schedule:** `exchange-rates:daily-snapshot` recurring `NexoraJob` on queue
  `maintenance`, cron `0 6 * * *` UTC. The job fans out provider calls in parallel, writes
  all successful `(from, to, date, rate)` tuples to `shared.exchange_rate_snapshot`, and
  records a `security-event` audit entry summarizing provider, pair count, and any
  partial-failure. Missed runs are replayed by the idempotent re-fetch command.
- **Cache strategy:**
  - Source of truth: `shared.exchange_rate_snapshot` table (PostgreSQL, shared schema).
  - **L2 (Redis via Dapr State Store)** — the `IExchangeRateProvider` wraps DB reads in a
    cache-aside with **1-hour TTL** keyed as `exchange-rate:{from}:{to}:{yyyy-MM-dd}`.
  - **L1 (in-memory)** — handlers that perform many conversions in a request hydrate a
    `DailyRateBook` from the L2 cache once at handler entry; the book is a
    `Dictionary<(Currency,Currency), decimal>` scoped to the request.
  - Invalidation on manual admin refresh purges both layers for the affected date.
- Queries never hit the provider synchronously — they hit the snapshot. The provider is
  called only by the job (and by a manual admin refresh).

### 3.3 Conversion Rules

- Conversion happens at **module boundaries** (e.g. when a donation in EUR settles into a
  TRY-denominated fund). The domain keeps the original `Money`; the converted `Money` is a
  separate property with its own audit trail.
- Historical reports use the rate `AsOf` the transaction date — never today's rate.
- If no rate is available for `(from, to, date)`, the operation fails explicitly with
  `lockey_money_exchange_rate_unavailable` — never silently falls back.

## 4. Precision & Rounding

- Arithmetic is done in `decimal`. Storage column is `numeric(18,4)` — 4 fractional
  digits internally to preserve precision for multiplication/division before final
  rounding.
- **Financial totals** (invoice totals, donation totals, journal postings, any value
  that enters a ledger) use **banker's rounding** (`MidpointRounding.ToEven`) to
  `Currency.DecimalDigits` at the point the total is sealed. Intermediate line items are
  NEVER rounded — only final totals. Banker's rounding is required by IFRS-aligned
  accounting practice and eliminates the upward-bias of away-from-zero rounding over
  large batches.
- **Display rounding** is locale-aware and delegated to `Intl.NumberFormat` on the
  frontend (see §6). Backend never formats for display; it always returns
  `{ amount, currencyCode }` at full precision and lets the client format.
- Taxes, fees, and discounts are computed in the natural currency of the transaction and
  rounded once at the end — never round intermediate results.

## 5. Cross-Module Usage

### 5.1 Standard

Every module that transacts in money uses the same value object from SharedKernel:

```csharp
// Nexora.SharedKernel.Money
public sealed record Money {
    public decimal Amount { get; }           // numeric(18,4) on the wire and at rest
    public string CurrencyCode { get; }      // ISO-4217, three letters, uppercase
}
```

Rules:

- `Amount` is a `decimal(18,4)` on the wire and at rest, regardless of the currency's
  natural display digits. Display-digit rounding happens at presentation time (§6).
- `CurrencyCode` is a three-letter ISO-4217 code; modules NEVER use free-form strings.
- Conversion ALWAYS happens via the Finance module's exchange-rate service
  (`IExchangeRateProvider`, ultimately backed by `shared.exchange_rate_snapshot`). **No
  module converts on its own** — it is a CRITICAL review finding to compute a conversion
  using a provider call, a hard-coded rate, or "today's rate" from anywhere other than
  the Finance service.

### 5.2 Module Responsibilities

| Module | Responsibility |
|--------|----------------|
| **SharedKernel** | Owns `Money`, `Currency`, `IExchangeRateProvider` contract. |
| **Infrastructure** | Owns the default exchange-rate provider implementations (TCMB, ECB, OpenExchangeRates) and the daily-snapshot job. |
| **Finance** | Owns the `shared.exchange_rate_snapshot` table and the `IExchangeRateProvider` implementation that other modules consume. |
| **Subscription** | Bills in the plan's currency; requests conversions from Finance for tenant base-currency reporting only. |
| **Fundraising (Tier 3a)** | Accepts donations in multiple currencies; settles to the campaign currency using Finance's rate for the donation's transaction date. |
| **Events** | Stores ticket prices in `Money`; uses Finance conversions for revenue reports. |

Rule: A module touching money MUST depend on `SharedKernel.Money`. Defining a local
`Money`/`Currency` record is a CRITICAL review finding.

## 6. Frontend Display

Portal (Next.js) and Admin (React) both use `Intl.NumberFormat`:

```ts
function formatMoney(money: { amount: string; currency: string }, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: money.currency,
  }).format(Number(money.amount));
}
```

Rules:

- Always pass the user's active locale (never hardcode `en-US`).
- Never concatenate `amount + symbol` by hand — locale-specific position and grouping
  matter.
- Inputs use a locale-aware parser that normalises thousands separator and decimal mark
  before round-tripping back to a decimal string.

## 7. Testing

- Unit tests per module that handles money MUST cover:
  - same-currency arithmetic,
  - rejected cross-currency arithmetic,
  - precision violations (e.g. `Money.Of(1.005m, USD)` with 2 digits),
  - JPY-style zero-decimal rounding.
- Integration tests for exchange-rate provider MUST cover:
  - rate-available path,
  - missing-rate-for-date path,
  - stale snapshot detection.

## 8. Review Checklist Additions

- MCUR-1 — Any `decimal`/`double` used to represent money outside `Money` is a MAJOR
  finding.
- MCUR-2 — Any module-local `Money`/`Currency` type is CRITICAL.
- MCUR-3 — Conversion using "today's rate" for a historical transaction is MAJOR.
- MCUR-4 — Hardcoded currency symbol in JSX is MAJOR (use `Intl.NumberFormat`).
