# Code Review: Domain Hardening & Tenant State Machine

**Date**: 2026-03-21
**Reviewer**: AI Agent (Claude Sonnet 4.6)
**Commit**: `3bb5055` — `feat: enhance document and tenant management with validation and logging improvements`
**Branch**: `development`
**Scope**: Tüm değişen dosyalar — 18 dosya, +182 / -37 satır
**Standards Referenced**: `CODING_STANDARDS.md`, `OBSERVABILITY_STANDARDS.md`, `LOCALIZATION_STANDARDS.md`, `INFRASTRUCTURE_STANDARDS.md`, `DOCUMENTATION_STANDARDS.md`, `CLAUDE.md`

---

## Review Summary

| Kategori | Bulgu | Critical | Major | Minor | Info |
|----------|-------|----------|-------|-------|------|
| Domain & Business Logic | 5 | 1 | 2 | 1 | 1 |
| Localization Standards | 1 | 0 | 1 | 0 | 0 |
| Test Coverage | 4 | 0 | 3 | 1 | 0 |
| Infrastructure & Deployment | 1 | 1 | 0 | 0 | 0 |
| Observability | 2 | 0 | 0 | 1 | 1 |
| SharedKernel / API Contracts | 1 | 0 | 0 | 1 | 0 |
| **Toplam** | **14** | **2** | **6** | **3** | **2** |

**Verdict**: `CHANGES_REQUESTED` — 2 critical ve 6 major bulgu merge öncesi düzeltilmelidir.

---

## Severity Legend

- **CRITICAL**: Güvenlik açığı, veri bütünlüğü riski, deployment blocker veya standard ihlali — merge blocker
- **MAJOR**: Mimari sorun, business logic hatası veya standarttan belirgin sapma — merge öncesi düzeltilmeli
- **MINOR**: Kod kalitesi sorunu veya minor standart sapması — düzeltilmeli ama merge blocker değil
- **INFO**: Öneri, gözlem veya best practice notu — opsiyonel

---

## Değişen Dosyalar

| # | Dosya | Değişim | Amaç |
|---|-------|---------|------|
| 1 | `Documents/Application/Commands/ArchiveDocumentCommand.cs` | +6 | Already-archived guard eklendi |
| 2 | `Documents/Application/Commands/CreateFolderCommand.cs` | +5/-5 | UserId eksik → Guid.Empty yerine Result.Failure |
| 3 | `Documents/Application/Commands/UploadDocumentCommand.cs` | +5/-5 | Aynı UserId fix |
| 4 | `Documents/Infrastructure/IntegrationEvents/DocumentArchivedDomainEventHandler.cs` | +16 | Tenant context yoksa DB fallback |
| 5 | `Documents/Infrastructure/IntegrationEvents/DocumentSignedDomainEventHandler.cs` | +1/-1 | Recipient lookup'a RequestId filtresi eklendi |
| 6 | `Identity/Domain/Entities/Role.cs` | +1 | `IsActive = true` explicit set |
| 7 | `Identity/Domain/Entities/Tenant.cs` | +31 | Durum makinesi guard'ları + SetRealmId validation |
| 8 | `Identity/Infrastructure/Configurations/RoleConfiguration.cs` | +2 | `IsSystemRole`/`IsActive` EF defaults |
| 9 | `Notifications/Infrastructure/IntegrationEvents/NotificationBounced/Delivered/SentDomainEventHandler.cs` | -9 | Duplicate LogDebug kaldırıldı |
| 10 | `SharedKernel/Abstractions/MultiTenancy/TenantContextExtensions.cs` | +2 | `accessor is null` null guard |
| 11 | `tests/Documents.Tests/Application/CreateFolderTests.cs` | +4/-4 | `_userId` tenant context'e eklendi |
| 12 | `tests/Documents.Tests/Application/DocumentQueryTests.cs` | +0/-0 | `CreateTenantAccessor` instance method'a çevrildi |
| 13 | `tests/Documents.Tests/Application/GrantDocumentAccessTests.cs` | +0/-0 | Aynı |
| 14 | `tests/Documents.Tests/Application/UploadDocumentTests.cs` | +0/-0 | Aynı |
| 15 | `tests/Identity.Tests/Domain/TenantTests.cs` | +62 | 7 yeni test |
| 16 | `docs/roadmap/ROADMAP.md` | +3/-2 | Test sayısı ve compliance notu güncellendi |

---

## 1. Infrastructure & Deployment

### [CR-001] CRITICAL — EF Core Migration Eksik

**Dosya**: `src/Modules/Nexora.Modules.Identity/Infrastructure/Configurations/RoleConfiguration.cs:22-23`
**Kategori**: Deployment Blocker / Infrastructure

```csharp
// Yeni eklenen satırlar:
builder.Property(r => r.IsSystemRole).IsRequired().HasDefaultValue(false);
builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);
```

**Problem**: `HasDefaultValue(false)` ve `HasDefaultValue(true)` konfigürasyonları EF Core model snapshot'ına yazılıyor. Bu değişiklikler:
1. Mevcut `identity_roles` tablosundaki sütunların PostgreSQL `DEFAULT` constraint'ini etkiliyor
2. EF Core migration oluşturulmadan production'a deploy edildiğinde schema drift oluşuyor
3. `IdentityModuleMigration.cs` içinde `dbContext.Roles.AnyAsync(r => r.IsSystemRole)` çağrısı varken, DB'de `DEFAULT FALSE` yoksa eski kayıtlar için beklenmedik davranış oluşabilir

Projede migration dosyası bulunamadı (`find . -path "*/Migrations/*.cs"` boş döndü) — eğer migration sistemi kullanılmıyorsa ve schema tamamen seed/init ile yönetiliyorsa bu MAJOR'a düşer. Ama CODING_STANDARDS'da "Migrations are additive-only in production" yazıyor, bu da migration mekanizmasının var olduğunu gösteriyor.

**Fix**:
```bash
dotnet ef migrations add AddRoleIsSystemRoleAndIsActiveDefaults \
  --project src/Modules/Nexora.Modules.Identity \
  --startup-project src/Nexora.Host
```

---

## 2. Domain & Business Logic

### [CR-002] CRITICAL — `DocumentSignedDomainEventHandler` Düzeltmesi: Scope Sızıntısı Riski (Önceki State)

**Dosya**: `src/Modules/Nexora.Modules.Documents/Infrastructure/IntegrationEvents/DocumentSignedDomainEventHandler.cs:25`
**Kategori**: Güvenlik / Doğruluk

```csharp
// ÖNCE (güvenlik açığı):
var recipient = await dbContext.SignatureRecipients
    .FirstOrDefaultAsync(r => r.Id == notification.RecipientId, cancellationToken);

// SONRA (düzeltilmiş):
var recipient = await dbContext.SignatureRecipients
    .FirstOrDefaultAsync(r => r.Id == notification.RecipientId && r.RequestId == notification.RequestId, cancellationToken);
```

**Değerlendirme**: Bu düzeltme **kritik bir güvenlik açığını kapattı**. Önceki kod, `RecipientId` için farklı bir `RequestId`'ye ait recipient bulabilirdi — cross-request veri sızıntısı. Düzeltme doğru ve gerekli.

**Ek endişe**: Commit mesajında veya kod yorumunda bu düzeltmenin güvenlik implikasyonu belirtilmemiş. `DocumentSignedDomainEventHandler` test kapsamı mevcut mu?

```bash
# Kontrol:
grep -rn "DocumentSignedDomainEventHandler\|SignedDomainEvent" tests/ --include="*.cs"
```

Eğer test yoksa MAJOR seviyesinde test eksikliği var.

---

### [CR-003] MAJOR — `Tenant.Activate()` ve `Suspend()`: Aynı `lockey_` Anahtarı

**Dosya**: `src/Modules/Nexora.Modules.Identity/Domain/Entities/Tenant.cs:61,76`
**Kategori**: Localization Standards / UX

```csharp
// Activate() içinde:
if (Status is not (TenantStatus.Trial or TenantStatus.Suspended))
    throw new DomainException("lockey_identity_error_invalid_tenant_transition");

// Suspend() içinde:
if (Status is not TenantStatus.Active)
    throw new DomainException("lockey_identity_error_invalid_tenant_transition");
```

**Problem**: Her iki hata aynı `lockey_` anahtarını kullanıyor. Localization Standards'a göre:
> "lockey_{scope}_{context}_{descriptor}"

`_invalid_tenant_transition` tüm geçersiz geçişler için aynı hata mesajını gösterir. Frontend hiçbir zaman "Terminated tenant aktive edilemez" ile "Suspended tenant doğrudan Terminate edilemez" arasındaki farkı ayırt edemez.

**Durum Makinesi Haritası (şu anki implementation)**:

```
Trial     → Active ✓, Terminated ✓, Suspended ✗ (exception)
Active    → Suspended ✓, Terminated ✓, (Active=no-op)
Suspended → Active ✓, Terminated ✓
Terminated → (hepsi exception — fakat Terminate no-op)
```

**Fix**:
```csharp
// Activate() için:
throw new DomainException("lockey_identity_error_tenant_cannot_activate_from_terminated");

// Suspend() için:
throw new DomainException("lockey_identity_error_tenant_suspend_requires_active_status");
```

Ya da tek key kullanmak isteniyorsa, parametric format:
```csharp
throw new DomainException($"lockey_identity_error_invalid_tenant_transition"); // asgari
// veya
throw new DomainException("lockey_identity_error_tenant_activation_not_allowed");
throw new DomainException("lockey_identity_error_tenant_suspension_not_allowed");
```

---

### [CR-004] MAJOR — `ArchiveDocumentCommand` Handler Guard: Domain Invariant Duplikasyonu

**Dosya**: `src/Modules/Nexora.Modules.Documents/Application/Commands/ArchiveDocumentCommand.cs:47-53`
**Dosya**: `src/Modules/Nexora.Modules.Documents/Domain/Entities/Document.cs:159-160`
**Kategori**: DDD / Coding Standards

```csharp
// Handler (yeni eklenen):
if (document.Status is DocumentStatus.Archived)
{
    logger.LogWarning("Document {DocumentId} is already archived...", request.DocumentId);
    return Result.Failure(LocalizedMessage.Of("lockey_documents_error_already_archived"));
}

document.Archive(); // domain entity aynı koşulda DomainException fırlatıyor
```

```csharp
// Domain entity (mevcut):
public void Archive()
{
    if (Status is DocumentStatus.Archived)
        throw new DomainException("lockey_documents_error_already_archived"); // aynı koşul!
}
```

**Problem**: CODING_STANDARDS şunu söylüyor:
> "Rich domain model (behavior on entities, not anemic)"
> "DomainException ONLY from domain entities — handlers use `Result.Failure()` instead"

Handler'ın domain state'i kendi pre-check ile kontrol etmesi doğru bir pattern. Ancak şu an aynı business invariant **iki farklı kod yolunda** implement edilmiş:
1. Handler → `Result.Failure` (expected error olarak — doğru)
2. Entity → `DomainException` (domain guard olarak — de facto güvenlik ağı)

Bu durum "belt and suspenders" yaklaşımı olarak savunulabilir. **Ancak CODING_STANDARDS açıkça domain-only DomainException diyor** — handler'ın entity guard'ını pre-empt etmesi, entity'nin `DomainException` fırlatmasını gizler. `GlobalExceptionHandler` bu exception'ı yakalayıp 422 döndürür — ama handler zaten `Result.Failure` döndüğü için entity guard hiçbir zaman tetiklenmeyecek.

**Sonuç**: Bu "correct by accident" durumu. Domain entity guard erişilebilir değil (yalnızca handler'dan çağrılıyor) ama entity güvenlik ağı olarak iyi.

**Acil Fix Gerekmez** — MINOR'a taşınabilir. Fakat tutarlılık için tüm similar handler'lara (Contacts `ArchiveContact`, vb.) bakılmalı. Contacts'ta aynı pattern var mı?

---

### [CR-005] MINOR — `DocumentArchivedDomainEventHandler` DB Fallback: Gereksiz Full Entity Load

**Dosya**: `src/Modules/Nexora.Modules.Documents/Infrastructure/IntegrationEvents/DocumentArchivedDomainEventHandler.cs:29-37`
**Kategori**: Performance

```csharp
// Mevcut:
var document = await dbContext.Documents
    .AsNoTracking()
    .FirstOrDefaultAsync(d => d.Id == notification.DocumentId, cancellationToken);

tenantId = document.TenantId.ToString();
```

**Problem**: Fallback yalnızca `TenantId` için DB'ye gidiyor ama tüm `Document` entity'sini yüklüyor (tüm sütunlar: status, storageKey, fileSize, description, tags, vb.). Bu gereksiz I/O.

**Fix**:
```csharp
var tenantId = await dbContext.Documents
    .AsNoTracking()
    .Where(d => d.Id == notification.DocumentId)
    .Select(d => d.TenantId.ToString())
    .FirstOrDefaultAsync(cancellationToken);

if (tenantId is null)
{
    logger.LogWarning("Document {DocumentId} not found for DocumentArchivedEvent, skipping",
        notification.DocumentId.Value);
    return;
}
```

Bu hem daha verimli hem de null check daha net.

---

### [CR-006] INFO — `Terminate()`: Trial → Terminated Geçişi Dokümante Edilmemiş

**Dosya**: `src/Modules/Nexora.Modules.Identity/Domain/Entities/Tenant.cs:83-87`
**Kategori**: Business Logic / Documentation

```csharp
/// <summary>
/// Terminates the tenant. No-op if already terminated.
/// Any non-terminated tenant can be terminated.
/// </summary>
public void Terminate()
{
    if (Status == TenantStatus.Terminated) return;
    Status = TenantStatus.Terminated;
    // ...
}
```

**Gözlem**: `Trial → Terminated` geçişine izin veriliyor — yani bir tenant trial başlamadan sonlandırılabilir. Bu iş gereksinimiyle uyumlu mu? Business case (ödeme yapmadan iptal, deneme sürümü reddi) olduğu için makul görünüyor. Fakat `docs/modules/identity/SPEC.md` içindeki durum diyagramında bu geçiş var mı?

**Aksiyon**: SPEC.md'deki state diagram güncellenmeli; Trial→Terminated geçişi açıkça gösterilmeli.

---

## 3. Test Coverage

### [CR-007] MAJOR — `ArchiveDocumentCommand` Handler Guard için Test Yok

**Dosya**: Yok — olması gereken: `tests/Nexora.Modules.Documents.Tests/Application/ArchiveDocumentTests.cs`
**Kategori**: Test Coverage

Yeni eklenen handler pre-check:
```csharp
if (document.Status is DocumentStatus.Archived)
    return Result.Failure(LocalizedMessage.Of("lockey_documents_error_already_archived"));
```

Bu kod yolu için hiçbir Application-level test yok. `DocumentTests.cs`'deki domain testi (`Archive_WhenAlreadyArchived_ShouldThrow`) yalnızca entity'nin `DomainException` fırlattığını test ediyor — handler'ın `Result.Failure` döndürdüğünü değil.

**Eksik Test**:
```csharp
[Fact]
public async Task Handle_WhenDocumentAlreadyArchived_ShouldReturnFailure()
{
    // Arrange
    var document = CreateArchivedDocument(); // Status = Archived
    _dbContext.Documents.Add(document);
    await _dbContext.SaveChangesAsync();

    var command = new ArchiveDocumentCommand(document.Id.Value, _tenantId, _orgId);
    var handler = CreateHandler();

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error!.Message.Key.Should().Be("lockey_documents_error_already_archived");
}
```

---

### [CR-008] MAJOR — `CreateFolderCommand` ve `UploadDocumentCommand`: Missing UserId Senaryosu Test Edilmemiş

**Kategori**: Test Coverage

`CreateFolderCommand.cs` ve `UploadDocumentCommand.cs`'de:
```csharp
if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var parsedUid))
    return Result<FolderDto>.Failure(LocalizedMessage.Of("lockey_documents_error_missing_user_context"));
```

Bu değişiklik Guid.Empty kullanan bir "silent corruption" bug'ını düzeltiyor — ama yeni failure path'i için test yok. `CreateFolderTests` ve `UploadDocumentTests`'te `CreateTenantAccessor` artık `_userId` sağlıyor, bu da şu anki testlerin yeni failure path'ini hiç test etmediği anlamına geliyor.

**Eksik Testler**:
```csharp
[Fact]
public async Task Handle_WhenUserIdMissingFromContext_ShouldReturnFailure()
{
    // Arrange — UserId olmadan tenant context
    var accessor = new TenantContextAccessor();
    accessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), userId: null);
    var handler = new CreateFolderHandler(_dbContext, accessor, NullLogger<CreateFolderHandler>.Instance);

    // Act
    var result = await handler.Handle(CreateCommand(), CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error!.Message.Key.Should().Be("lockey_documents_error_missing_user_context");
}
```

Hem `CreateFolderTests` hem de `UploadDocumentTests` için bu test gerekiyor.

---

### [CR-009] MAJOR — `Tenant` Durum Makinesi: Happy Path Testleri Eksik

**Dosya**: `tests/Nexora.Modules.Identity.Tests/Domain/TenantTests.cs`
**Kategori**: Test Coverage

Yeni eklenen testler yalnızca exception senaryolarını kapsıyor. Aşağıdaki happy path testler **yok**:

| Geçiş | Test Durumu |
|--------|-------------|
| `Trial → Active` | ❌ YOK (temel kullanım durumu!) |
| `Active → Suspended` | ❌ YOK |
| `Suspended → Terminated` | ❌ YOK |
| `Activate_FromSuspended` | ✅ var |
| `Activate_WhenAlreadyActive_NoOp` | ✅ var |
| `Suspend_FromTrial_ShouldThrow` | ✅ var |
| `Suspend_FromTerminated_ShouldThrow` | ✅ var |
| `Activate_FromTerminated_ShouldThrow` | ✅ var |

**Eksik Testler** (minimum):
```csharp
[Fact]
public void Activate_FromTrial_ShouldChangeStatusAndRaiseDomainEvent()
{
    var tenant = Tenant.Create("Test", "test");
    // Status = Trial (initial)

    tenant.Activate();

    tenant.Status.Should().Be(TenantStatus.Active);
    tenant.DomainEvents.Should().ContainSingle()
        .Which.Should().BeOfType<TenantStatusChangedEvent>();
}

[Fact]
public void Suspend_FromActive_ShouldChangeStatusAndRaiseDomainEvent()
{
    var tenant = Tenant.Create("Test", "test");
    tenant.Activate();
    tenant.ClearDomainEvents();

    tenant.Suspend();

    tenant.Status.Should().Be(TenantStatus.Suspended);
    tenant.DomainEvents.Should().ContainSingle()
        .Which.Should().BeOfType<TenantStatusChangedEvent>();
}
```

---

### [CR-010] MINOR — `DocumentSignedDomainEventHandler` için Test Yok

**Dosya**: Yok
**Kategori**: Test Coverage

`DocumentSignedDomainEventHandler.cs`'deki fix (recipient lookup'a RequestId filtresi eklendi) güvenlik açısından kritik bir düzeltme. Bu handler için hiç test yok.

---

## 4. Observability

### [CR-011] MINOR — `TryGetCurrent` Null Guard: Non-Nullable Parametre Üzerinde

**Dosya**: `src/Nexora.SharedKernel/Abstractions/MultiTenancy/TenantContextExtensions.cs:26`
**Kategori**: API Design / C# NRT

```csharp
// Mevcut:
public static ITenantContext? TryGetCurrent(this ITenantContextAccessor accessor)
{
    if (accessor is null) return null; // null guard non-nullable parametre üzerinde
    // ...
}
```

**Problem**: `ITenantContextAccessor accessor` non-nullable olarak tanımlanmış (C# NRT aktifse). Null guard eklemek:
1. `ArgumentNullException` yerine sessizce `null` döner — DI misconfiguration'ı maskeler
2. NRT'yi ihlal eder — compiler `accessor` zaten non-null varsayar, null check warning üretebilir

**Seçenekler**:

```csharp
// Seçenek A — nullable kabul et (doğru API design):
public static ITenantContext? TryGetCurrent(this ITenantContextAccessor? accessor)
{
    if (accessor is null) return null;
    // ...
}

// Seçenek B — null guard kaldır, NRT'ye güven (DI'nın doğru çalıştığı varsayımı):
public static ITenantContext? TryGetCurrent(this ITenantContextAccessor accessor)
{
    // accessor null olamaz — DI bunu garanti eder
    try { return accessor.Current; }
    catch (InvalidOperationException) { return null; }
}
```

`DocumentArchivedDomainEventHandler`'daki kullanıma bakıldığında (`tenantContextAccessor.TryGetCurrent()`), `tenantContextAccessor` primary constructor injection ile geliyor — null olmayacak. Seçenek B daha doğru.

---

### [CR-012] INFO — Kaldırılan Notification Debug Log'ları: Kontekst Kayıpı

**Dosya**: `Notifications/Infrastructure/IntegrationEvents/Notification*DomainEventHandler.cs`
**Kategori**: Observability

```csharp
// Kaldırılan (3 handler'dan):
logger.LogDebug("NotificationBounced context — NotificationId: {NotificationId}, ContactId: {ContactId}",
    notification.NotificationId.Value, notification.ContactId);

logger.LogDebug("NotificationDelivered context — NotificationId: {NotificationId}, RecipientId: {RecipientId}, ContactId: {ContactId}",
    notification.NotificationId.Value, notification.RecipientId.Value, notification.ContactId);

logger.LogDebug("NotificationSent context — NotificationId: {NotificationId}, Channel: {Channel}, RecipientCount: {RecipientCount}",
    notification.NotificationId.Value, notification.Channel, notification.RecipientCount);
```

`PublishAndLogAsync` yalnızca `{EventType}`, `{TenantId}`, `{EventId}` logluyor. Kaldırılan log'lar şu alanları içeriyordu: `ContactId`, `RecipientId`, `Channel`, `RecipientCount` — bunlar integration event body'sindeki alanlar ama log'da artık görünmüyor.

**Geliştirme Önerileri** (zorunlu değil):
1. OpenTelemetry Activity tag'leri olarak bu alanları ekle
2. Veya `PublishAndLogAsync` overload'ını enrichment callback ile genişlet

---

## 5. Onaylanan Düzeltmeler (Pozitif Bulgular)

### ✅ Tenant Durum Makinesi Guard'ları

`Tenant.Activate()` ve `Suspend()` metodlarına eklenen state machine validation doğru implement edilmiş. No-op guard'lar (zaten aktif/suspended ise), state machine guard'lardan ÖNCE geliyor — bu doğru sıralama:

```csharp
public void Activate()
{
    if (Status == TenantStatus.Active) return;       // 1. No-op (erken çıkış)
    if (Status is not (TenantStatus.Trial           // 2. Geçerli kaynak state
        or TenantStatus.Suspended))
        throw new DomainException("...");
    // ...
}
```

### ✅ `SetRealmId` Validation

`string.IsNullOrWhiteSpace` + `.Trim()` kombinasyonu doğru. `realmId.Trim()` Keycloak realm identifier'larındaki leading/trailing whitespace'i temizliyor.

### ✅ `CreateFolder` / `UploadDocument` UserId Fix

`Guid.Empty` ile sessizce devam etmek yerine `Result.Failure` döndürmek doğru. `Guid.Empty` sahte bir ownerId olarak kayıt edilseydi veri bütünlüğü sorunu yaratırdı.

### ✅ `DocumentSignedDomainEventHandler` — Compound Lookup

Recipient lookup'ına `r.RequestId == notification.RequestId` filtresi eklemek doğru — cross-request veri sızıntısı riski giderildi.

### ✅ `Role.Create` — Explicit `IsActive = true`

Field initializer zaten `= true` ayarlıyor olsa da, factory method'da explicit set etmek niyeti netleştiriyor ve field initializer değiştirildiğinde koruma sağlıyor.

### ✅ `DocumentArchivedDomainEventHandler` DB Fallback

Tenant context unavailable olduğunda event'i tamamen drop etmek yerine DB'den TenantId lookup yapılması doğru bir event reliability improvement. Background job execution context'inde HTTP request context'i yoktur — bu fix o senaryoyu ele alıyor.

### ✅ Notification Debug Log Temizliği

`PublishAndLogAsync` zaten event publishing'i logluyor. Duplicate `LogDebug` kaldırılması log noise'u azaltıyor.

---

## Aksiyon Listesi

| # | Bulgu | Öncelik | Dosya |
|---|-------|---------|-------|
| CR-001 | EF Core migration oluştur (`IsSystemRole`, `IsActive` defaults) | **BLOCKER** | `RoleConfiguration.cs` |
| CR-003 | Tenant transition için ayrı `lockey_` anahtarları tanımla | **HIGH** | `Tenant.cs` |
| CR-007 | `ArchiveDocumentHandler` already-archived test yaz | **HIGH** | Yeni test dosyası |
| CR-008 | `CreateFolder` / `UploadDocument` missing-UserId test yaz | **HIGH** | `CreateFolderTests`, `UploadDocumentTests` |
| CR-009 | `Tenant` happy path testleri yaz (Trial→Active, Active→Suspended) | **HIGH** | `TenantTests.cs` |
| CR-005 | `DocumentArchivedDomainEventHandler` → sadece `TenantId` select et | **MEDIUM** | Handler dosyası |
| CR-010 | `DocumentSignedDomainEventHandler` testi yaz | **MEDIUM** | Yeni test dosyası |
| CR-011 | `TryGetCurrent` parametre nullable yap veya null guard kaldır | **LOW** | `TenantContextExtensions.cs` |
| CR-006 | SPEC.md state diagram'a Trial→Terminated geçişi ekle | **LOW** | `docs/modules/identity/SPEC.md` |

---

## Sonuç

Bu commit genel olarak sağlıklı bir "domain hardening" çalışması — Tenant state machine guard'ları, UserId null safety fix'i, DocumentSigned güvenlik düzeltmesi ve Guid.Empty silent corruption bug'ı tüm doğru adımlar. Ana sorunlar test coverage eksikliği ve deployment için gerekli EF migration'ın atlanması.

**Merge Öncesi Zorunlu**:
1. EF Core migration oluşturulması (CR-001)
2. Tenant lockey_ anahtar ayrımı (CR-003)
3. 4 adet eksik test (CR-007, CR-008, CR-009)

**Merge Sonrası Kabul Edilebilir**:
- CR-005, CR-010, CR-011, CR-012
