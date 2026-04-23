# 0019 — Recurring Billing vs Recurring Donations: Two Engines, One Payment Port

## Status

Accepted

## Date

2026-04-22

## Context

Nexora has two modules that drive recurring money movement:

- **Subscription & Billing** (Tier 2, Enterprise) — recurring invoicing for tuition,
  memberships, SaaS-style plans. Central concepts: Plan → Subscription → BillingCycle →
  Invoice → Payment. Output is a **tax invoice**. Amounts are plan-driven, predictable, and
  often carry VAT/tax.
- **Fundraising** (Tier 3a, NGO) — recurring donations and sponsorship installments.
  Central concepts: RecurringDonationPlan → RecurringDonationCharge → Donation → Receipt.
  Output is a **tax-deductible donation receipt** (TR bağış makbuzu, US 501(c)(3)). Amounts
  may be varied by the donor mid-lifecycle, donations may be anonymous, and each charge is
  attributed to a campaign / fund / sponsorship.

The legacy Donations spec (see Fundraising SPEC appendix) already models
`RecurringPlan`/`StoredPaymentMethod` entirely inside the donations domain. Now that a
Subscription module exists in Tier 2, the question is whether recurring donations should be
reframed as a specialisation of Subscription, or remain a first-class capability of
Fundraising.

## Decision drivers

- NGOs that install Fundraising must not be forced to also install Subscription.
- Receipt semantics differ: tax-deductible donation receipt vs VAT invoice — different
  numbering series, different legal templates, different annual-statement rules.
- Donor anonymity (`is_anonymous`, `is_guest`) has no equivalent in Subscription.
- Donation amounts can vary per charge (donor raises from 100 to 150 TL mid-plan); plan
  amounts rarely do.
- Campaign / fund / sponsorship attribution is per charge, not per plan.
- Both engines need identical plumbing into Stripe / iyzico / bank providers, and both
  need the same PCI posture (tokenised cards only).
- Dunning differs: Subscription suspends a service; Fundraising just stops charging and
  notifies the donor — no resource to "lock out".

## Considered options

1. **Option A — Single Subscription engine, donations as a plan type**
   - Reuse `subscription_subscriptions` / `subscription_invoices`; add a `ReceiptKind`
     enum (Invoice vs DonationReceipt) and an `AnonymousMode` flag.
   - Pros: one codebase for recurring billing; one dunning pipeline.
   - Cons: forces NGO-only tenants to install an Enterprise-tier module; conflates
     invoice numbering with receipt numbering; variable-amount + anonymity fields become
     polluting optionals on every Subscription row; annual tax statements become
     module-spanning joins.

2. **Option B — Two separate engines sharing a common `IPaymentProvider` port** *(chosen)*
   - Fundraising owns `RecurringDonationPlan` / `RecurringDonationCharge` / `Receipt`.
   - Subscription owns `Subscription` / `BillingCycle` / `Invoice`.
   - Both depend on a shared SharedKernel port `IPaymentProvider` with Stripe/iyzico
     adapters in Infrastructure.
   - Pros: clear module boundaries aligned with ADR-0016 tiering; receipts vs invoices
     stay semantically distinct; either module installable without the other; failure /
     retry policies evolve independently.
   - Cons: two recurring-job schedulers; two sets of stored-payment-method tables
     (mitigated by shared adapter that tokenises once per `(tenantId, contactId)` at the
     provider side).

3. **Option C — Extract a third "RecurringEngine" module both depend on**
   - Pros: theoretically DRY.
   - Cons: premature — creates a module with no UX, duplicates tenancy/permission wiring,
     and leaks donation-specific concepts (campaign attribution, anonymity) back through
     the abstraction.

## Decision outcome

We adopt **Option B**. Subscription and Fundraising each own a recurring engine tuned to
its domain semantics. They share a single `IPaymentProvider` port (in
`Nexora.SharedKernel.Payments`) with Stripe and iyzico adapters living in
`Nexora.Infrastructure.Payments`. Neither module imports the other; both import the
SharedKernel port.

This matches the tier split in ADR-0016 (Enterprise vs NGO), preserves receipt semantics,
and keeps each module installable in isolation.

## Consequences

### Positive

- Clear receipt-vs-invoice boundary; no conditional logic mixing tax invoice and donation
  receipt code paths.
- Fundraising can ship to an NGO tenant without the Subscription module installed.
- Each module's dunning policy evolves independently (Subscription suspends access;
  Fundraising pauses the plan and notifies).
- Payment-provider upgrades (adding PayPal, a local Turkish bank, etc.) ship once in
  Infrastructure and are immediately available to both engines.

### Negative

- Two Hangfire recurring schedulers (`subscription:generate-invoices`,
  `fundraising:charge-recurring`) — more jobs to observe, but each is simpler.
- Two stored-payment-method tables (`subscription_payment_methods`,
  `fundraising_stored_payment_methods`). A tenant's donor who is also a subscriber will
  have two rows — acceptable because both reference the same provider token.
- Slight duplication of retry-policy code; mitigated by keeping both policies as data-only
  configuration consumed by the shared `IPaymentProvider.ChargeAsync` with an
  idempotency key.

### Neutral

- Introduces `Nexora.SharedKernel.Payments.IPaymentProvider` as a new SharedKernel port.
- Subscription and Fundraising each publish their own `PaymentFailed` / `ChargeFailed`
  integration events — consumers (Notifications, Reporting) subscribe to both.

## Implementation notes

- **SharedKernel port:** `IPaymentProvider` exposes `ChargeAsync`, `RefundAsync`,
  `TokenizePaymentMethodAsync`, `ValidateWebhookAsync`. Webhook routing is module-local —
  each module exposes its own `/webhooks/{provider}` endpoint and dispatches into its own
  charge table by metadata correlation ID.
- **Idempotency:** every recurring charge uses a deterministic key:
  `sub:{subscriptionId}:{cycleNumber}` for Subscription,
  `fnd:{planId}:{chargeSequence}` for Fundraising.
- **Observability:** two Meters — `nexora.subscription.payments` and
  `nexora.fundraising.charges` — both emit the same counters (attempted, succeeded,
  failed, retried) so a platform-wide dashboard can sum them.
- **Failure handling:** Subscription → grace-period + suspend (see its SPEC §4.1).
  Fundraising → retry schedule (day 1, 3, 7); on exhaustion, mark plan
  `PaymentFailed`, notify donor, do not lock out any resource.
- **Reporting:** donor annual statements live in Reporting and read only from Fundraising
  tables. Subscription revenue reports read only from Subscription tables.

### Hangfire collision mitigation

- Subscription recurring job: `subscription:generate-invoices` (runs 00:05 UTC daily, queue `default`).
- Fundraising recurring job: `fundraising:charge-recurring` (runs 00:35 UTC daily, queue `default`).
- 30-minute offset prevents simultaneous payment provider contention.
- Both jobs respect provider rate limits via Polly bulkhead (per-provider, configured in `payments:ratelimit` options).
- Queue segregation: critical payment retries go to `critical` queue (higher priority); normal cycles stay on `default`.

### Stored payment method deduplication

When the same person is both a subscriber (B2B) and a donor (NGO), the stored payment method must not duplicate:
- `IPaymentMethodVault.StoreMethod` returns an existing `PaymentMethodId` if the provider customer ID + fingerprint already exists for the tenant+person.
- Cross-module reference: both Subscription's `stored_method_id` and Fundraising's `stored_method_id` can point to the same `payment_methods` row.
- Delete semantics: a payment method is hard-deleted only when no Subscription subscription AND no Fundraising recurring plan references it.

## References

- [ADR-0016 Module Tier Classification](0016-module-tier-classification.md)
- [ADR-0017 Portal Extension Architecture](0017-portal-extension-architecture.md)
- [ADR-0005 Transactional Outbox](0005-transactional-outbox.md)
- `docs/modules/tier-2-enterprise/subscription/SPEC.md`
- `docs/modules/tier-3a-ngo/fundraising/SPEC.md`
- `docs/standards/multi-currency.md`
