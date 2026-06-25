# Goals

## Purpose

TaskSorter provides a ranked, read-only web dashboard for GitHub issues and pull requests across personal repositories.

## Current Phase

Move from a CLI report generator to a dockerized backend plus frontend workflow with saved profiles.

## Completed Outcomes

- Generate a top 5-10 task queue from GitHub Issues without modifying GitHub.
- Support cross-project priority through repository ordering and project tiers.
- Support lightweight task ranking through priority, type, status, and size labels.
- Replace the CLI-first workflow with an ASP.NET Core backend and React dashboard.
- Persist named profiles in PostgreSQL and store GitHub tokens encrypted.

## Next Outcomes

- Improve dashboard filtering after real daily use.
- Add optional run history if live-only runs are not enough.
- Consider optional label validation reports without changing GitHub.
