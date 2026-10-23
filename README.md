# Trekky

A lean task tracker — a simpler alternative to Jira. Planned follow-ons: Trekky Docs, a context layer, and an AI agent that completes agent-ready tasks.

Status: **POC, v0.1 Skeleton** in progress. Plan and decisions: Notion page "Project Management - Task tracker" and the Trekky Planning Baseline doc.

## Repository layout

```
backend/    .NET 10 modular monolith (ASP.NET Core API, EF Core, Postgres)
frontend/   React + TypeScript (Vite)
docs/adr/   Architecture decision records
.github/    CI workflows
docker-compose.yml   Local infrastructure: Postgres, Mailpit
docker/postgres/init/   Database roles created on first start
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
dotnet test        # needs Docker running: tests start Postgres with Testcontainers
dotnet run --project src/Trekky.Api

# 3. Frontend (http://localhost:5173, proxies /api to the backend)
cd frontend
npm ci        # first time without a lockfile: npm install
npm run dev
```

Local services: Postgres `localhost:5432`, Mailpit inbox `http://localhost:8025`. There is no object storage yet; see ADR-0006.

Authentication (ADR-0003): `POST /api/v1/auth/sign-up` or `/login` sets the session cookie; the web client `trekky-web` then uses authorization code + PKCE at `/connect/authorize` and `/connect/token`. Development uses OpenIddict's development certificates; other environments need `Identity:SigningCertificate` and `Identity:EncryptionCertificate` (PKCS#12 path and password).

New EF Core migration for a module (tool version pinned in `backend/.config/dotnet-tools.json`):

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Name> -p src/Modules/Trekky.Modules.<Module> -s src/Trekky.Api -o Persistence/Migrations
```

Database roles: on first start Postgres creates `trekky_owner` (runs migrations, owns tables) and `trekky_app` (used by the API; row-level security always applies) from `docker/postgres/init/01-roles.sql`. In Development the API applies pending migrations on startup. If your Postgres volume was created before these roles existed, recreate it once: `docker compose down -v && docker compose up -d` (this deletes local data).

## Conventions

- **Branches:** short-lived feature branches named `TRK-42-short-description`, squash-merged into `dev`. Long-lived `dev` → `staging` → `main`, one per environment, promoted by merge.
- **Commits:** a descriptive message saying what changed and why, prefixed with the item key, e.g. `TRK-42: Add drag-and-drop between board columns`.
- **Buildable history:** every TRK is verified locally (build, tests, app runs) before it is pushed. If one TRK alone would leave the app not building or running, combine it with the TRKs it needs into one commit.
- **Reviews:** every PR is reviewed; CI (build, tests, lint) must be green to merge.
- **Tests:** unit tests for core and complex business logic; Playwright e2e for key user flows.
- **Decisions:** one ADR per decision in `docs/adr`, never edited after it is Agreed.

## License

Not chosen yet (ADR-0007). Keep the repository private until it is.
