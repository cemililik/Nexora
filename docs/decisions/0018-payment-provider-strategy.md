# 0018 — Payment Provider Strategy

## Status

Accepted

## Date

2026-04-22

## Context

The Tier-2 Subscription module (see `docs/modules/tier-2-enterprise/subscription/SPEC.md`)
must process recurring charges, refunds, and payment-method tokenisation on behalf of tenants.
Nexora is a global-by-default platform with a meaningful Turkey-based tenant base; the
legacy specification referenced both Stripe and iyzico without a governing rule for when each
is used, how the system behaves when one of them is degraded, or how modules other than
Subscription should interact with either.

Without a documented strategy we have three concrete risks:

1. **Provider coupling.** Stripe or iyzico SDK types could leak into the Subscription domain,
   making a future provider swap (Adyen, Braintree, PayPal, local acquirers) a breaking change.
2. **Regional compliance.** Turkish tenants are subject to TCMB / BKM regulations that
   effectively require a local PSP (iyzico, PayTR, Craftgate). Routing all Turkish traffic
   through a US-based processor would introduce FX markup, compliance risk, and
   3-D Secure friction.
3. **Resilience.** Provider outages (Stripe has had multi-hour incidents; iyzico has had API
   blackouts) would block renewal runs entirely unless the platform has a documented
   failover path.

This ADR is explicitly referenced from the Subscription SPEC §5.3, §11, and §15.

## Decision drivers

- **Domain isolation.** Nothing provider-specific may appear in Subscription domain /
  application layers.
- **Regional fit.** Turkey tenants need a local acquirer; global tenants need broad card
  network coverage and strong subscription primitives.
- **Failover.** A single provider's outage must not halt the entire billing pipeline.
- **Reconciliation.** Every charge must be traceable end-to-end via a provider charge
  identifier that Finance consumes.
- **Additive-only.** Adding a third provider later must not require changing the port
  contract.

## Considered options

1. **Single provider — Stripe only.**
   - Pros: simplest integration, strongest subscription feature set.
   - Cons: Turkish BIN acceptance is unreliable; no TL-denominated local acquiring; FX cost;
     regulatory exposure. Rejected on compliance grounds.

2. **Single provider — iyzico only.**
   - Pros: excellent Turkey coverage.
   - Cons: global card coverage weaker; no native subscription primitives (recurring is
     emulated on our side — which we already do, but the global story suffers). Rejected.

3. **Dual provider behind a port — Stripe as default, iyzico for Turkey tenants, failover
   policy, `IPaymentProvider` abstraction.**
   - Pros: regional fit and global coverage; a clean port isolates domain from SDKs; failover
     is explicit and testable.
   - Cons: two integrations to maintain; reconciliation must disambiguate provider per row.

4. **Aggregator / orchestration layer (Spreedly, Primer, Finix).**
   - Pros: one integration, routing handled for us.
   - Cons: extra vendor, extra fee, another point of failure, and loses direct access to
     provider subscription primitives. Rejected as over-engineered for current scale.

## Decision outcome

**Chosen: Option 3.** We ship two first-party provider adapters behind a single
`IPaymentProvider` port:

- **Stripe** — global default for every tenant that is not region-locked.
- **iyzico** — default for any tenant whose `tenant.country_code = TR`.
- **Failover** — if the primary provider's circuit breaker is open (5 consecutive failures
  within 60 seconds at the adapter layer, per Polly configuration), the Subscription charge
  pipeline fails the current attempt with a transient error code and reschedules per the
  dunning retry schedule. A **manual per-tenant override** (`subscription.provider.override`)
  allows ops to switch a tenant to a secondary provider while the primary is degraded; this
  override is audited and expires automatically after 24 hours unless renewed.

The port is the single integration surface for the domain:

```csharp
namespace Nexora.Modules.Subscription.Application.Abstractions;

public interface IPaymentProvider
{
    string Name { get; }              // "stripe" | "iyzico"

    Task<ChargeResult> ChargeAsync(
        ChargeRequest request, CancellationToken ct);

    Task<RefundResult> RefundAsync(
        RefundRequest request, CancellationToken ct);

    Task<TokeniseResult> TokeniseMethodAsync(
        TokeniseRequest request, CancellationToken ct);

    Task<WebhookValidation> ValidateWebhookAsync(
        string payload, string signature, CancellationToken ct);
}
```

Resolution is done by `IPaymentProviderResolver` keyed on the tenant's country plus the
override flag; adapters live in
`Nexora.Modules.Subscription.Infrastructure/PaymentProviders/{Stripe,Iyzico}/`.

## Consequences

### Positive

- Subscription domain remains provider-agnostic; SDK upgrades stay in Infrastructure.
- Turkish tenants get a compliant local acquirer with TL-native settlement.
- Global tenants get Stripe's subscription primitives and card coverage.
- Failover behaviour is explicit, circuit-broken, and audited — not ad-hoc retry.
- Adding a third provider later requires a new adapter, not port changes.

### Negative

- Two integrations to maintain, test, and monitor — including two webhook signature schemes.
- Reconciliation rows in Finance must carry `provider_name` (already present in the schema).
- Provider-specific feature gaps (e.g. Stripe's smart retries vs. iyzico's simpler API) must
  be normalised by the port, which means we give up some provider-native optimisation.

### Neutral

- Tenant onboarding gains a provider-selection step; default is inferred from country.
- The platform takes a 24-hour cap on manual provider overrides; long-term provider switches
  require an ops ticket and a secondary ADR for the tenant's contract terms.

## Implementation notes

- Adapters: `Infrastructure/PaymentProviders/Stripe/StripePaymentProvider.cs`,
  `.../Iyzico/IyzicoPaymentProvider.cs`. Each adapter owns its SDK dependency; no SDK types
  leak upward.
- Resolver: `Infrastructure/PaymentProviders/PaymentProviderResolver.cs`, registered as
  `Scoped` and resolved from the command handler via the port interface.
- Webhook routes: `/api/v1/subscription/webhooks/stripe` and `.../iyzico`. Signature
  validation runs before any state mutation.
- Circuit breakers: Polly per-provider; metrics exposed as
  `subscription_payment_provider_latency_seconds{provider}` and
  `subscription_payment_provider_failures_total{provider}`.
- Manual override: `ITenantConfiguration` key `subscription.provider.override` with a TTL
  field; expiry is enforced by the `subscription:provider-override-sweep` recurring job on
  queue `maintenance`.
- Tests: contract tests assert both adapters satisfy the same `IPaymentProvider` behaviour
  set (success, decline, network timeout, webhook replay, refund idempotency).
- Secrets: provider API keys live in `nexora/stripe/api-key` and `nexora/iyzico/api-key` via
  `ISecretProvider`.

### Money value object

All `IPaymentProvider` method signatures use `Nexora.SharedKernel.Money` (see ADR-0021) — never `decimal amount` + `string currency` pairs. Providers are responsible for ISO 4217 validation against their own supported-currency list.

### Secret rotation

- All payment provider API keys/secrets are rotated annually at minimum (SOC2 requirement).
- Rotation is coordinated via `ISecretProvider` — new secret is written under versioned key `nexora/payments/{provider}/api-key/v{N}`; `IPaymentProvider` reads latest version.
- During rotation, both old and new keys are valid for 24 hours (provider-specific dual-key window).
- Rotation audit trail: `payments_secret_rotation_log` table tracks who/when.

### Reconciliation job ownership

- Recurring job `payments:reconciliation` (runs daily at 04:00 UTC per tenant) is **owned by the Finance module**, not Subscription or Fundraising.
- Job fetches the previous day's payment records from each provider and reconciles against local `payment_intents` / `payment_transactions`.
- Discrepancies (webhook missed, provider-only transaction, local-only transaction) are logged to `payments_reconciliation_variance` and surface on the Finance admin dashboard.
- Resolution workflow: manual review → journal adjustment or provider dispute initiation.

## References

- `docs/modules/tier-2-enterprise/subscription/SPEC.md` (§5.3, §6, §11, §15)
- `docs/decisions/0014-distributed-consistency-patterns.md`
- `docs/decisions/0016-module-tier-classification.md`
- `docs/standards/multi-currency.md`
- Stripe API docs (subscriptions, PaymentIntents, Webhooks)
- iyzico API docs (subscription, 3-D Secure, webhook signature)
