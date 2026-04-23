# Nexora — ADR & Module SPEC Comprehensive Review

**Rapor tarihi:** 2026-04-22
**Kapsam:** `docs/decisions/*.md` (ADR-0001 … ADR-0024) + `docs/modules/**/SPEC.md` (16 modül)
**Yöntem:** Her ADR ve SPEC CLAUDE.md standartlarına, peer ADR'lara ve birbirine karşı doğrulandı.

---

## 0. Executive Summary

### Genel değerlendirme

Doküman seti mimari açıdan olgun: Modular Monolith + schema-per-tenant + Dapr pub/sub + outbox pattern temeli sağlam; Tier sınıflandırması (ADR-0016), Portal Extension (ADR-0017), Payment Provider stratejisi (ADR-0018), Money/FX kontratı (ADR-0021), Contact Extensions (ADR-0020) ve NMP Billing (ADR-0023) gibi son ADR'lar mimari boşlukları kapatıyor. Modüllerin çoğu kendi bounded context'lerini doğru çiziyor ve event-driven integration pattern'lerini takip ediyor.

Ancak sistemik sorunlar var:

1. **ADR-0021 Money uyumu SPEC'lere tam yansımamış** — Projects, HR, Fundraising, CRM, Events-NGO, Documents, Reporting ve Subscription'da hâlâ `amount + currency` ayrı field pattern'i kullanılıyor.
2. **Stale module isimleri** — `Donations`/`Sponsorship` artık `Fundraising`'e konsolide oldu, ama Contacts, Documents, Notifications, Events-NGO, Identity ve Reporting SPEC'lerinde hâlâ eski isimlere referanslar var.
3. **Interface adlandırma çelişkisi** — ADR-0021 `IExchangeRateService` tanımlıyor; Subscription ve Finance SPEC'leri `IExchangeRateProvider` yazıyor.
4. **ADR statüs tutarsızlığı** — ADR-0021 ve ADR-0022 "Proposed" ama downstream SPEC'ler bunları authoritative kontrat olarak assume ediyor; "Accepted" olmalı.
5. **Enforcement/architecture test'leri Phase-2 iş olarak ertelenmiş** — ADR-0015 sync-rule, ADR-0016 tier-boundary, ADR-0020 contact-extension tier guard, ADR-0021 Money type scan hiçbiri Phase-1'de mekanik olarak doğrulanmıyor.
6. **Bazı architectural claims standartlarla çelişiyor** — Events-NGO "PostgreSQL RLS" diyor, CLAUDE.md schema-per-tenant + EF Core global query filters diyor; Events-NGO "Presentation/Controllers/" layer naming CLAUDE.md'nin "Api/" kuralına aykırı.

### Kritiklik dağılımı

| Seviye | Adet | Aksiyon |
|--------|-----:|---------|
| 🔴 **Kritik** (implementation blocker) | 12 | Phase-2 kick-off öncesi kapatılmalı |
| 🟠 **Önemli** (yakın vadede çözülmeli) | 27 | Phase-2 erken milestone'ları içinde |
| 🟡 **Minor / dokümantasyon temizliği** | 35+ | Rolling basis |

Tüm liste §5'te.

---

## 1. ADR Reviews (0001–0024)

### ADR-0001 — Modular Monolith Architecture

**Status:** Accepted · 2026-03-19

**Karar:** Tek deployable ASP.NET Core host; modül sınırları MediatR notifications (in-process) + Dapr pub/sub (Kafka) üzerinden iletişim; her modül kendi Domain/Application/Infrastructure/Api katmanıyla Clean Architecture.

**Güçlü yönler:** Evrimsel mikroservis yolu açık; boundary ihlalleri architecture test'leri ile korunmak isteniyor; performans bottleneck'leri async event processing ile azaltılıyor; tek deploy → tek availability tradeoff açıkça kabul edilmiş.

**Zayıflıklar / Boşluklar:**
- **Architecture test'lerin yeri belirtilmemiş.** "Enforced via architecture tests" deniyor, ama hangi projede (tests/Nexora.Architecture.Tests/) ve hangi kuralların yazılı olduğu ADR'da yok. Implementor reference'ı eksik.
- **Cross-module transaction semantiği boş** — "Single transaction across modules when needed" yazılı ama schema-per-tenant + per-module DbContext mimarisinde bu nasıl mümkün olacak? `TransactionScope`? Outbox pattern mi? ADR-0005 ve ADR-0011 bu boşluğu sonradan kapatıyor ama ADR-0001 tek başına okunduğunda kafa karıştırıcı.
- **"Shared interface" tanımı bulanık** — SharedKernel'da neyin paylaşılabileceği ve neyin modüle özel kalması gerektiği tanımlanmamış.

**Consistency:** ADR-0002, 0003, 0005, 0010, 0011, 0016 ile uyumlu. Contradiction yok.

**Verdict:** ✅ OK; implementor reference ve architecture test lokasyon bilgisi eklenmeli.

---

### ADR-0002 — Schema-per-Tenant Multi-Tenancy

**Status:** Accepted · 2026-03-19

**Karar:** PostgreSQL schema-per-tenant; tenant resolution `JWT tenant_id` → `TenantMiddleware` → DbContext `search_path` routing; organizasyon filtering aynı şema içinde `organization_id` kolonuyla.

**Güçlü yönler:** DB-katman izolasyon; cross-tenant query teknik olarak imkânsız; backup/restore per-tenant; GDPR `DROP SCHEMA` ile tamamlanır; compliance-friendly.

**Zayıflıklar / Boşluklar:**
- **Migration orchestration boş** — "Migrations are applied per-schema" deniyor ama: Her tenant için hangi mekanizma migration uyguluyor? Migration bir tenant'ta başarısız olursa state ne olur? Drift takibi nasıl? Operasyonel boşluk.
- **5000 tenant limiti gerekçesiz** — PostgreSQL "thousands of schemas" ifadesi var, ama 5K sayısı ölçüme dayanmıyor. 10K+ için "sharding düşünülür" deniyor; threshold ve trigger kriteri yok.
- **`search_path` lifecycle belirsiz** — Connection pooling sırasında search_path ne zaman set ediliyor? Connection reuse edildiğinde previous tenant'ın search_path'i leak edebilir mi? Critical correctness konusu.
- **Soft-delete + schema-per-tenant etkileşimi** — CLAUDE.md AuditableEntity global query filter kullanıyor; bu schema-per-tenant ile nasıl birleşiyor açıklanmalı.

**Consistency:** ADR-0001, 0003, 0008 ile uyumlu. ADR-0006 ile bağlantısı (tenant context → permission check) implicit; cross-reference eksik.

**Verdict:** 🟠 Operasyonel detaylar zayıf — migration orchestration bir runbook/ADR ile tamamlanmalı.

---

### ADR-0003 — Deployment Strategy

**Status:** Accepted · 2026-03-19

**Karar:** Üç deployment model (Cloud SaaS / Dedicated / Self-Hosted) tek codebase + Helm chart ile.

**Güçlü yönler:** Ticari model (SKU + per-user / flat + license) açık; N-2 support window tanımlı; additive-only migrations kuralı belirtilmiş.

**Zayıflıklar / Boşluklar:**
- **License key üretimi/doğrulaması belirsiz** — RSA-2048 signed deniyor (ADR-0023'te detaylı) ama ADR-0003 tek başına bunu belirtmiyor.
- **"Additive-only migrations" enforcement yok** — Production'da breaking migration eklemeyi kim engelleyecek? Architecture test veya CI check yok.
- **Air-gapped license validation grace period'u belirsiz** — Self-Hosted için offline fallback/grace period yok.
- **Helm chart versioning strategy eksik** — Chart v1 → v2 upgrade path nasıl? Breaking changes?
- **NMP ilişkisi vague** — "Semi-automated" tenant provisioning; NMP ADR-0023 ile bağlantı kurulmalı.

**Consistency:** ADR-0002 ve ADR-0023 (NMP billing) ile uyumlu.

**Verdict:** 🟠 Önemli boşluklar var — özellikle migration enforcement, license lifecycle ve Helm upgrade stratejisi ayrı bir runbook gerektiriyor.

---

### ADR-0004 — Centralized Permission Seeding

**Status:** Accepted · 2026-03-23

**Karar:** Tüm permission tanımları `IdentityModuleMigration.SeedAsync()`'da merkezi; frontend manifest'te UI gate için mirror.

**Güçlü yönler:** Tek kaynak — audit edilebilir; ordering race yok; transactional consistency.

**Zayıflıklar / Boşluklar:**
- **String-based permission keys type-safe değil.** `contacts.note.read` gibi string'ler backend migration + frontend manifest + endpoint `.RequireAuthorization()` olmak üzere 3 yerde tekrar ediliyor. Typo'yu yakalayacak mekanizma yok.
- **Manifest ↔ migration senkronizasyon testi vague.** ADR "integration tests verify all manifest permissions exist in DB" diyor ama test lokasyonu/adı verilmemiş.
- **Permission değişiklik audit trail'i yok** — Kim eklemiş, ne zaman, neden?
- **Hierarchy belirsiz** — `contacts.*.*` wildcard permission destekleniyor mu? Tier-based inheritance? Flat'mi?

**Consistency:** ADR-0006 ve ADR-0012 ile doğrudan bağlantılı; uyumlu.

**Verdict:** 🟠 Permission key'ler için const/enum + compile-time check önerilir.

---

### ADR-0005 — Transactional Outbox

**Status:** Accepted · 2026-03-31 (**Amended by ADR-0011**)

**Karar:** Domain event'leri local `outbox_messages` tablosuna yaz (aynı transaction'da) → `OutboxProcessor` polling → Kafka.

**Güçlü yönler:** Kafka outage'da event kaybı yok; local persist; mevcut infra kullanımı.

**Zayıflıklar / Boşluklar:**
- **ADR-0005 atomicity bug'ı ADR-0011'e kadar canlı kaldı.** Non-generic `OutboxService` → `SaveChangesAsync` internally → separate transaction → atomicity broken. ADR-0005 tek başına okunursa yanlış pattern öğretiyor. ADR içine "superseded by ADR-0011 for atomicity" notu eklenmeli.
- **5 saniye polling latency justification yok** — Tuning rationale? Configurable mı (IOptions)?
- **Outbox table TTL cleanup mekanizması tanımlanmamış** — Job kim? Hangi TTL?
- **Poison pill handling yok** — Malformed event sürekli crash ettirirse? Dead-letter queue yok.
- **Notifications at-least-once → duplicate email riski** — Idempotency key Notifications modülüne deferred ama hiçbir yerde sıkı tanımlanmamış.

**Consistency:** ADR-0011 bug-fix; ADR-0010 (notification Kafka delivery) bu pattern'i kullanıyor.

**Verdict:** 🟡 ADR-0011'i mutlaka ADR-0005'in başına cross-reference olarak ekle.

---

### ADR-0006 — Permission-Based Authorization

**Status:** Accepted · 2026-03-31

**Karar:** `PermissionPolicyProvider` dinamik policy; `PermissionAuthorizationHandler` + cached `UserPermissionService` (5 dk TTL).

**Güçlü yönler:** Built-in ASP.NET Core authorization; dinamik policy; cached.

**Zayıflıklar / Boşluklar:**
- **5 dakika staleness bir güvenlik sorunu** — Demoted user hâlâ erişim alabilir. "Explicit cache eviction on role change" deniyor ama cache key format, invalidation API, ve reliability belirtilmemiş.
- **`ICacheService` kullanımı implicit** — CLAUDE.md "only ICacheService" kuralı var; ADR-0006 cache mekanizmasından bahsetmiyor (Redis mi? Dapr? MemoryCache?).
- **Middleware ordering enforcement yok** — Tenant middleware authorization middleware'den önce çalışmalı ama kim kontrol ediyor?
- **N+1 risk** — UserPermissionService permission resolution multi-table join mu, multi-query mi?
- **Permission revocation timing** — Role'den permission kalkınca tüm user'lar ne zaman erişim kaybeder (max 5 dk mi)?

**Consistency:** ADR-0004 + ADR-0012 ile birlikte anlamlı.

**Verdict:** 🟠 Cache eviction contract (key format + invalidation API) açıkça dokümante edilmeli; ICacheService referansı eklenmeli.

---

### ADR-0007 — Tab-Based Layout

**Status:** Accepted · 2026-03-31

**Karar:** Custom underline tab pattern (`<button>` + `border-b-2`); shadcn/Radix Tabs kullanma; useState + local state; URL param kullanma.

**Güçlü yönler:** Görsel tutarlılık; kontrollü styling; dependency-free.

**Zayıflıklar / Boşluklar:**
- **Accessibility manuel → compliance riski** — ARIA attribute'ları developer'a bırakılmış; lint rule / test yok; WCAG 2.1 AA açık footgun.
- **Keyboard navigation gerekliliği yok** — Radix out-of-the-box veriyor; custom pattern verimli değil.
- **Enforcement code review'a bırakılmış** — `*DetailPage.tsx` dosyalarını tarayan architecture test yok.
- **"Max 5 tabs" kuralı ADR'da yok** — CLAUDE.md'de var; ADR cross-reference yapmalı.

**Consistency:** CLAUDE.md UX standardı ile uyumlu; ADR kendi standardına atıfta bulunmuyor.

**Verdict:** 🟡 Accessibility için test/lint rule ekle; "Max 5 tabs" referansını ADR'a taşı.

---

### ADR-0008 — GDPR Deletion Strategy

**Status:** Accepted (Phase 1 interim) — Superseded plan for Phase 1.5.6 · 2026-03-31

**Karar:** Phase-1 soft-delete + anonymization; Phase-1.5.6 hard delete + `ConsentRecord` anonymize (Article 17(3)(e)) + cross-module cleanup events.

**Güçlü yönler:** Phased approach pragmatik; ConsentRecord retention legal rigor; cross-module event coordination.

**Zayıflıklar / Boşluklar:**
- **Phase-1 Article 17 compliance tartışmalı** — "may not satisfy strict interpretations" explicitly admission — açıkça tehlikeli; risk register'a eklenmeli.
- **Child entity listesi hardcoded ve fragile** — "9 child entities" listesi nasıl maintain ediliyor? Yeni FK eklendiğinde unutulursa?
- **Cross-module handler failure policy yok** — GdprDeletionCompletedEvent consumer'dan biri fail ederse? Rollback? Retry? Manuel intervene?
- **"Anonymized ConsentRecord" tanımı eksik** — Hangi field'lar silinir, hangileri kalır?
- **GDPR request audit trail yok** — Kim request etmiş? Validate edildi mi?
- **Phase-1.5.6 timeline yok** — Indefinite olabilir → indefinite non-compliance.

**Consistency:** ADR-0002 (schema drop), ADR-0020 (contact extensions cascade) ile uyumlu ama cross-reference zayıf.

**Verdict:** 🔴 Compliance kritik — Phase-1.5.6 için concrete milestone + child-entity enforcement mekanizması tanımlanmalı.

---

### ADR-0009 — Audit Module Repository Pattern

**Status:** Accepted · 2026-03-31

**Karar:** Audit module'da `IAuditEntryRepository` / `IAuditSettingRepository` Domain'de; EF implementation Infrastructure'da; Application handler'lar sadece repository interface'e bağımlı.

**Güçlü yönler:** Clean Architecture compliance; testability.

**Zayıflıklar / Boşluklar:**
- **Repository module-specific mi, cross-cutting mi belirsiz** — Neden sadece Audit? Tüm modüller kullanmalı mı? Peer SPEC'lerde (Identity, Documents) benzer pattern yok.
- **IQueryable sızıntı riski** — Repository IQueryable expose ediyorsa, handler hâlâ EF-aware.
- **Generic repository çok hızlı reddedilmiş** — Middle ground (base `IRepository<T>` + specific methods) keşfedilmemiş.
- **DI registration detayı yok.**

**Consistency:** ADR-0001 Clean Architecture ile uyumlu.

**Verdict:** 🟡 Pattern platform-wide mi yoksa Audit-specific mi net olmalı; net değilse genelleştirilmeli veya cross-cutting bir ADR'a dönüştürülmeli.

---

### ADR-0010 — Notification Delivery via Kafka

**Status:** Accepted · 2026-03-31

**Karar:** Notification delivery outbox → Kafka → consumer; Hangfire path disable ama kod retain edildi.

**Güçlü yönler:** Horizontal scalability; partition-based parallelism.

**Zayıflıklar / Boşluklar:**
- **Disabled Hangfire path rot riski** — Actively test edilmezse fallback bozulur.
- **Duplicate delivery mitigation (idempotency key) specified değil** — Her recipient için? Notification-level mi?
- **Kafka partition key strategy belirsiz** — Ordering guarantee tenant-level mi, recipient-level mi?
- **Delivery failure retry policy / DLQ yok.**
- **Per-notification status tracking transaction semantics vague.**

**Consistency:** ADR-0005 outbox, ADR-0011 atomicity fix ile uyumlu.

**Verdict:** 🟠 Idempotency ve DLQ stratejisi eksik — Notifications SPEC ile birleştirilmiş runbook gerekir.

---

### ADR-0011 — Outbox Service Atomicity Fix

**Status:** Accepted · 2026-03-31

**Karar:** Generic `OutboxService<TContext>`; `EnqueueAsync` sadece change tracker'a ekler; caller'ın `SaveChangesAsync`'i atomicity sağlar. Multi-tenant schema iteration + per-message isolated transactions + reflection caching + `InboxGuard<TContext>`. Addendum 1: `OutboxOptions` + validator.

**Güçlü yönler:** Critical bug-fix; multi-tenant aware; per-message isolation; configuration validation.

**Zayıflıklar / Boşluklar:**
- **Registration boilerplate** — `AddOutboxService<TContext>()` extension metodu yok.
- **Connection string duplication** — `ConnectionStrings:Default` + `Outbox:ConnectionString` bind edilmemiş.
- **Per-tenant poll latency high-count'ta problem** — 100/1000/10K tenant'ta ne olur? Per-schema processor gerekebilir.
- **Reflection cache memory growth** — `PublishMethodCache` unbounded mı?
- **New module setup mandate** — DbContext'e `OutboxMessage` konfig eklemek kolay atlanır; architecture test yok.
- **Batch efficiency** — 10K pending → tek tek mi processed?

**Consistency:** ADR-0005 amend ve ADR-0010 enable.

**Verdict:** 🟡 Extension method + architecture test + batch strategy eklensin.

---

### ADR-0012 — Tenant Management & Authorization

**Status:** Accepted · 2026-03-31

**Karar:** JWT `tenant_id` claim → `ITenantContextAccessor`; permission-based auth; NMP → admin panel tenant CRUD replacement (deferred); `PermissionScope` enum (Platform | Tenant) Phase-1.5.2.

**Güçlü yönler:** Middleware-level enforcement; granular; forward-looking PermissionScope.

**Zayıflıklar / Boşluklar:**
- **CR-01 route-based tenantId tech debt** — Güvenlik riski; architecture test eklenmeli (hangi endpoint'lerde `{tenantId:guid}` route param var).
- **Cache invalidation detayı ADR-0006 ile aynı boşluk.**
- **PermissionScope interim state belirsiz** — Phase-1.5.2 öncesi Platform-level endpoint'ler nasıl korunuyor?
- **NMP'ye transition plan belirsiz** — Admin panel tenant CRUD ne zaman devre dışı kalır?

**Consistency:** ADR-0002, 0004, 0006 ile uyumlu; ADR-0023 (NMP) ile cross-reference eksik.

**Verdict:** 🟠 Platform vs Tenant scope interim policy + route-param architecture test.

---

### ADR-0013 — Cache Cross-Instance Invalidation

**Status:** Accepted · 2026-03-31

**Karar:** `CacheInvalidationEvent` Dapr pub/sub → tüm instance'lar L1 in-memory evict; self-publish InstanceId filter.

**Güçlü yönler:** Temiz two-tier cache model; clever InstanceId pattern; Dapr reuse.

**Zayıflıklar / Boşluklar:**
- **`ICacheService` adı ADR'da geçmiyor** — CLAUDE.md "only ICacheService" kuralı; implementation interface adlandırması implicit.
- **InstanceId collision mitigation vague** — `Guid.NewGuid()` yeterli mi? ProcessId/hostname eklenmeli mi?
- **Broadcast efficiency under high load** — Batching/debouncing future work.
- **50ms window measured değil.**
- **Enforcement — `RemoveByPrefixAsync` publish'i contract test edilmiyor.**

**Consistency:** ADR-0014 ile uyumlu.

**Verdict:** 🟡 `ICacheService` referansı eklensin; InstanceId stability güçlendirilsin.

---

### ADR-0014 — Distributed Consistency Patterns

**Status:** Accepted · 2026-04-01

**Karar:** 4-tier consistency: Tier-1 single DB tx; Tier-2A DB-first + non-fatal external; Tier-2B external-first + compensation; Tier-3 payment gateway (idempotency + pending + webhook + reconciliation); Tier-4 Saga (Phase-4).

**Güçlü yönler:** Gerçek incident'lere dayanıyor (Keycloak orphans); Tier-3 gold standard; Saga defer mantıklı.

**Zayıflıklar / Boşluklar:**
- **Keycloak reconciliation job designed değil** — Phase-2 ama unscheduled; Keycloak orphan tech debt.
- **Result<T> pattern referansı yok** — Tier-2B compensation handler'lardan nasıl döner?
- **Tier-2B try/catch pizza-code riski** — Abstraction (`ICompensationHandler`) önerilmemiş.
- **Tier-2B compensation idempotency unaddressed.**
- **Tier-3 "pending-first" state machine tanımlanmamış.**

**Consistency:** ADR-0018, 0019, 0023 bu pattern'i kullanıyor.

**Verdict:** 🟠 Keycloak reconciliation job scheduled olmalı; compensation handler abstraction önerilsin.

---

### ADR-0015 — Roadmap Structure

**Status:** Accepted · 2026-04-22

**Karar:** Phase files + `current.md` sync point + `T-NNN.md` task files; explicit status vocabulary (Proposed → In Progress → In Review → Done | Blocked | Deferred | Superseded).

**Güçlü yönler:** Monolithic roadmap problem'ini çözüyor; status vocabulary disciplined.

**Zayıflıklar / Boşluklar:**
- **Sync rule enforcement yok** — CI/CD hook veya PR checklist yok.
- **Migration plan separate file'da, kendisi provide edilmemiş.**
- **Phase model versioning yok.**
- **Numbering format change (NNN→NNNN) redirect strategy eksik.**
- **Task owner/assignee field yok.**
- **Link-check CI mentioned but not specified.**

**Consistency:** ADR-0016 ile uyumlu.

**Verdict:** 🟡 CI hook (current.md ↔ phase change + task state) eklensin; task ownership field.

---

### ADR-0016 — Module Tier Classification

**Status:** Accepted · 2026-04-22

**Karar:** Tier 1 Platform Core / Tier 2 Enterprise / Tier 3a NGO + 3b Education / Tier 4 Extensions; dependency `4→3→2→1`; Tier-3'ler orthogonal.

**Güçlü yönler:** Temiz dependency graph; ticari modelle hizalı; tier stable; architecture test intent.

**Zayıflıklar / Boşluklar:**
- **"Domain-neutral vocabulary" rule Tier-1'de enforce edilmemiş** — Contacts/CRM'de vertical leak var (cleanup Prompt-02'ye deferred).
- **Architecture test'ler Phase-2** — Phase-1'de enforcement sıfır.
- **Per-module dependency matrix yok** — CRM Audit'e bağlı olabilir mi?
- **Tier-3 orthogonality enforcement eksik** — Tier-3a'dan Tier-3b import'u engelleyen test yok.
- **Tier-4 scope "narrow" belirsiz.**
- **Module reclassification process yok.**
- **Manifest tier field ↔ ADR-0017 bidirectional link eksik.**

**Consistency:** ADR-0015, 0017, 0019, 0020 ile uyumlu.

**Verdict:** 🟠 Architecture test Phase-1'de yazılmalı — Tier ihlali kritiktir.

---

### ADR-0017 — Portal Extension Architecture

**Status:** Accepted · 2026-04-22

**Karar:** `module.manifest.yaml` v1 schema; backend per-tenant assembly; frontend lazy-load; SRI Tier-4 için.

**Güçlü yönler:** Build-time decoupling; lazy-load; versioned schema; graceful degrade.

**Zayıflıklar / Boşluklar:**
- **JSON Schema file Phase-2 work** — Phase-1'de validation yok.
- **License gate mekanizması tanımsız** — Tenant'ın lisans tier'ı nereden okunuyor? Subscription/Finance bağlantısı yok.
- **Permission filtering per-user vs per-tenant belirsiz** — Performance impact var.
- **Widget slot registry — `dashboard.main` vs nerede tanımlı?** Slot registry canonical source eksik.
- **i18n JSON dosyaları — bundled mi, separate fetch mi? CDN mi?**
- **Tier-4 SRI Phase-4'e deferred — marketplace güvenlik açığı.**
- **Manifest version skew (v1 ↔ v2) forward/backward compat yok.**

**Consistency:** ADR-0016 ile uyumlu.

**Verdict:** 🟠 JSON Schema + license gate → Phase-2 Milestone A öncesi.

---

### ADR-0018 — Payment Provider Strategy

**Status:** Accepted · 2026-04-22

**Karar:** `IPaymentProvider` port; Stripe global default, iyzico TR; circuit-breaker failover (Polly 5 fail/60s); ops override 24h TTL.

**Güçlü yönler:** Domain isolation; regional fit; resilient.

**Zayıflıklar / Boşluklar:**
- **Manual override audit mechanism tanımsız.**
- **`IPaymentProviderResolver` interface signature yok.**
- **Polly config sparse** — Retry policy / backoff / jitter detail yok.
- **Webhook signature unified validator yok** — Code duplication riski.
- **Reconciliation job referansı var ama modül/schedule yok.**
- **Secret rotation strategy yok.**
- **`Money` VO explicit kullanımı belirtilmemiş.**

**Consistency:** ADR-0014 Tier-3 pattern implementasyonu; ADR-0019 tarafından kullanılıyor.

**Verdict:** 🟠 Reconciliation job sahipliği + `Money` kullanımı explicit olmalı.

---

### ADR-0019 — Recurring Billing vs Recurring Donations

**Status:** Accepted · 2026-04-22

**Karar:** İki ayrı recurring engine (Subscription Tier-2 + Fundraising Tier-3a), tek `IPaymentProvider` port.

**Güçlü yönler:** Module boundary preserved; semantic distinction; independent dunning; independent lifecycle.

**Zayıflıklar / Boşluklar:**
- **SharedKernel payment port'un ADR-0018'den önceliği implicit.**
- **Dual Hangfire scheduler collision yok** — `subscription:generate-invoices` + `fundraising:charge-recurring` aynı anda fire olursa provider contention.
- **Stored payment method duplication sync mekanizması yok** — Donor hem subscriber olursa iki row nasıl sync?
- **Retry-policy config format unspecified.**
- **Idempotency key max length validation yok** (Stripe limiti).
- **Observability Meter duplication** — `nexora.payments` tek Meter + `module` label daha iyi olurdu.
- **Annual tax statement Reporting cross-module join** — Module isolation violation.

**Consistency:** ADR-0016, 0018, 0005 ile uyumlu.

**Verdict:** 🟠 Job collision strategy + payment method sync tanımlanmalı.

---

### ADR-0020 — Contact Extensions by Vertical Modules

**Status:** Accepted · 2026-04-22

**Karar:** `contact_extensions` side table (id, contact_id, module_id, payload jsonb); Tier-1 Contacts owner; Tier-3/4 vertical payload owner; `IContactExtensionRepository` write; `IContactExtensionProjector` read.

**Güçlü yönler:** Tier-1 domain-neutral; surgical uninstall; no row-lock contention; GDPR cascade.

**Zayıflıklar / Boşluklar:**
- **Payload versioning tooling yok** — `payload_version` field? Migration guide?
- **`IContactExtensionProjector` interface tanımsız** — SharedKernel'da mı, nerede?
- **Projection cache TTL/invalidation + `ICacheService` kullanımı belirsiz.**
- **Cross-vertical read permission matrix sketchy.**
- **GDPR anonymization hook contract undefined.**
- **Architecture test "no Tier-1 references vertical module_id" detection mekanizması vague.**
- **Vertical index placement guidance yok.**

**Consistency:** ADR-0016, 0008, 0017 ile uyumlu.

**Verdict:** 🟠 Interface contract'ları + payload versioning policy eklenmeli.

---

### ADR-0021 — Money & Exchange-Rate Contract

**Status:** Proposed · 2026-04-22 (**→ Accepted olmalı; downstream SPEC'ler assume ediyor**)

**Karar:** `Money { decimal Amount, string CurrencyCode }` SharedKernel VO; `IExchangeRateService` Finance owned; daily 06:00 UTC snapshot; TCMB/ECB/OpenExchangeRates; Amendment 1: OpenExchangeRates fallback + default asOf = UtcNow.

**Güçlü yönler:** 7 driver karşılanmış; Option A net; Amendment iki TODO'yu kapatıyor; event-handler rule (business date not UtcNow) mükemmel practice.

**Zayıflıklar / Boşluklar:**
- **Status Proposed kalmamalı.**
- **`IExchangeRateService` adı Subscription+Finance SPEC'lerde `IExchangeRateProvider` olarak geçiyor — tutarsızlık.**
- **`Money` constructor "strict-totals mode" tanımsız.**
- **Default asOf interface overload vs default(DateOnly) ambiguity.**
- **Architecture test enforcement Phase-1'de yok (Phase-3'te enable ediliyor).**

**Consistency:** Finance SPEC, Subscription SPEC, Projects SPEC, HR SPEC, Fundraising SPEC, CRM SPEC, Events SPEC, Documents SPEC, Reporting SPEC bu kontratı assume ediyor.

**Verdict:** 🔴 Status→Accepted; interface adı disambiguation kritik.

---

### ADR-0022 — Two-Tier Locale Resolution

**Status:** Proposed · 2026-04-22 (**Phase-1.5.3'te zaten ship edilmiş** → Accepted olmalı)

**Karar:** `LocalizationResource` (platform baseline) + `LocalizationOverride` (tenant override); L1 (5min) + L2 Redis (30min); `localization.key.updated` event invalidation; module key prefix `lockey_{moduleName}_*`; en/tr parity mandatory; Amendment 1: `Nexora.Infrastructure.Localization` namespace.

**Güçlü yönler:** Already-shipped formalize; architecture test (`^lockey_` regex); CI parity check; namespace pattern peer infra'larla tutarlı.

**Zayıflıklar / Boşluklar:**
- **Status Proposed kalmamalı.**
- **`localization.key.updated` Kafka topic adı belirtilmemiş.**
- **`invalidatesAll` vs `tenantId == null` semantiği belirsiz.**
- **Frequently-changing tenant override TTL strategy yok.**

**Consistency:** ADR-0013, 0016 ile uyumlu.

**Verdict:** 🟡 Accept + topic name + semantic clarity.

---

### ADR-0023 — NMP Billing Model

**Status:** Proposed · 2026-04-22

**Karar:** `Plan` + `NmpSubscription` + `Entitlement` + `NmpInvoice` + `WebhookLedger`; SaaS runtime `/internal/license/verify` → `Entitlement` O(1); on-prem RSA-2048 signed license key + daily phone-home; Stripe+iyzico webhook dedup.

**Güçlü yönler:** Clean Plan/Subscription/Entitlement ayrımı; O(1) check; on-prem air-gapped support; WebhookLedger idempotency.

**Zayıflıklar / Boşluklar:**
- **2 unresolved TODO (proration + reporting currency) NMP.2 öncesi kapanmalı.**
- **`grace_until` nullable tutarsızlığı** — ER diagram `datetime`, API contract `DateTime?`.
- **`tenant.entitlement.updated` cross-codebase pub/sub transport belirsiz** — Separate Dapr? Shared Kafka?
- **Architecture test (Nexora.Management.* references) Phase-2 ile entegre olmalı.**

**Consistency:** ADR-0016, 0017, 0018, 0019, 0021 ile uyumlu.

**Verdict:** 🟠 2 TODO + cross-codebase event transport NMP.2 kick-off öncesi kapanmalı.

---

### ADR-0024 — SignalR for Events Real-Time

**Status:** Proposed · 2026-04-22

**Karar:** ASP.NET Core SignalR + Redis backplane, sadece Events-NGO module scope; module-owned hub endpoint; `{tenantId}:{eventId}` group.

**Güçlü yönler:** Scope discipline (sadece Events-NGO); alternatifler değerlendirilmiş; APISIX JWT reuse; secret ISecretProvider.

**Zayıflıklar / Boşluklar:**
- **"Redis backplane via Dapr State Store" ifadesi teknik olarak yanlış** — `AddStackExchangeRedis(connectionString)` doğrudan Redis pub/sub'a bağlanır, Dapr State Store API Redis pub/sub expose etmez.
- **ADR Ledger referans hatası** — "Consumes ADR-0007 (Caching/Redis)" → ADR-0007 tab-based-layout; cache için ADR-0013.
- **Azure SignalR Service alternatifi on-prem uyumsuz, gerekçesiz eklenmiş.**
- **JWT expiration + WebSocket re-auth pattern yok** (accessTokenFactory).
- **Disconnection + missed event replay strategy yok.**
- **Sticky session / ingress session affinity config belirtilmemiş.**
- **Observability tamamen eksik** (metric, tracing, health check).
- **Frontend Zustand/TanStack Query integration yok.**
- **Scale limits ve lockey policy eksik.**
- **Test strategy yok.**

**Consistency:** ADR-0002 ile uyumlu.

**Verdict:** 🔴 "Dapr State Store" ifadesi ve ADR-0007 referansı yanlış — düzeltilmeli; JWT+disconnection pattern tanımlanmalı.

---

## 2. Module SPEC Reviews (16 modül)

### Tier 1 — Platform Core

#### Identity
- **Status:** Implemented · v1.0.0
- **Güçlü:** Temiz foundational module; Keycloak realm-per-tenant; dual cache invalidation (inline + event); strongly-typed IDs.
- **Sorunlar:**
  - Keycloak credentials için `ISecretProvider` usage explicit değil.
  - Integration surface doğru; stale reference yok.
- **Verdict:** ✅ OK (minor: ISecretProvider wording).

#### Contacts
- **Status:** Implemented
- **Sorunlar:**
  - 🔴 **Stale module references** — Events/diagram hâlâ `Donations`, `Sponsorship` ayrı modül; `Fundraising` olarak güncellensin.
  - 🟠 Permission naming inconsistency — bu SPEC `contacts.contacts.create/update/delete` pattern; peer SPEC'ler `contacts.contacts.read/write`. Platform-wide normalize edilmeli.
  - 🟠 `Contact.organization_id` vs "tenant-wide visibility" semantik netleştirilmeli.
  - 🟡 `ContactActivity.module_source` string → validated constant set.
  - 🟡 `ConsentRecord.ip_address` string → normalized IPv4/IPv6.
- **Verdict:** 🟠 Stale reference + permission naming kritik.

#### Documents
- **Status:** Implemented
- **Sorunlar:**
  - 🔴 **Stale module references** — `Donations` ayrı modül olarak listeli (lines 310, 326, 31, 410); `Fundraising` + `fundraising.donation.confirmed` olarak güncellensin.
  - 🟠 **ADR-0021 uyum boşluğu** — Template variables "tuition amount, currency" ayrı field; `SharedKernel.Money` kullanılmalı.
  - 🟡 Events Consumed table heading "Trigger" → "Event + Source Module" daha net.
- **Verdict:** 🔴 Stale reference + Money compliance kritik.

#### Audit
- **Status:** Implemented · 2026-03-29
- **Güçlü:** Standalone module; MediatR pipeline integration; JSONB + monthly partitioning; configurable per-operation.
- **Sorunlar:**
  - 🟡 `[AuditMask]` default masked field listesi dokümante edilmeli (passwords, tokens, SSN, PII).
  - 🟡 Depth limit + max payload spec main section'da explicit yapılmalı.
- **Verdict:** ✅ OK (minor).

#### Reporting
- **Status:** Implemented · v1.0.0
- **Güçlü:** SQL sandbox (READ ONLY + search_path + timeout); streaming exports; widget framework; locale-aware templates.
- **Sorunlar:**
  - 🔴 **ADR-0021 uyum boşluğu** — Template variables "amount, currency" ayrı (lines 375, 425-431); `Money` kullanılmalı.
  - 🟡 Widget data fetching sequence diagram eksik.
  - 🟡 Template jurisdiction-bound legal text vs `lockey_` UI labels disambiguation.
  - Phase-2+ TODO (e-Fatura, AR/FR locales, legal sign-off) explicit ve meşru.
- **Verdict:** 🟠 Money compliance kritik.

#### Notifications
- **Status:** Implemented
- **Güçlü:** Kafka-based outbox delivery; HTML encoding + CR/LF strip; multi-provider fallback.
- **Sorunlar:**
  - 🔴 **Stale event references** — `donations.donation.confirmed` → `fundraising.donation.confirmed`.
  - 🟠 **`NotificationProvider.config` encrypted api_key DB'de** — CLAUDE.md INFRASTRUCTURE_STANDARDS `ISecretProvider` mandate; sadece secret reference key DB'de tutulmalı.
  - 🟠 **`Notification.body_rendered` GDPR riski** — Rendered PII anonymization/retention policy gerekir.
- **Verdict:** 🔴 Secret storage pattern kritik.

#### Portal Framework
- **Status:** In Review · shipped Phase 1.5
- **Güçlü:** Next.js 16 + NextAuth v5 + `RefreshAccessTokenError` pattern; RTL-ready logical CSS; module manifest consumption.
- **Sorunlar:**
  - 🟡 `isSafeUrl` kriter tanımı yok (allowlist? schema-check?).
  - 4 TODO Phase-2 içinde, meşru.
  - Test coverage % hedefi yok.
- **Verdict:** ✅ OK.

#### Admin Dashboard
- **Status:** In Review · Phase 1
- **Güçlü:** React 19 + Vite + shadcn; UX/UI standards compliant; 358 tests.
- **Sorunlar:**
  - 🟡 `/api/v1/portal/manifests?layout=admin` endpoint ownership "shared with Portal Framework" — kim implement ediyor belirsiz.
  - 🟡 Admin-specific slots (`dashboard.kpi`, `topbar.actions`, `sidebar.extra`) canonical slot registry'de yok.
  - Manifest endpoint hata durumu fallback yok.
- **Verdict:** ✅ OK (minor).

---

### Tier 2 — Enterprise Core

#### Projects
- **Status:** In Review
- **Güçlü:** Kapsamlı ER; fractional indexing + nightly rebalance; WIP warning-not-blocking; 3 state machine; CRM/Documents/Finance integration well-specified.
- **Sorunlar:**
  - 🔴 **ADR-0021 uyum boşluğu** — `TimeEntry.billable_rate_amount + billable_rate_currency`, `ProjectMember.default_hourly_rate_*`, `Project.budget_total_amount` (currency-less) → `Money` kullanılmalı.
  - 🟡 Gantt FS-only dependency — TODO ile flagged, OK.
  - 🟡 HR.EmployeeDeactivated stub — Phase 2.5 öncesi contact-only senaryosu netleştirilmeli.
  - 🟡 Archived project retention policy eksik.
- **Verdict:** 🔴 Money compliance kritik.

#### HR Core
- **Status:** In Review
- **Güçlü:** Generic employer backbone; four-eyes payroll rule explicit; leave state machine; ADR-0020 teacher/volunteer extension compliance.
- **Sorunlar:**
  - 🔴 **ADR-0021 uyum boşluğu (yaygın)** — `EmploymentContract.salary_*`, `PayrollRun.total_*`, `PayrollLine.gross/deductions/net + currency` → `Money`.
  - 🟠 `hr.self-service.*` auto-grant mekanizması belirsiz — permission seeder mi, Keycloak mapping mi?
  - 🔴 Retention matrix TODO production öncesi kapanmalı.
  - 🟡 Four-eyes domain invariant'ı entity seviyesinde güçlendirilmeli (sadece handler'da değil).
- **Verdict:** 🔴 Money + retention kritik.

#### CRM
- **Status:** In Review
- **Güçlü:** Domain-neutral (vertical vocabulary yok); event-driven 360-cache (ADR-0014 uyumlu); per-pipeline custom fields; workflow v1 scoped.
- **Sorunlar:**
  - 🔴 **ADR-0021 uyum boşluğu** — `Opportunity.expected_revenue_amount + currency` → `Money`.
  - 🟠 `crm.contact360.updated` event Produced tablosunda yok (metinde var).
  - 🟠 Contact360Publisher outbox table name TODO — Contacts spec koordinasyonu blocker.
  - 🟡 `Lead.priority` ER'de `int 0-3`, VO'da `Priority` enum — tutarsızlık.
  - 🟡 500K leads/org pagination strategy yok.
- **Verdict:** 🟠 Money + blocking TODO.

#### Subscription
- **Status:** In Review · v2.0.0
- **Güçlü:** Clean scope (GL yok, tax engine yok); upgrade immediate-proration / downgrade end-of-period; dunning + feature-gating signal; audit coverage excellent.
- **Sorunlar:**
  - 🔴 **`IExchangeRateProvider` (§7) vs ADR-0021 `IExchangeRateService`** — tutarsız.
  - 🔴 **Dunning schedule çelişki** — §6 text "4 attempt / 8 gün"; diagram note "3 retries / 7 gün (1d, 3d, 7d)". Hangisi doğru?
  - 🟡 `cancel_at_period_end` type — `date` yerine `timestamp`.
  - 🟡 Pending downgrade + cancellation edge case specified değil.
- **Verdict:** 🔴 Interface name + dunning schedule kritik.

#### Finance
- **Status:** In Review
- **Güçlü:** Generic B2B GL; double-entry integrity; Subscription revenue recognition (deferred vs immediate); reconciliation multi-pass; Fundraising restricted-fund delegation clean.
- **Sorunlar:**
  - 🟠 **`ExchangeRate` double-definition** — SharedKernel type + Finance persistence record — explicitly clarified edilmeli.
  - 🔴 **`IExchangeRateProvider` naming** — ADR-0021 ile align edilmeli.
  - 🟡 `Account.dimensions_schema` JSONB — schema reference eksik.
- **Verdict:** 🟠 Interface name + ExchangeRate split dokümante edilmeli.

---

### Tier 3 — Vertical Editions

#### Fundraising (Tier 3a NGO)
- **Status:** In Review
- **Güçlü:** 3 capability + 1 opt-in; ADR-0019 cross-reference; receipt template delegation; clean tier boundary; Zakat/Qurban opt-in elegant.
- **Sorunlar:**
  - 🔴 **ADR-0021 uyum boşluğu (yaygın)** — `Donation.amount + currency`, `RecurringDonationPlan.amount + currency`, `Campaign.goal_amount + goal_currency`, `Sponsorship.monthly_amount + currency` → `Money`.
  - 🟡 500+ satırlık legacy appendix noise — separate archive'a taşınsın.
  - 🟡 `Receipt.template_key` free-form string — Reporting template registry bağlantısı yok.
  - 🟡 `ContactRef` abstraction sadece bu modülde — tutarlılık.
- **Verdict:** 🔴 Money compliance + legacy cleanup.

#### Events-NGO (Tier 3a)
- **Status:** In Review · v1.0.0 — En detaylı SPEC
- **Güçlü:** Generic vs NGO-seasonal ayrımı via feature flag; ADR-0016 forward-note (Tier-2 extraction); background job schedule explicit; HMAC-SHA256 QR; NFR extensive.
- **Sorunlar:**
  - 🔴 **Layer naming yanlış** — `Presentation/Controllers/` CLAUDE.md `Api/` kuralına aykırı.
  - 🔴 **RLS claim yanlış** — "PostgreSQL RLS policies" — Nexora schema-per-tenant + EF global filters; RLS kullanılmıyor.
  - 🔴 **SignalR dependency ADR-0024 ile kapandı** ✅ (önceki review'daki concern resolved).
  - 🟠 `IntegrationEventEnvelope<T>` inline tanım — SharedKernel / ADR-0011 envelope reuse edilmeli.
  - 🟠 **Stale event** — `DonationReceivedIntegrationEvent` source `donations` → `fundraising`.
  - 🔴 **ADR-0021 uyum** — `events_ticket_type.price + currency` separate.
  - 🟡 Event topic naming (snake vs PascalCase) inconsistent.
- **Verdict:** 🔴 Architectural claims düzeltilmeli (RLS, layer).

#### Education (Tier 3b)
- **Status:** In Review
- **Güçlü:** ADR-0020 explicit usage notes; CRM pipeline reuse (custom template=enrollment); HR extension for teachers; parent portal; COPPA/FERPA awareness.
- **Sorunlar:**
  - 🟠 **`SECTION.homeroom_teacher_employee_id` cross-module FK** — module boundary ihlali; `contact_id` üzerinden çözülmeli.
  - 🟡 350+ satırlık legacy appendix non-normative ama karışıklık yaratıyor.
  - 🟠 COPPA/FERPA consent mekanizma operasyonel tanım eksik.
  - 🔴 Retention matrix TODO (jurisdiction-specific) production öncesi kapanmalı.
- **Verdict:** 🟠 FK boundary + retention + legacy cleanup.

---

## 3. Cross-cutting Findings

### 3.1 ADR-0021 Money Compliance (Pervasive Gap)

Amount+currency ayrı field pattern'i kullanan modüller:

| Modül | Entities / Fields |
|-------|-------------------|
| Projects | `TimeEntry.billable_rate_*`, `ProjectMember.default_hourly_rate_*`, `Project.budget_total_amount` (currency-less) |
| HR | `EmploymentContract.salary_*`, `PayrollRun.total_*`, `PayrollLine.gross/deductions/net + currency` |
| Fundraising | `Donation.*`, `RecurringDonationPlan.*`, `Campaign.goal_*`, `Sponsorship.monthly_*`, `SponsorshipInstallment.*`, `Receipt` |
| CRM | `Opportunity.expected_revenue_*` |
| Events-NGO | `events_ticket_type.price + currency`, `events_event_sponsor.contribution_amount + currency` |
| Documents | Template variables (amount, currency separate) |
| Reporting | Template variables (amount, currency separate) |

**Aksiyon:** Phase-2 "ADR-0021 compliance sweep" task'ı — her modül için ER + event payload + API DTO + template variable sweep; architecture test `Money` usage enforce etsin.

### 3.2 Interface Name Drift

| ADR | Interface Name |
|-----|---------------|
| ADR-0021 | `IExchangeRateService` |
| Subscription SPEC §7 | `IExchangeRateProvider` |
| Finance SPEC | `IExchangeRateProvider` |

**Aksiyon:** Single name consolidation — ADR-0021 authoritative, SPEC'ler align.

### 3.3 Stale Module References (`Donations`/`Sponsorship` → `Fundraising`)

| Modül | Referans yeri |
|-------|---------------|
| Contacts | Events Consumed + Integration diagram |
| Documents | Event Producers diagram + consumed events + module_name field |
| Notifications | Events Consumed table |
| Events-NGO | `DonationReceivedIntegrationEvent` source |
| Identity | Tables referenced "Donations" in event consumers |
| Reporting (borderline — only Fundraising used, OK) | ✅ |

**Aksiyon:** Global find-replace + event name migration.

### 3.4 ADR Status Drift

| ADR | Current | Should Be | Rationale |
|-----|---------|-----------|-----------|
| 0021 | Proposed | **Accepted** | Downstream SPEC'ler authoritative assume ediyor |
| 0022 | Proposed | **Accepted** | Phase 1.5.3'te zaten implemented |
| 0023 | Proposed | Proposed (OK) | NMP.2 öncesi 2 TODO kapanmalı |
| 0024 | Proposed | Accepted (after fixes) | Events-NGO SPEC assume ediyor |

### 3.5 Phase-1 Enforcement Gaps (Architecture Tests)

Hiçbiri Phase-1'de mekanik olarak doğrulanmıyor:

- ADR-0015 sync-rule (current.md ↔ task state)
- ADR-0016 tier-boundary (Tier-1 vertical vocabulary; Tier-3 orthogonality)
- ADR-0020 "no Tier-1 references vertical module_id"
- ADR-0021 "no module outside SharedKernel/Finance declares Money/ExchangeRate"
- ADR-0022 `^lockey_` regex scan
- ADR-0012 route-based tenantId exception scope

**Aksiyon:** Phase-2 Milestone A: `tests/Nexora.Architecture.Tests/` package + CI wiring.

### 3.6 CLAUDE.md Standard References Implicit

Several ADRs don't explicitly cite standards they rely on:

- ADR-0006 → `ICacheService` (CLAUDE.md mandate)
- ADR-0013 → `ICacheService`
- ADR-0014 → `Result<T>` pattern
- ADR-0018 → `Money` VO, `ISecretProvider` rotation
- ADR-0020 → `ICacheService` for projection cache
- ADR-0008 → soft-delete + global query filter semantics

**Aksiyon:** Her ADR'ın "References" section'ına CLAUDE.md Infrastructure Standards link.

### 3.7 Architectural Claim Mismatches

- **Events-NGO: PostgreSQL RLS** ↔ Platform schema-per-tenant + EF global filters.
- **Events-NGO: `Presentation/Controllers/`** ↔ CLAUDE.md `Api/` layer.
- **ADR-0024: "Redis backplane via Dapr State Store"** ↔ `AddStackExchangeRedis` doğrudan Redis; Dapr State Store pub/sub expose etmez.
- **ADR-0024 Ledger: "Consumes ADR-0007 (Caching/Redis)"** ↔ ADR-0007 tab-based-layout; cache için ADR-0013.

### 3.8 Legacy Appendix Noise

- **Fundraising SPEC** — 500+ satır legacy Donations + Sponsorship verbatim.
- **Education SPEC** — 350+ satır legacy spec verbatim.

Non-normative işaretli ama implementor için kafa karıştırıcı; ayrı `docs/_archive/` klasörüne taşınsın.

### 3.9 Permission Naming Inconsistency

| Pattern | Kullanan |
|---------|---------|
| `{module}.{resource}.{action}` with `create/update/delete` | Contacts (kendi SPEC'i) |
| `{module}.{resource}.{action}` with `read/write` | Projects, HR, CRM, Subscription, Finance, Fundraising, Events |

Platform-wide normalize edilmeli.

### 3.10 GDPR / Retention Compliance Gaps

- **ADR-0008 Phase-1.5.6** timeline yok, indefinite non-compliance window.
- **HR retention matrix TODO** — jurisdiction-specific.
- **Education retention matrix TODO** — COPPA/FERPA/KVKK.
- **Notifications `body_rendered`** PII retention policy yok.

---

## 4. Tablosal Compliance Matrix

| Modül | ADR-0021 Money | Stale Refs | Layer | Multi-tenancy | Secrets | Permissions | Legacy Noise |
|-------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Identity | N/A | ✅ | ✅ | ✅ | ⚠️ impl. | ✅ | ✅ |
| Contacts | N/A | ❌ | ✅ | ⚠️ semantics | ✅ | ⚠️ naming | ✅ |
| Documents | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Audit | N/A | ✅ | ✅ | ✅ | N/A | ✅ | ✅ |
| Reporting | ❌ | ✅ | ✅ | ✅ | N/A | ✅ | ✅ |
| Notifications | N/A | ❌ | ✅ | ✅ | ❌ enc. DB | ✅ | ✅ |
| Portal Framework | N/A | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Admin Dashboard | N/A | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Projects | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| HR | ❌ | ✅ | ✅ | ✅ | ✅ | ⚠️ self-serv. | ✅ |
| CRM | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Subscription | ⚠️ IX-name | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Finance | ⚠️ IX-name | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Fundraising | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ legacy |
| Events-NGO | ❌ | ❌ | ❌ Present. | ❌ RLS claim | ✅ | ✅ | ✅ |
| Education | N/A (Subscription owns) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ legacy |

Legend: ✅ OK · ⚠️ Minor · ❌ Critical

---

## 5. Priority-Ordered Action List

### 🔴 Critical (Phase-2 kick-off blockers)

| # | Action | Owner | ADRs/SPECs |
|---|--------|-------|-----------|
| 1 | ADR-0021 uyum sweep — `Money` value object platform-wide | Platform | Projects, HR, Fundraising, CRM, Events-NGO, Documents, Reporting, Subscription |
| 2 | `IExchangeRateService` vs `IExchangeRateProvider` disambiguation | Platform | ADR-0021, Subscription, Finance |
| 3 | Stale module references sweep (`Donations`/`Sponsorship` → `Fundraising`) | Platform | Contacts, Documents, Notifications, Events-NGO, Identity |
| 4 | Events-NGO: RLS claim düzelt (schema-per-tenant + EF global filters) | Events team | Events-NGO SPEC |
| 5 | Events-NGO: `Presentation/Controllers/` → `Api/` | Events team | Events-NGO SPEC |
| 6 | Subscription dunning schedule tutarsızlığı çöz (text vs diagram) | Subscription team | Subscription SPEC §6 |
| 7 | Notifications: Secret storage → `ISecretProvider` (not encrypted DB) | Notifications team | Notifications SPEC |
| 8 | ADR-0024: "Dapr State Store" wording fix + ADR-0007 → ADR-0013 reference | Platform | ADR-0024 |
| 9 | ADR-0021 & ADR-0022 status → Accepted | Maintainer | ADR |
| 10 | ADR-0023: 2 TODO (proration + reporting currency) resolve before NMP.2 | NMP team | ADR-0023 |
| 11 | HR retention matrix + Education retention matrix (compliance) | Platform | HR SPEC, Education SPEC, retention.md |
| 12 | ADR-0008: Phase-1.5.6 concrete milestone date | Platform | ADR-0008 |

### 🟠 Important (Phase-2 early milestones)

| # | Action | Target |
|---|--------|--------|
| 13 | Architecture test package — tier boundary + Money + lockey + route tenantId | `tests/Nexora.Architecture.Tests/` |
| 14 | ADR-0024: JWT re-auth + disconnection replay + sticky session config | ADR-0024 |
| 15 | ADR-0024: Observability + Azure SignalR on-prem guidance | ADR-0024 |
| 16 | ADR-0018: Reconciliation job ownership + `Money` VO + secret rotation | ADR-0018 |
| 17 | ADR-0019: Dual Hangfire collision + payment method sync | ADR-0019 |
| 18 | ADR-0014: Keycloak reconciliation job schedule + compensation handler abstraction | ADR-0014 |
| 19 | ADR-0017: Portal manifest JSON Schema + license gate mechanism | ADR-0017 |
| 20 | ADR-0020: `IContactExtensionProjector` interface + payload versioning | ADR-0020 |
| 21 | Contacts: Permission naming normalize + organization_id semantics | Contacts SPEC |
| 22 | Education: `homeroom_teacher_employee_id` cross-module FK → `contact_id` | Education SPEC |
| 23 | CRM: Contact360Publisher outbox table coordination + `crm.contact360.updated` in Events Produced table | CRM SPEC |
| 24 | HR: `hr.self-service.*` auto-grant mechanism dokümante et | HR SPEC |
| 25 | HR: Four-eyes domain invariant entity seviyesinde güçlendir | HR SPEC |
| 26 | Finance: `ExchangeRate` split (SharedKernel type vs persistence record) dokümante et | Finance SPEC |
| 27 | Projects: HR.EmployeeDeactivated stub netleştir | Projects SPEC |
| 28 | Notifications: `body_rendered` PII retention policy | Notifications SPEC |
| 29 | Fundraising + Education: Legacy appendix → `docs/_archive/` | Both SPECs |
| 30 | ADR-0005: ADR-0011 supersede note ekle | ADR-0005 |
| 31 | ADR-0006 + 0012: Cache eviction contract explicit (key + invalidation API) | ADR |
| 32 | ADR-0012: Route-based tenantId architecture test | ADR-0012 |
| 33 | ADR-0010: Idempotency key + DLQ strategy | ADR-0010 |
| 34 | ADR-0016: Architecture tests Phase-2'ye değil Phase-1.6'ya çek | ADR-0016 |
| 35 | ADR-0002: Migration orchestration runbook | Ops |
| 36 | ADR-0003: License lifecycle + Helm upgrade runbook | Ops |
| 37 | ADR-0015: CI hook (current.md ↔ phase change) | Platform |
| 38 | Portal Framework: `isSafeUrl` kriterleri tanımla | Portal team |
| 39 | Admin Dashboard: `/api/v1/portal/manifests` endpoint ownership + slot registry | Portal team |

### 🟡 Minor / Documentation Cleanup

| # | Action |
|---|--------|
| 40 | ADR-0022: Kafka topic name + `invalidatesAll` semantics |
| 41 | ADR-0013: `ICacheService` cross-reference + InstanceId stability |
| 42 | ADR-0024: lockey policy (push edilen mesajlar key mi rendered mi?) |
| 43 | Each ADR References section → CLAUDE.md Infrastructure Standards link |
| 44 | Permission naming normalize (platform-wide read/write vs CRUD) |
| 45 | Events-NGO event topic naming (snake_case vs PascalCase) |
| 46 | CRM: `Lead.priority` ER ↔ VO tutarlılık |
| 47 | Subscription: `cancel_at_period_end` date → timestamp |
| 48 | Audit: `[AuditMask]` default field listesi |
| 49 | Reporting: Widget data fetch sequence diagram |
| 50 | Contacts: `ContactActivity.module_source` → validated constant |
| 51 | Contacts: `ConsentRecord.ip_address` → normalized IPv4/IPv6 |
| 52 | Fundraising: `Receipt.template_key` → Reporting template registry reference |
| 53 | Portal Framework: Test coverage % target |
| 54 | ADR-0007: "Max 5 tabs" + accessibility test/lint rule |
| 55 | ADR-0009: Repository pattern platform-wide genelleştir veya Audit-specific kal |
| 56 | ADR-0023: `grace_until` type tutarlılığı (ER vs API contract) |

---

## 6. Sonuç

Nexora dokümantasyon seti mimari açıdan olgun ve iç tutarlı. Ancak son yayınlanan ADR-0021 (Money), ADR-0022 (locale), ADR-0023 (NMP) ve ADR-0024 (SignalR) gibi kontratlar downstream SPEC'lere tam sızmadı; aynı şekilde modül konsolidasyonu (Donations/Sponsorship → Fundraising) her spec'te tam yansıtılmadı. Bu boşluklar Phase-2 erken milestone'larında **"Compliance Sweep"** başlığı altında tek kampanyayla kapatılabilir.

En büyük sistemik risk Phase-1'de hiç architecture test yazılmamış olması: Tier boundary, Money VO usage, lockey regex, route tenantId guard — hepsi "Phase-2 Agent D work" olarak işaretlenmiş. Bu mekanik enforcement olmadan mevcut temiz mimari sessizce drift edecek.

Öneri sırası:

1. **Hemen:** ADR status fix (0021/0022 → Accepted), ADR-0024 teknik düzeltme, stale reference sweep, interface name disambiguation.
2. **Phase-2 kick-off öncesi:** Architecture test package + Money compliance sweep + Events-NGO architectural claim fix + dunning schedule fix + Notifications secret storage fix.
3. **Phase-2 erken:** Compliance matrix'teki ⚠️/❌ hücrelerini ✅'e çekecek modül-bazlı iyileştirmeler; retention matrix'ler; portal manifest JSON Schema.
4. **Rolling:** Minor doc cleanup items 40–56.

---

*Rapor sonu — 2026-04-22*
