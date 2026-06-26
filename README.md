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

1. Use the profile avatar in the header to see the active profile, switch to another profile, or create one.
2. Add repository rows, select a tier for each one, then drag rows or use the move controls to set their order.
3. Discover repository labels, then drag each new label into its unique priority position or ignore it.
4. Open the Settings tab to rename the active profile or enter its GitHub token.
5. Open Ranked Queue, choose `Top` and `Delay`, then run the profile and review its results.

Existing profiles save automatically after edits. The profile avatar menu saves the current profile before switching. New profiles use an explicit Create action from that menu. Running a profile flushes current profile changes first, so changing `Top` from 10 to 15 re-ranks the current GitHub task data up to 15 items.

Runs read GitHub issues for the selected profile token and can take time on large repository lists. Docker Compose defaults to a 240 second profile-run timeout, a 45 second timeout for each GitHub API request, and a 5 minute persisted backend cache for successful GitHub reads. The cache stores normalized GitHub task data, not final ranked result slices, so changes to `Top` or label scoring can reuse cached GitHub items and produce a new ranked queue. Cache entries are stored in PostgreSQL until their TTL expires and are warmed back into memory after backend container restarts. Use the refresh action or the Cache tab `Clear cache and refresh` button when you need to bypass cached GitHub data and fetch fresh issue lists. The Cache tab shows whether the last run used cached data, GitHub reads, refresh, or a mix.

Repository tiers are global database records, seeded with `core`, `active`, `maintenance`, `paused`, and `archive`. The Scoring tab lets you create, rename, score, set the default, and delete tiers. A repository receives exactly one tier from its combobox; deleting an assigned non-default tier requires confirmation and moves affected repositories to the default tier.

The Labels tab collects distinct labels from configured repository issues. It reuses persisted GitHub cache entries, fetching only stale or missing repository targets while respecting quota protection. Discovery reconciles the saved labels with the repositories: labels no longer found are removed, while existing order and ignore choices are retained. New labels stay highlighted until dragged into the ranked list or ignored. Every ranked label has one unique position; ignored labels can be restored as pending labels.

Scoring contains global repository tier management plus per-profile assignment bonus and lock penalty tuning. Labels remain dynamic and are configured only in the Labels tab.

## How Ranking Works

TaskSorter fetches open issues and pull requests without modifying GitHub. Each task is scored from repository row order, its selected repository tier score, configured label priorities, assignment, and lock state. Labels such as `status/*` and `size/*` are normal label priority entries, so their weights belong in the Labels tab. The dashboard shows each repository's calculated priority and the final task score breakdown.

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

Validation:

```bash
dotnet test src/TaskSorter.Tests/TaskSorter.Tests.csproj -p:NuGetAudit=false
npm run test --prefix src/TaskSorter.Frontend
npm run build --prefix src/TaskSorter.Frontend
```

PR coverage runs the same test project with coverlet collector and excludes EF migrations plus generated `obj` sources from the coverage denominator. Keep workflow paths pointed at concrete projects under `src/`; this repository does not keep a solution file at the repo root.

## Documentation

- [API reference](docs/API.md)
- [Logging](docs/LOGGING.md)
- [FAQ](docs/FAQ.md)
- [Personal Project Workflow](docs/PERSONAL_PROJECT_WORKFLOW.md)
- [Goals](docs/GOALS.md)
- [Agent instructions](AGENTS.md)

## Security

GitHub tokens are accepted by the backend and encrypted with ASP.NET Core Data Protection before being stored in PostgreSQL. Decrypted tokens are never returned by API responses. Cache keys and quota snapshots use a SHA-256 token fingerprint instead of the raw token. Keep the Data Protection key volume private, because encrypted tokens depend on those keys.

Backend logs are structured JSON events and include request correlation ids. GitHub tokens and request bodies are not logged.

## License

This project is licensed under GPLv3. See [LICENSE](LICENSE).
