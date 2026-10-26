---
name: new-module
description: Add a new backend module to the Trekky modular monolith (e.g. Identity, Projects, WorkItems) with its five layer projects (ADR-0009), schema, DbContext, endpoints and tests. Use when a TRK introduces a new module.
---

# Add a backend module

Replace `<Name>` with the module name in PascalCase (e.g. `Projects`) and `<schema>` with its lowercase schema (e.g. `projects`). `backend/src/Modules/Identity/` is the reference module.

## Projects (ADR-0009)

```bash
cd backend
M=src/Modules/<Name>/Trekky.Modules.<Name>
for layer in Api Application Domain Infrastructure Persistence; do
  dotnet new classlib -n Trekky.Modules.<Name>.$layer -o $M.$layer
  dotnet sln Trekky.slnx add $M.$layer
done
dotnet new xunit -n Trekky.Modules.<Name>.Tests -o tests/Trekky.Modules.<Name>.Tests
dotnet sln Trekky.slnx add tests/Trekky.Modules.<Name>.Tests

dotnet add $M.Application    reference $M.Domain
dotnet add $M.Persistence    reference $M.Domain src/Trekky.Persistence
dotnet add $M.Infrastructure reference $M.Application $M.Persistence src/Trekky.SharedKernel
dotnet add $M.Api            reference $M.Application $M.Infrastructure src/Trekky.SharedKernel
dotnet add src/Trekky.Api    reference $M.Api
dotnet add tests/Trekky.Modules.<Name>.Tests reference $M.Api
```

Remove `TargetFramework` and package versions that `dotnet new` writes; they come from `Directory.Build.props` and `Directory.Packages.props`. Delete the templates' `Class1.cs` / `UnitTest1.cs`. Api and Infrastructure need `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; Application gets `<PackageReference Include="FluentValidation" />`. `src/Modules/Directory.Build.props` already makes the five projects and the test project friends; do not add `InternalsVisibleTo` by hand.

## What goes where

| Project | Holds |
| --- | --- |
| `Domain` | Entities, constants, domain rules. No EF Core, no ASP.NET Core. |
| `Application` | `Contracts/` (public request/response records), use-case interfaces (`I<Thing>Service`), FluentValidation validators, outcome enums |
| `Persistence` | `<Name>DbContext`, `Migrations/`, RLS helpers, read queries, `Add<Name>Persistence()` |
| `Infrastructure` | Implementations of the Application interfaces, framework setup, `Add<Name>Infrastructure()` (calls `Add<Name>Persistence()`) |
| `Api` | `<Name>Module : IModule` (registers validators and infrastructure), `Endpoints/` mappings |

Only `<Name>Module` and `Application/Contracts` are public; everything else is `internal sealed`. Copy `ModuleLayeringTests` from the Identity tests: project references are transitive, so only that test stops Api from using the DbContext. One type per file. Endpoints validate with `IValidator<T>` and return `TypedResults.ValidationProblem(result.ToDictionary())`, with error keys set by `OverridePropertyName("<jsonName>")`.

## Rules

- **Schema:** `<Name>DbContext` declares `public const string Schema = "<schema>";` and calls `modelBuilder.HasDefaultSchema(Schema)`. Migrations live in Persistence: `dotnet ef migrations add <Migration> -p src/Modules/<Name>/Trekky.Modules.<Name>.Persistence -s src/Trekky.Api -o Migrations`.
- **Tenancy:** every tenant-owned table has `org_id` (uuid, not null, indexed). Register the DbContext with `AddModuleDbContext<T>(configuration, <Name>DbContext.Schema)` and add the policy in the migration with `migrationBuilder.EnableTenantRowLevelSecurity(schema, table)`; never hand-write it.
- **IDs:** `Guid.CreateVersion7()`.
- **Events:** state changes write an `activity_event` through the outbox in the same `SaveChanges` transaction.
- **Endpoints:** `MapEndpoints` maps under the `/api/v1` group it is given, e.g. `group.MapGroup("/<schema>")`; return `TypedResults` and problem details. Map lambdas, not method groups from another project (the route-handler analyzer crashes on those).
- **Registration:** add `new <Name>Module()` to `builder.Services.AddModules(...)` in `Program.cs`.
- **Postman:** add the module's requests as a folder in `postman/Trekky.postman_collection.json` (ADR-0008).

## Done when

Builds with zero warnings, module tests and an integration test for one endpoint pass (`/verify`), and the `code-reviewer` agent has no blocking findings.
