# Trekky

A lean task tracker — a simpler alternative to Jira. Planned follow-ons: Trekky Docs, a context layer, and an AI agent that completes agent-ready tasks.

Status: **POC, v0.1 Skeleton** in progress. Plan and decisions: Notion page "Project Management - Task tracker" and the Trekky Planning Baseline doc.

## Repository layout

```
backend/    .NET 10 modular monolith (ASP.NET Core API, EF Core, Postgres)
frontend/   React + TypeScript (Vite)
docs/adr/   Architecture decision records
.github/    CI workflows
docker-compose.yml   Local infrastructure: Postgres, MinIO, Mailpit
```

## Prerequisites

- .NET SDK 10 (`dotnet --list-sdks`)
- Node.js 22 LTS
- Docker Desktop

## Getting started

```bash
# 1. Infrastructure
cp .env.example .env
docker compose up -d

# 2. Backend  (http://localhost:5080, health: /api/v1/health, OpenAPI: /openapi/v1.json)
cd backend
dotnet build
dotnet test
dotnet run --project src/Trekky.Api

# 3. Frontend (http://localhost:5173, proxies /api to the backend)
cd frontend
npm install
npm run dev
```

Local services: Postgres `localhost:5432`, MinIO console `http://localhost:9001`, Mailpit inbox `http://localhost:8025`.

## Conventions

- **Branches:** short-lived feature branches named `TRK-42-short-description`, squash-merged into `dev`. Long-lived `dev` → `staging` → `main`, one per environment, promoted by merge.
- **Commits:** a descriptive message saying what changed and why, prefixed with the item key, e.g. `TRK-42: Add drag-and-drop between board columns`.
- **Reviews:** every PR is reviewed; CI (build, tests, lint) must be green to merge.
- **Tests:** unit tests for core and complex business logic; Playwright e2e for key user flows.
- **Decisions:** one ADR per decision in `docs/adr`, never edited after it is Agreed.

## License

Not chosen yet (ADR-006). Keep the repository private until it is.
