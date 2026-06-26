# API Reference

TaskSorter exposes a JSON API from `TaskSorter.Backend`. In Docker Compose the frontend proxies `/api` and `/health` to the backend.

## Health

`GET /health`

Returns backend and database health.

## Profiles

`GET /api/profiles`

Returns profile summaries: `id`, `name`, `taskLimit`, `delayInMilliseconds`, `hasGitHubToken`, and `updatedAt`.

`GET /api/profiles/{id}`

Returns editable profile data, including `priorityFactors`. The decrypted GitHub token is never returned.

`POST /api/profiles`

Creates a profile.

```json
{
  "name": "Daily",
  "repositoryLines": "owner/repo core",
  "labelLines": "priority/high\nstatus/next",
  "taskLimit": 10,
  "delayInMilliseconds": 500,
  "priorityFactors": {
    "repositoryTiers": {
      "core": 500,
      "active": 300,
      "maintenance": 100,
      "paused": -200,
      "archive": -500
    },
    "status": {
      "inProgress": 60,
      "next": 50,
      "waiting": -150,
      "blocked": -200,
      "default": 0
    },
    "size": {
      "small": 30,
      "medium": 15,
      "large": -10,
      "default": 0
    },
    "assignmentBonus": 20,
    "lockPenalty": -100
  },
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
  "delayInMilliseconds": 500,
  "priorityFactors": {
    "repositoryTiers": {
      "core": 500,
      "active": 300,
      "maintenance": 100,
      "paused": -200,
      "archive": -500
    },
    "status": {
      "inProgress": 60,
      "next": 50,
      "waiting": -150,
      "blocked": -200,
      "default": 0
    },
    "size": {
      "small": 30,
      "medium": 15,
      "large": -10,
      "default": 0
    },
    "assignmentBonus": 20,
    "lockPenalty": -100
  }
}
```

Returns normalized repositories, normalized labels, warnings, and validation errors. Repository preview scores use the supplied `priorityFactors.repositoryTiers` values.

## Run Profile

`POST /api/profiles/{id}/run`

Fetches GitHub issues and pull requests, scores them, applies the task limit, and returns ranked tasks. The saved profile supplies the encrypted GitHub token. The request body is optional; when present, it supplies the current editable run settings without saving them:

```json
{
  "repositoryLines": "owner/repo core",
  "labelLines": "priority/high\nstatus/next",
  "taskLimit": 15,
  "delayInMilliseconds": 0,
  "priorityFactors": {
    "repositoryTiers": {
      "core": 500,
      "active": 300,
      "maintenance": 100,
      "paused": -200,
      "archive": -500
    },
    "status": {
      "inProgress": 60,
      "next": 50,
      "waiting": -150,
      "blocked": -200,
      "default": 0
    },
    "size": {
      "small": 30,
      "medium": 15,
      "large": -10,
      "default": 0
    },
    "assignmentBonus": 20,
    "lockPenalty": -100
  }
}
```

When the body is omitted, the saved profile settings are used.

Task items include rank, GitHub URL, repository, project tier, labels, status, size, final score, score breakdown, and unscored labels.

Priority factors are saved per profile and can be edited from the web panel. The complete editable factor list is repository tier scores (`core`, `active`, `maintenance`, `paused`, `archive`), status scores (`inProgress`, `next`, `waiting`, `blocked`, `default`), size scores (`small`, `medium`, `large`, `default`), `assignmentBonus`, and `lockPenalty`.

Large profiles can take longer than normal CRUD requests because each run reads current-user issues and configured repository issues from GitHub. The backend returns `504 application/problem+json` when the configured profile-run timeout or GitHub request timeout is exceeded. Problem responses include the backend correlation id when one is available.

Successful GitHub reads are cached in the backend for `GitHub:CacheDurationSeconds` seconds. The backend keeps a hot in-memory copy and persists cache entries in PostgreSQL until they expire, so backend container restarts can reuse recent cache entries. The cache stores normalized GitHub operation results, not final ranked slices. Changing `taskLimit`, label order, label weights, or repository order re-ranks the cached tasks with the current request settings. Changing repository targets reuses cached matching targets and fetches only missing or stale targets.

TaskSorter also tracks GitHub REST API quota from response headers and, when a fresh GitHub read is needed and the saved snapshot is stale, from GitHub's `/rate_limit` endpoint. GitHub documents the quota headers as `x-ratelimit-limit`, `x-ratelimit-remaining`, `x-ratelimit-used`, `x-ratelimit-reset`, and `x-ratelimit-resource`; see [GitHub REST API rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api).

Run responses include cache and quota metadata:

```json
{
  "items": [],
  "warnings": [],
  "cache": {
    "status": "cache",
    "enabled": true,
    "refreshRequested": false,
    "durationSeconds": 300,
    "hitCount": 12,
    "gitHubRequestCount": 0,
    "operationCount": 12,
    "operations": [
      {
        "operation": "repository-issues",
        "target": "owner/repo",
        "source": "cache"
      }
    ]
  },
  "quota": {
    "status": "ok",
    "protectionEnabled": true,
    "reserveRequests": 50,
    "warningRemaining": 250,
    "estimatedRequiredRequests": 12,
    "actualGitHubRequestCount": 0,
    "limit": 5000,
    "remaining": 4920,
    "used": 80,
    "resetAt": "2026-06-25T12:00:00+00:00",
    "resetInSeconds": 1800,
    "source": "snapshot"
  }
}
```

`cache.status` can be `cache`, `github`, `mixed`, `refreshed`, or `disabled`. Operation `source` can be `cache`, `github`, `refresh`, or `disabled`.

`quota.status` can be `unknown`, `ok`, `low`, `protected`, `exhausted`, or `secondary-limited`. `quota.source` can be `snapshot`, `headers`, `rate-limit-endpoint`, or `unavailable`. Cached runs can complete without contacting GitHub; in that case quota is the latest saved snapshot or `unknown`.

`POST /api/profiles/{id}/run?refresh=true`

Bypasses existing GitHub cache entries for this run and stores fresh successful GitHub responses. `refresh=1` is also accepted.

`POST /api/profiles/{id}/run?refresh=true&quotaOverride=true`

Allows a forced refresh even when the saved quota snapshot is low. Use this only for an explicit user-confirmed refresh. `quotaOverride=1` is also accepted.

When quota protection blocks a normal run, the backend returns `429 application/problem+json`, sets `Retry-After` when a reset time is known, and includes a `quota` extension with the same quota shape shown above.

`POST /api/profiles/{id}/run/stream?refresh=true`

Runs the same profile request but streams newline-delimited JSON progress events with `Content-Type: application/x-ndjson`. The request body uses the same optional current-run shape as `/run`.

Events use this shape:

```json
{
  "type": "progress",
  "phase": "github",
  "message": "Loaded 12 task(s) for owner/repo from cache.",
  "completedOperations": 3,
  "totalOperations": 12,
  "operation": "repository-issues",
  "target": "owner/repo",
  "source": "cache",
  "itemCount": 12,
  "quota": {
    "status": "ok",
    "protectionEnabled": true,
    "reserveRequests": 50,
    "warningRemaining": 250,
    "estimatedRequiredRequests": 12,
    "actualGitHubRequestCount": 0,
    "limit": 5000,
    "remaining": 4920,
    "used": 80,
    "resetAt": "2026-06-25T12:00:00+00:00",
    "resetInSeconds": 1800,
    "source": "snapshot"
  }
}
```

`type` can be `started`, `progress`, `completed`, or `failed`. The `completed` event includes the normal run response in `result`.
