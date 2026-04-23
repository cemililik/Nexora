---
name: create-backend-module
description: Scaffold a new Nexora backend module (Nexora.Modules.X) following the Modular Monolith + Clean Architecture layout defined in CLAUDE.md and docs/architecture/MODULE_SYSTEM.md. Use when the user asks to "add a new module", "create module X", or "scaffold module".
---

# Create Backend Module

Creates a new `src/Modules/Nexora.Modules.{Name}/` project with Clean Architecture layers and an `IModule` implementation.

## Preconditions
- Read `docs/architecture/MODULE_SYSTEM.md` and `docs/standards/CODING_STANDARDS.md` first.
- Confirm module name with user (PascalCase, singular-ish: `CRM`, `Donations`, `Events`).

## Layout to produce
```
src/Modules/Nexora.Modules.{Name}/
├── Nexora.Modules.{Name}.csproj      # TargetFramework net10.0, references SharedKernel + Infrastructure
├── {Name}Module.cs                    # : IModule — Name, Version, Dependencies, Register/Configure/ConfigureJobs
├── Domain/                            # Entities (AuditableEntity<TId>), ValueObjects, strongly-typed IDs, DomainEvents, repository interfaces
├── Application/                       # Commands, Queries (MediatR), Validators (FluentValidation), DTOs, Mapster profiles
├── Infrastructure/                    # EF DbContext (own context, schema-per-tenant), Repositories, Migrations
└── Api/                               # Minimal API endpoints under /api/v{version}/{module}/{resource}
```

## Mandatory rules
- Tables prefixed `{module}_{table}` in tenant schema.
- Own `DbContext` — never share tables with another module.
- No project reference to other `Nexora.Modules.*`. Cross-module via MediatR notifications, integration events (Dapr pub/sub), or SharedKernel interfaces.
- Strongly-typed IDs (never raw Guid) — put the ID record in `Domain/Ids/`.
- File-scoped namespaces, primary constructors for DI, sealed classes by default.
- Register recurring jobs in `IModule.ConfigureJobs()` using expression pattern.
- Register the module in `Nexora.Host` startup wiring.
- Add to `Nexora.sln` and add architecture tests under `tests/` enforcing boundary rules.

## Checklist to present after scaffolding
- [ ] `.csproj` added to solution
- [ ] Module registered in `Nexora.Host`
- [ ] Initial EF migration created (`dotnet ef migrations add Init_{Name}`)
- [ ] Architecture tests added for module boundaries
- [ ] Module spec doc stub in `docs/modules/{name}/` (use write-module-spec skill)
- [ ] Localization keys planned with `lockey_{module}_*` prefix
