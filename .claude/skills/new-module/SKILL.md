---
name: new-module
description: Add a new backend module to the Trekky modular monolith (e.g. Identity, Projects, WorkItems) with its own project, schema, DbContext, endpoints and tests. Use when a TRK introduces a new module.
---

# Add a backend module

Replace `<Name>` with the module name in PascalCase (e.g. `Projects`) and `<schema>` with its lowercase schema (e.g. `projects`).

## Projects

```bash
cd backend
dotnet new classlib -n Trekky.Modules.<Name> -o src/Modules/Trekky.Modules.<Name>
dotnet new xunit   -n Trekky.Modules.<Name>.Tests -o tests/Trekky.Modules.<Name>.Tests
dotnet sln Trekky.slnx add src/Modules/Trekky.Modules.<Name> tests/Trekky.Modules.<Name>.Tests
dotnet add src/Trekky.Api reference src/Modules/Trekky.Modules.<Name>
dotnet add src/Modules/Trekky.Modules.<Name> reference src/Trekky.SharedKernel
dotnet add tests/Trekky.Modules.<Name>.Tests reference src/Modules/Trekky.Modules.<Name>
```

Remove `TargetFramework` and package versions that `dotnet new` writes — they come from `Directory.Build.props` and `Directory.Packages.props`. Delete the template's `Class1.cs` / `UnitTest1.cs`.

## Layout

```
Trekky.Modules.<Name>/
  <Name>Module.cs          public sealed class implementing IModule
  Contracts/               public DTOs, events and interfaces other modules may use
  Domain/                  entities and rules (internal)
  Persistence/             <Name>DbContext (internal), configurations, migrations
  Endpoints/               minimal-API endpoint mappings (internal)
```

Only `<Name>Module` and `Contracts/` are public. Everything else is `internal sealed`.

## Rules

- **Schema:** `<Name>DbContext` calls `modelBuilder.HasDefaultSchema("<schema>")`. Migrations live in the module: `dotnet ef migrations add <Migration> -p src/Modules/Trekky.Modules.<Name> -s src/Trekky.Api`.
- **Tenancy:** every tenant-owned table has `org_id` (uuid, not null, indexed) and an RLS policy created in the migration (`ENABLE ROW LEVEL SECURITY`, `FORCE ROW LEVEL SECURITY`, policy on `current_setting('app.org_id')::uuid`).
- **IDs:** `Guid.CreateVersion7()`.
- **Events:** state changes write an `activity_event` through the outbox in the same `SaveChanges` transaction.
- **Endpoints:** `MapEndpoints` maps under the `/api/v1` group it is given, e.g. `group.MapGroup("/<schema>")`; return `TypedResults` and problem details.
- **Registration:** add `new <Name>Module()` to `builder.Services.AddModules(...)` in `Program.cs`.

## Done when

Builds with zero warnings, module tests and an integration test for one endpoint pass (`/verify`), and the `code-reviewer` agent has no blocking findings.
