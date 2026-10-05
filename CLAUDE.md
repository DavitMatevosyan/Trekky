# Trekky

A lean task tracker — a simpler alternative to Jira — built by Davit with Claude, about 1 hour a week.
Later products build on it: Trekky Docs → a context layer → an AI agent that completes agent-ready tasks.
So every entity must be machine-readable from day one: stable keys, structured fields, full history.

- Plan, versions, contracts and the task list (TRK-1…): Notion page "Project Management - Task tracker".
- Current stage: **POC, v0.1 Skeleton**. Build only what the current version needs (see "Scope").

## Repo layout

```
backend/                 .NET 10 modular monolith
  Trekky.slnx
  src/Trekky.Api/        Host: wiring, /api/v1 group, OpenAPI, health
  src/Trekky.SharedKernel/   IModule contract and cross-cutting primitives only
  src/Modules/Trekky.Modules.<Name>/   One project per module (added per TRK)
  tests/                 xUnit; integration tests use WebApplicationFactory<Program>
frontend/                React 19 + TypeScript strict + Vite; Vitest + Testing Library
docs/adr/                ADRs (template: 0000-template.md)
docker-compose.yml       Postgres 17, MinIO, Mailpit (local only)
```

## Commands

```bash
docker compose up -d                       # infra (copy .env.example to .env first)

cd backend
dotnet build                               # warnings are errors
dotnet test
dotnet run --project src/Trekky.Api        # http://localhost:5080, /api/v1/health

cd frontend
npm install
npm run lint && npm test && npm run build
npm run dev                                # http://localhost:5173, proxies /api → 5080
```

## How we work (non-negotiable)

- **One TRK at a time, in order.** Every change belongs to an existing TRK key from the Notion task list. Never invent a new key or jump ahead; if work is needed that has no TRK, ask Davit first.
- **Buildable history.** Every TRK is verified locally (build, tests, lint, app starts) before it is pushed. If one TRK alone would leave the app not building or running, combine it with the TRKs it needs into one commit. A fix for a TRK goes under that same TRK key.
- **Branches:** `TRK-42-short-description` off `dev`, squash-merged into `dev`. `dev` → `staging` → `main` are promoted by merge only.
- **Commits:** `TRK-42: <descriptive message>` — say what changed and why, with a body when it is not obvious.
- **Never push, force-push, or rewrite pushed history.** Davit verifies and pushes. Do not amend or squash commits that exist on the remote.
- **Never claim something is verified unless you ran it.** If you could not run the build or tests, say so plainly.
- **Reviews:** every change is reviewed by Davit and Claude (use the `code-reviewer` agent) before it is pushed.
- **Decisions:** anything that sets a rule goes in an ADR (`/adr` skill). ADRs are never edited after Agreed; supersede them instead.

## Architecture rules

- **Modular monolith.** Each module implements `IModule` (SharedKernel), owns its own Postgres schema and DbContext, and maps endpoints under `/api/v1`. Modules talk through public contracts or events — never through another module's internals or cross-schema joins.
- **Tenancy.** Every tenant-owned table has `org_id`; Postgres RLS policies enforce it; the request sets `app.org_id` per connection. Never rely on `WHERE org_id = …` alone.
- **Activity log.** Every state change writes an `activity_event` through the outbox in the same transaction. This stream later feeds notifications, real-time, webhooks and AI context — do not skip it.
- **IDs.** UUIDv7 internally (`Guid.CreateVersion7()`); human keys like `TRK-42` externally.
- **Work items.** One `work_item` table: `type` (Epic, Story, Task, SubTask, Bug, TechDebt) + `parent_id`; reserved `custom` JSONB column.
- **API.** OpenAPI-first REST under `/api/v1`; RFC 9457 problem details; cursor pagination; ETag/If-Match for edits; `TypedResults`.
- **Auth.** ASP.NET Core Identity + OpenIddict in-app (no external IdP); JWT with user, org and role claims.
- **Storage / mail.** MinIO via the S3 API; SMTP (Mailpit locally).

## Code conventions

- **C#:** file-scoped namespaces, nullable on, `sealed` by default, records for DTOs, async all the way, no static mutable state. Package versions live only in `Directory.Packages.props`; check for known vulnerabilities before adding or bumping one (warnings are errors, so NU190x audit warnings fail the build).
- **TypeScript:** strict, no `any`, named exports, components in PascalCase files. User-facing strings must be easy to move to i18n; UI must meet WCAG AA.
- **Tests:** unit tests for core and complex business logic (no coverage target); integration tests for endpoints; Playwright e2e for key user flows once they exist. Test names describe behaviour.

## Scope

| Version | Contents |
| --- | --- |
| v0.1 Skeleton (current) | Login (Identity + OpenIddict), org at sign-up, `org_id` + RLS, deploy to the Oracle VM, nightly backup |
| v0.2 Work items | Projects with key, work items with parent/child, fixed workflow, list, detail, activity log |
| v1.0 Dogfood | Board with drag and drop, comments, assignee |

Not before their version: sprints, notifications, real-time, custom workflows/fields, MCP server, SSO, billing.

## Agents and skills

| Name | Kind | Use it to |
| --- | --- | --- |
| `code-reviewer` | agent | Review the current diff against these rules before Davit pushes |
| `test-writer` | agent | Write unit, integration or e2e tests for a change |
| `/trk` | skill | Start, implement and finish a TRK end to end |
| `/verify` | skill | Run the full local verification before a commit is pushed |
| `/new-module` | skill | Add a backend module that follows the module rules |
| `/adr` | skill | Write an ADR from the template |
