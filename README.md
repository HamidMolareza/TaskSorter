# TaskSorter

TaskSorter is a web dashboard for ranking GitHub issues and pull requests across multiple repositories. It keeps GitHub as the read-only task source, stores reusable ranking profiles in PostgreSQL, and helps choose a focused queue for daily project work.

## Built With

- Backend: ASP.NET Core Web API, .NET 10, EF Core, PostgreSQL
- Frontend: React 19, TypeScript, Vite
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

PostgreSQL data and ASP.NET Core Data Protection keys are persisted in Docker volumes. The first backend startup applies migrations and creates a `Default` profile from the seed files in `src/TaskSorter.Backend/SeedData`.

## Usage

1. Select or create a profile.
2. Paste repository priority lines and label priority lines.
3. Set `Top` and request delay values.
4. Save a GitHub token for the profile.
5. Run the profile and review the ranked queue.

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

## How Ranking Works

TaskSorter fetches open issues and pull requests without modifying GitHub. Each task is scored from repository priority, project tier, configured labels, status labels, size labels, assignment, and lock state. The dashboard shows the final score and score breakdown for each ranked task.

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

Tests:

```bash
dotnet test src/TaskSorter.slnx -p:NuGetAudit=false
npm run test --prefix src/TaskSorter.Frontend
npm run build --prefix src/TaskSorter.Frontend
```

## Documentation

- [API reference](docs/API.md)
- [FAQ](docs/FAQ.md)
- [Personal Project Workflow](docs/PERSONAL_PROJECT_WORKFLOW.md)
- [Goals](docs/GOALS.md)

## Security

GitHub tokens are accepted by the backend and encrypted with ASP.NET Core Data Protection before being stored in PostgreSQL. Decrypted tokens are never returned by API responses. Keep the Data Protection key volume private, because encrypted tokens depend on those keys.

## License

This project is licensed under GPLv3. See [LICENSE](LICENSE).
