---
name: add-hangfire-job
description: Add a Hangfire background/recurring job extending NexoraJob<TParams> per INFRASTRUCTURE_STANDARDS.md. Use when the user asks to "add a background job", "schedule a recurring task", "process async", or needs work done outside the request/response cycle.
---

# Add Hangfire Job

All background work in Nexora goes through Hangfire via `NexoraJob<TParams>` (tenant-aware, logged, traced).

## When to use a job
- Long-running work (>2s user-facing) → queue it.
- Scheduled/recurring work → register in `IModule.ConfigureJobs()`.
- Retries needed on transient failures → Hangfire retries automatically (job MUST be idempotent).

## File location
`src/Modules/Nexora.Modules.{Module}/Infrastructure/Jobs/{JobName}Job.cs`

## Skeleton
```csharp
public sealed class RecurringChargeJob(
    IDonationRepository repo,
    IPaymentGateway gateway,
    ILogger<RecurringChargeJob> logger
) : NexoraJob<RecurringChargeParams>(logger)
{
    public override string JobName => "donations:recurring-charge";

    protected override async Task ExecuteAsync(RecurringChargeParams p, CancellationToken ct)
    {
        // idempotent work only
    }
}

public sealed record RecurringChargeParams(DonationId DonationId);
```

## Rules
- **Job name**: `{module}:{action-descriptor}` — kebab-case descriptor. Example: `notifications:send-digest`.
- **Idempotent**: same params twice must produce the same effect (use dedupe keys / state checks).
- **Max duration 10 min** — split larger work into batched jobs.
- **Queue selection**:
  - `critical` — payments, auth side-effects.
  - `default` — normal async work.
  - `bulk` — mass operations (imports, exports).
  - `maintenance` — cleanup, retention, archival.
- **No `catch(Exception)`** inside the job — `NexoraJob` base handles that.
- **Log** start/end at Information with structured params; Warning on expected business failure.
- **Tenant context** — automatic via `NexoraJob`; do not manually resolve tenant.
- **Cache writes** use `ICacheService`; **secrets** use `ISecretProvider`. Never `DaprClient` directly.

## Registration
- **Fire-and-forget / scheduled**: enqueue via `IBackgroundJobClient` from a command handler.
- **Recurring**: in module's `ConfigureJobs(IRecurringJobManager mgr)`:
  ```csharp
  mgr.AddOrUpdate<RecurringChargeJob>(
      "donations:recurring-charge",
      job => job.RunAsync(new RecurringChargeParams(...), CancellationToken.None),
      Cron.Hourly,
      new RecurringJobOptions { Queue = "critical" });
  ```
- Expression pattern: `job => job.RunAsync(params, ct)` — **do not** inline logic in the expression.

## Tests
- Unit test the job's `ExecuteAsync` in isolation.
- Idempotency test: run twice with same params → assert single effect.
