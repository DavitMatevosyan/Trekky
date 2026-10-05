---
name: verify
description: Run Trekky's full local verification — backend build and tests, frontend lint, tests and build, and an API health check — before a commit is pushed. Use before every commit or when Davit asks "is it green?".
---

# Verify the repo

Run every step from the repo root, in order, and stop to fix the first failure.

```bash
# Backend
cd backend
dotnet restore
dotnet build --no-restore          # warnings are errors
dotnet test --no-build
cd ..

# Frontend
cd frontend
npm install                        # npm ci once package-lock.json is committed
npm run lint
npm test
npm run build
cd ..

# Runtime smoke test
docker compose up -d
dotnet run --project backend/src/Trekky.Api &   # wait until it listens on :5080
curl -fsS http://localhost:5080/api/v1/health   # expect {"status":"ok"}
# stop the API afterwards
```

## Report

A short table: step, result (pass / fail / not run), and the first error line for any failure.

- NU190x errors mean a vulnerable package: bump to a patched version in `Directory.Packages.props`; never suppress the audit.
- If a step cannot run in this environment (no SDK, no network to NuGet/npm, no Docker), mark it **not run** and say why. Never report a step as passing that you did not see pass.
- Only call the change "verified" when every step passed.
