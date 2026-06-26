# TaskSorter

TaskSorter is a web dashboard for ranking GitHub issues and pull requests across multiple repositories. It keeps GitHub as the read-only task source, stores reusable ranking profiles in PostgreSQL, and helps choose a focused queue for daily project work.

## Built With

- Backend: ASP.NET Core Web API, .NET 10, EF Core, PostgreSQL
- Frontend: React 19, TypeScript, Vite, MUI
- Deployment: Docker Compose with separate backend, frontend, and PostgreSQL services

## Architecture

- `src/TaskSorter.Core`: repository/label parsing, project tiers, scoring, sorting, and GitHub task fetching.
- `src/TaskSorter.Backend`: Minimal API, PostgreSQL persistence, encrypted GitHub token storage, migrations, and profile endpoints.
- `src/TaskSorter.Frontend`: React dashboard for profile management, config preview, filtering, and ranked task display.
- `src/TaskSorter.Tests`: core and backend API tests.

## Getting Started

### Prerequisites

- Docker with Compose V2
- A GitHub token with read access to the repositories you want to rank
- Optional for local development: .NET 10 SDK and Node.js 24+

### Run With Docker Compose

```bash
docker compose up --build
```

Open the frontend at:

```text
http://localhost:5173
```

The backend is exposed at:

```text
http://localhost:5111
```

PostgreSQL data, persisted GitHub cache entries, and ASP.NET Core Data Protection keys are kept in Docker volumes. The first backend startup applies migrations and creates a `Default` profile from the seed files in `src/TaskSorter.Backend/SeedData`.

## Usage

1. Select or create a profile.
2. Paste repository priority lines and label priority lines.
3. Set `Top`, request delay, and priority factors.
4. Save a GitHub token for the profile.
5. Run the profile and review the ranked queue.

Runs use the current form values, even before you save profile edits. For example, changing `Top` from 10 to 15 and clicking `Run` re-ranks the current GitHub task data up to 15 items without requiring `Save`.

Runs read GitHub issues for the selected profile token and can take time on large repository lists. Docker Compose defaults to a 240 second profile-run timeout, a 45 second timeout for each GitHub API request, and a 5 minute persisted backend cache for successful GitHub reads. The cache stores normalized GitHub task data, not final ranked result slices, so changes to `Top` or label scoring can reuse cached GitHub items and produce a new ranked queue. Cache entries are stored in PostgreSQL until their TTL expires and are warmed back into memory after backend container restarts. Use the refresh action or the Cache tab `Clear cache and refresh` button when you need to bypass cached GitHub data and fetch fresh issue lists. The Cache tab shows whether the last run used cached data, GitHub reads, refresh, or a mix.

Repository lines use this format:

```text
owner/repo core
owner/active-project active
owner/maintenance-project maintenance
```

Valid tiers are `core`, `active`, `maintenance`, `paused`, and `archive`. Repositories without a tier default to `active`.

Label lines use descending priority:

```text
priority/critical
status/in-progress
status/next
priority/high
type/bug
priority/medium
size/s
size/m
type/feature
type/docs
priority/low
size/l
```

TaskSorter also recognizes older labels such as `priority-high`, `scope-bug`, and `status-in-progress`.

Priority factors are editable from the web panel:

- Repository tier scores: `core`, `active`, `maintenance`, `paused`, `archive`
- Status scores: `in-progress`, `next`, `waiting`, `blocked`, default
- Size scores: `s`, `m`, `l`, default
- Assignment bonus
- Lock penalty

## How Ranking Works

TaskSorter fetches open issues and pull requests without modifying GitHub. Each task is scored from repository priority, project tier, configured labels, status labels, size labels, assignment, and lock state. The weights come from the saved profile’s priority factors, which are editable in the Priority tab. The dashboard shows the final score and score breakdown for each ranked task.

## Local Development

Backend:

```bash
dotnet run --project src/TaskSorter.Backend/TaskSorter.Backend.csproj
```

Frontend:

```bash
npm install --prefix src/TaskSorter.Frontend
npm run dev --prefix src/TaskSorter.Frontend
```

Useful backend settings:

- `ProfileRun:TimeoutSeconds`: maximum total time for one run.
- `GitHub:RequestTimeoutSeconds`: timeout for each outbound GitHub request.
- `GitHub:CacheEnabled`: enables the persisted backend GitHub read cache.
- `GitHub:CacheDurationSeconds`: cache TTL for successful GitHub reads.
- `GitHub:CacheMaxEntries`: maximum GitHub cache entries kept in memory and PostgreSQL.
- `GitHub:QuotaProtectionEnabled`: blocks fresh GitHub reads when the saved quota snapshot is at or below the reserve.
- `GitHub:QuotaReserveRequests`: protected request reserve, default `50`.
- `GitHub:QuotaWarningRemaining`: warning threshold shown in the UI, default `250`.
- `GitHub:QuotaSnapshotTtlSeconds`: how long a saved quota snapshot is trusted before rechecking GitHub, default `60`.

Tests:

```bash
dotnet test src/TaskSorter.slnx -p:NuGetAudit=false
npm run test --prefix src/TaskSorter.Frontend
npm run build --prefix src/TaskSorter.Frontend
```

## Documentation

- [API reference](docs/API.md)
- [Logging](docs/LOGGING.md)
- [FAQ](docs/FAQ.md)
- [Personal Project Workflow](docs/PERSONAL_PROJECT_WORKFLOW.md)
- [Goals](docs/GOALS.md)

## Security

GitHub tokens are accepted by the backend and encrypted with ASP.NET Core Data Protection before being stored in PostgreSQL. Decrypted tokens are never returned by API responses. Cache keys and quota snapshots use a SHA-256 token fingerprint instead of the raw token. Keep the Data Protection key volume private, because encrypted tokens depend on those keys.

Backend logs are structured JSON events and include request correlation ids. GitHub tokens and request bodies are not logged.

## License

This project is licensed under GPLv3. See [LICENSE](LICENSE).
