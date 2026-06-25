# Personal Project Workflow

TaskSorter is intended to be the read-only queue generator for personal projects tracked with GitHub Issues.

## Source Of Truth

- Each project owns its tasks in GitHub Issues.
- Each project should keep a short `docs/GOALS.md` with purpose, current phase, and the top 1-3 outcomes.
- TaskSorter keeps cross-project priority outside GitHub issues through the repository priority file.
- `projects-status` is only a local repository hygiene signal. It should not decide product priority.

## Labels

Use a small shared label set across repositories:

```text
priority/critical
priority/high
priority/medium
priority/low
type/bug
type/feature
type/performance
type/docs
type/chore
status/next
status/in-progress
status/blocked
status/waiting
size/s
size/m
size/l
```

TaskSorter also recognizes the older TaskSorter labels such as `priority-high`, `scope-bug`, and `status-in-progress`.

## Project Tiers

The repository priority file supports optional project tiers:

```text
HamidMolareza/TaskSorter core
HamidMolareza/SomeActiveProject active
HamidMolareza/OldProject maintenance
HamidMolareza/PausedIdea paused
HamidMolareza/ArchivedIdea archive
```

Repositories without a tier default to `active`.

## Routine

## Daily Routine

1. Run `projects-status`.
2. If the selected repository has uncommitted or unpushed work, handle that before starting a new issue.
3. Run TaskSorter:

```bash
dotnet run --project src/TaskSorter/TaskSorter.csproj -- --top 10
```

4. Pick one task from the top queue based on available time and energy.
5. Keep work in progress narrow: one active issue per project and one to three active issues total.

## Weekly Routine

- Review active repositories and adjust tiers.
- Close, downgrade, or pause stale low-value issues.
- Keep each active project to one to three `status/next` issues.
- Move projects that are not realistic this month to `paused` or `archive`.

## Issue Readiness

A task is ready for the daily queue when it has:

- one `priority/*` label,
- one `type/*` label,
- one `status/next` or `status/in-progress` label,
- one `size/*` label when the work size is known.
