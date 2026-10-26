# ADR-0009: Five projects per module (Api, Application, Domain, Infrastructure, Persistence)

- **Status:** Proposed
- **Date:** 2026-10-26
- **Deciders:** Davit, Claude

## Context

TRK-7 built Identity as one project, `Trekky.Modules.Identity`, holding endpoints, validation, entities, the DbContext and the OpenIddict setup. Folder conventions were the only thing keeping them apart: an endpoint could use the DbContext directly, and input validation sat in the Domain folder. Davit's review of PR #7 asked for a project per layer, so the direction of dependencies is checked, not just agreed. ADR-0001 (one deployable modular monolith; modules talk only through public contracts) still holds; this ADR only decides how a module is split inside.

## Decision

Every module lives in `backend/src/Modules/<Name>/` as five projects, `Trekky.Modules.<Name>.{Api,Application,Domain,Infrastructure,Persistence}`, referencing each other only like this:

| Project | Holds | References |
| --- | --- | --- |
| Domain | Entities, constants and domain rules | nothing (Identity only: `Microsoft.Extensions.Identity.Stores`) |
| Application | Public contracts (DTOs), use-case interfaces, validators (FluentValidation) | Domain |
| Persistence | DbContext, migrations, RLS, queries | Domain, `Trekky.Persistence` |
| Infrastructure | Implementations of Application interfaces, framework setup | Application, Persistence |
| Api | `<Name>Module : IModule`, endpoint mappings | Application, Infrastructure |

`src/Modules/Directory.Build.props` makes a module's five projects and its `Trekky.Modules.<Name>.Tests` friends (`InternalsVisibleTo`), so everything except `<Name>Module` and the contracts stays `internal`. Project references are transitive, so the compiler would still let Api use the DbContext: each module's `ModuleLayeringTests` checks the compiled assemblies' references against the table above. `Trekky.Api` references only `<Name>.Api`. Another module may use only the public contracts in `<Name>.Application`.

## Consequences

- Easier: Domain, Application and Persistence cannot see the layers above them at all, and `ModuleLayeringTests` fails the build for the rest (an endpoint using the DbContext); each layer can be tested on its own.
- Harder: five projects and a longer `/new-module` per module; more `ProjectReference`s; migrations target the Persistence project (`-p src/Modules/<Name>/Trekky.Modules.<Name>.Persistence -o Migrations`).
- Identity exception: `User` derives from `IdentityUser<Guid>`, so Identity's Domain references `Microsoft.Extensions.Identity.Stores` (no EF Core, no ASP.NET Core). Putting `User` in Infrastructure would create a cycle: Persistence's DbContext needs `User`, and Infrastructure needs the DbContext.
- Protocol code that needs framework services (OpenIddict's authorize and token handlers) lives in Infrastructure; Api only maps its routes.
- One test project per module stays (`Trekky.Modules.<Name>.Tests`).

## Alternatives considered

- One project per module with folders (TRK-7 as first built): fewest projects, but the layering is only a convention.
- Four projects, with domain types in Application: fewer projects, but nothing stops entities from depending on application concerns.
- Three projects (Api, Core, Infrastructure with persistence): less ceremony, but persistence and framework setup share one project, and so do domain and use cases.
