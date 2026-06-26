# Logging

TaskSorter backend logs are structured JSON events from Serilog.

## Outputs

- `docker compose logs backend` shows compact JSON events on stdout.
- Docker Compose persists backend file logs in the `backend-logs` volume at `/app/logs`.
- The default rolling file pattern is `logs/backend-.clef`.

## Correlation

Every backend response includes an `X-Correlation-ID` header. If a request sends the header, TaskSorter reuses it. Otherwise the backend generates one. Search logs by `CorrelationId` to connect request logs, profile-run logs, GitHub fetch logs, and unhandled exception logs.

## Useful Events

- `ProfileCreated`, `ProfileUpdated`, `ProfileDeleted`
- `ProfileRunRequested`, `ProfileRunStarted`, `ProfileRunCompleted`, `ProfileRunFailed`
- `ProfileRunTimedOut`, `ProfileRunGitHubRequestTimedOut`, `ProfileRunClientAborted`
- `GitHubCurrentUserTaskFetchStarted`, `GitHubCurrentUserTaskFetchCompleted`
- `GitHubRepositoryTaskFetchStarted`, `GitHubRepositoryTaskFetchCompleted`, `GitHubRepositoryTaskFetchFailed`
- `GitHubCacheHit`, `GitHubCacheMiss`, `GitHubCacheBypassed`, `GitHubCacheStored`, `GitHubCacheExpired`, `GitHubCacheDeserializeFailed`, `GitHubCacheStoreFailed`, `GitHubCacheDisabled`
- `GitHubQuotaSnapshotFetched`, `GitHubQuotaProtectionBlocked`, `GitHubPrimaryRateLimitExceeded`, `GitHubSecondaryRateLimitExceeded`, `GitHubQuotaSnapshotFetchFailed`, `GitHubQuotaSnapshotStoreFailed`
- `GitHubRequestTimedOut`
- HTTP request completion events with method, path, status code, elapsed time, host, user agent, client IP, and correlation id

`ProfileRunTimedOut` means the overall run exceeded `ProfileRun:TimeoutSeconds`. `GitHubRequestTimedOut` means one GitHub API call exceeded `GitHub:RequestTimeoutSeconds`. `GitHubCacheHit` means the run reused cached GitHub data from memory or PostgreSQL. `GitHubCacheBypassed` means the request used `refresh=true` and intentionally skipped the existing cache entry. `GitHubQuotaProtectionBlocked` means TaskSorter stopped before a fresh GitHub read because the saved quota snapshot was at or below the configured reserve. Docker Compose sets the profile-run timeout to 240 seconds, GitHub request timeout to 45 seconds, GitHub cache duration to 300 seconds, and quota reserve to 50 requests.

## Sensitive Data

GitHub tokens, request bodies, and decrypted secrets are not logged. API responses expose only `hasGitHubToken`, and logs should use profile ids/names, operation targets, cache status, and operational counts instead of secret values.
