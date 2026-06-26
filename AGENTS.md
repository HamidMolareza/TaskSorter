# AGENTS.md - TaskSorter

These instructions apply to this repository. Follow the global user instructions first, then these project-specific notes.

## Project Shape

- TaskSorter is a Dockerized .NET 10 backend plus React 19 frontend for ranking GitHub issues and pull requests.
- Backend projects live under `src/TaskSorter.Backend`, `src/TaskSorter.Core`, and `src/TaskSorter.Tests`.
- Frontend code lives under `src/TaskSorter.Frontend` and uses MUI, React, TypeScript, and Vite.
- PostgreSQL-backed data includes profiles, repository rows, repository tiers, profile labels, GitHub cache entries, quota snapshots, and encrypted GitHub tokens.

## Development Rules

- Keep code, comments, commits, and documentation in English.
- Keep GitHub as a read-only task source. Do not add issue mutation behavior unless explicitly requested.
- Do not log, print, commit, or expose GitHub tokens, encrypted token material, Data Protection keys, or `.env` files.
- Avoid live GitHub smoke tests unless they are necessary; they consume quota. Prefer fake-client backend tests and mocked frontend stream tests.
- When changing public behavior, update `README.md`, `docs/API.md`, or `docs/FAQ.md` as appropriate.

## Validation

Use focused project commands from the repo root:

```bash
dotnet test src/TaskSorter.Tests/TaskSorter.Tests.csproj -p:NuGetAudit=false
npm run test --prefix src/TaskSorter.Frontend
npm run build --prefix src/TaskSorter.Frontend
```

For Docker validation:

```bash
docker compose up -d --build
```

Expected local service URLs:

- Frontend: `http://127.0.0.1:5173`
- Backend health: `http://127.0.0.1:5111/health`

## Docker Notes

- `docker-compose.yml` uses `network_mode: host`.
- The backend connects to PostgreSQL through `127.0.0.1:5432`; Docker DNS checks for `backend -> postgres` may be misleading.
- Data is intended to persist in Docker volumes, including PostgreSQL data and ASP.NET Core Data Protection keys.

## CI Notes

- PR coverage targets `src/TaskSorter.Tests/TaskSorter.Tests.csproj`.
- Coverage excludes EF migrations and generated `obj` sources from the coverage denominator.
- Use `-p:NuGetAudit=false` for .NET restore/test commands to avoid external vulnerability-feed failures on restricted networks.
- If local generated pre-commit hooks fail because Python `pre_commit` is missing, run the relevant project checks manually before bypassing hooks for a commit or push.

## Frontend UX Notes

- Keep the app as a tool-first dashboard, not a landing page.
- Use existing MUI patterns and lucide icons.
- Ranked Queue should stay dense and scan-first; preserve backend rank order rather than adding client-side sorting that hides ranking.
- Labels and repositories are ordered lists with drag and move controls. Avoid reintroducing manual numeric order fields for labels.
