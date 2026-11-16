# ADR-0001: Backend stack — .NET modular monolith

- **Status:** Agreed
- **Date:** 2026-10-06 (agreed in planning, 2026-10-05)
- **Deciders:** Davit, Claude

## Context

Trekky is built by one engineer with Claude, about an hour a week, with no paid services during the POC. Davit's main stack is .NET. The product must later grow into Trekky Docs, a context layer and an AI agent, and must be shippable as SaaS, self-hosted and open core from one codebase. Operational overhead has to stay close to zero.

## Decision

The backend is a single deployable **.NET 10 modular monolith** (ASP.NET Core minimal APIs, EF Core code-first with Npgsql, PostgreSQL 17); the frontend is **React + TypeScript on Vite**. A service in another language is allowed only when an ADR shows that it meets the rule below.

**When a non-.NET service is allowed** — all three must hold:

1. The capability has no reasonable .NET option (for example, a Python-only ML or embedding library for the context layer).
2. It can sit behind a narrow contract (HTTP API or queue) and owns no data another module reads directly.
3. Its extra build, deploy and monitoring cost is accepted in that ADR.

## Consequences

- One process, one deploy, one database: cheap to run on the POC VM and easy to self-host.
- Module boundaries are enforced by convention and project references, not the network. Each module owns its Postgres schema and DbContext, implements `IModule`, and talks to other modules only through public contracts or events (see CLAUDE.md).
- A module can be split out later if one ever needs independent scaling; the boundaries make that a refactor, not a rewrite.
- The whole team must keep module internals `internal`; reviews check this.

## Alternatives considered

- **Microservices** — network, deploy and observability cost far beyond what an hour a week can carry.
- **Node.js / TypeScript backend** — one language end to end, but away from Davit's strongest stack and the existing scaffold.
- **Layered monolith without modules** — fastest start, but tangles Docs and the agent into one model later.
