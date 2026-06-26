# API Reference

TaskSorter exposes a JSON API from `TaskSorter.Backend`. Docker Compose proxies `/api` and `/health` through the frontend.

## Health

`GET /health` returns backend and database health.

## Profiles

`GET /api/profiles` returns profile summaries. `GET /api/profiles/{id}` includes persisted label rows, tuning, and repository rows. GitHub tokens are never returned.

`POST /api/profiles` and `PUT /api/profiles/{id}` use this body:

```json
{
  "name": "Daily",
  "taskLimit": 10,
  "delayInMilliseconds": 500,
  "priorityFactors": {
    "assignmentBonus": 20,
    "lockPenalty": -100
  },
  "gitHubToken": "ghp_..."
}
```

The token changes only when `gitHubToken` is non-empty. Profiles may be created incomplete; runs validate repositories and token. `DELETE /api/profiles/{id}` deletes the profile, repository rows, and label rows.

## Repository Tiers

`GET /api/repository-tiers` returns global tiers with `id`, `name`, `score`, `isDefault`, and `assignedRepositoryCount`.

`POST /api/repository-tiers` and `PUT /api/repository-tiers/{id}` accept:

```json
{ "name": "active", "score": 300 }
```

Tier names are unique ignoring case. `PUT /api/repository-tiers/{id}/default` changes the single default tier. `DELETE /api/repository-tiers/{id}` returns `409` when assignments exist; repeat with `?reassignAssignedRepositories=true` after confirmation to reassign them to the default tier. The default tier cannot be deleted.

## Profile Repositories

`GET /api/profiles/{profileId}/repositories`, `POST /api/profiles/{profileId}/repositories`, `PUT /api/profiles/{profileId}/repositories/{id}`, and `DELETE /api/profiles/{profileId}/repositories/{id}` manage the profile's repository rows.

```json
{
  "owner": "owner",
  "name": "repo",
  "repositoryTierId": "00000000-0000-0000-0000-000000000000"
}
```

Repository names are unique within a profile. Responses include the selected tier, position score, calculated priority score, and validation state. `PUT /api/profiles/{profileId}/repositories/order` accepts `{ "repositoryIds": ["..."] }` to persist sorted rows.

## Profile Labels

`GET /api/profiles/{profileId}/labels` returns persisted label rows. `POST /api/profiles/{profileId}/labels/discover` collects distinct labels from configured repository issue lists. It uses persistent cache entries first, fetches only missing or expired targets, and returns cache and quota metadata. Add `?refresh=true` only when fresh GitHub label data is required. Discovery reconciles the saved rows with the collected labels: it preserves order and ignore state for labels still in use, adds new labels as pending, and removes rows that no longer occur in the configured repositories. The response includes `newLabelCount` and `removedLabelCount`.

New labels have `isPending: true` until they are placed in the ranked list or become ignored. The ranked list has one unique position per label; higher rows have higher priority.

`PUT /api/profiles/{profileId}/labels/{id}` accepts:

```json
{ "isIgnored": true }
```

`PUT /api/profiles/{profileId}/labels/order` accepts the complete ranked list as `{ "labelIds": ["...", "..."] }`. The list must retain every ranked label and may add one pending label at the intended position. Restored labels return to pending state until ranked again.

## Run Profile

`POST /api/profiles/{id}/run` and `POST /api/profiles/{id}/run/stream` rank persisted repositories. The stream endpoint returns newline-delimited JSON progress events.

An optional current-settings body can be supplied:

```json
{
  "taskLimit": 15,
  "delayInMilliseconds": 0,
  "priorityFactors": {
    "assignmentBonus": 20,
    "lockPenalty": -100
  }
}
```

`?refresh=true` bypasses GitHub cache entries for the run and updates them. `?quotaOverride=true` is accepted only for explicit, user-confirmed refreshes when quota is low. Successful responses include ranked items, warnings, cache metadata, and GitHub quota metadata. The cache stores normalized GitHub reads, not final ranked slices, so current settings can re-rank cached items without new GitHub requests.
