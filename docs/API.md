# API Reference

TaskSorter exposes a JSON API from `TaskSorter.Backend`. In Docker Compose the frontend proxies `/api` and `/health` to the backend.

## Health

`GET /health`

Returns backend and database health.

## Profiles

`GET /api/profiles`

Returns profile summaries: `id`, `name`, `taskLimit`, `delayInMilliseconds`, `hasGitHubToken`, and `updatedAt`.

`GET /api/profiles/{id}`

Returns editable profile data. The decrypted GitHub token is never returned.

`POST /api/profiles`

Creates a profile.

```json
{
  "name": "Daily",
  "repositoryLines": "owner/repo core",
  "labelLines": "priority/high\nstatus/next",
  "taskLimit": 10,
  "delayInMilliseconds": 500,
  "gitHubToken": "ghp_..."
}
```

`PUT /api/profiles/{id}`

Updates a profile. The token is changed only when `gitHubToken` is non-empty.

`DELETE /api/profiles/{id}`

Deletes a profile.

## Config Preview

`POST /api/preview-config`

Parses repository and label text without calling GitHub.

```json
{
  "repositoryLines": "owner/repo core",
  "labelLines": "priority/high\nstatus/next",
  "taskLimit": 10,
  "delayInMilliseconds": 500
}
```

Returns normalized repositories, normalized labels, warnings, and validation errors.

## Run Profile

`POST /api/profiles/{id}/run`

Fetches GitHub issues and pull requests, scores them, applies the profile task limit, and returns ranked tasks.

Task items include rank, GitHub URL, repository, project tier, labels, status, size, final score, score breakdown, and unscored labels.
