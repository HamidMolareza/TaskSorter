# API Reference

TaskSorter exposes a JSON API from `TaskSorter.Backend`. Docker Compose proxies `/api` and `/health` through the frontend.

## Health

`GET /health` returns backend and database health.

## Profiles

`GET /api/profiles` returns profile summaries. `GET /api/profiles/{id}` includes persisted label rows, tuning, repository factor rows, and repository rows. GitHub tokens are never returned.

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

The token changes only when `gitHubToken` is non-empty. Profiles may be created incomplete; runs validate repositories and token. `DELETE /api/profiles/{id}` deletes the profile, repository factor rows, repository rows, ratings, and label rows.

## Repository Tiers

`GET /api/repository-tiers` returns global tiers with `id`, `name`, `score`, `isDefault`, `assignedRepositoryCount`, `rowVersion`, and `updatedAt`.

`POST /api/repository-tiers` accepts:

```json
{ "name": "active", "score": 300 }
```

`PUT /api/repository-tiers/{id}` accepts the same shape plus the latest `rowVersion`:

```json
{ "name": "active", "score": 300, "rowVersion": 1 }
```

Tier names are unique ignoring case. Stale tier updates return `409` with `latestTier`. `PUT /api/repository-tiers/{id}/default` changes the single default tier. `DELETE /api/repository-tiers/{id}` returns `409` when assignments exist; repeat with `?reassignAssignedRepositories=true` after confirmation to reassign them to the default tier. The default tier cannot be deleted.

## Profile Repositories

`GET /api/profiles/{profileId}/repositories`, `POST /api/profiles/{profileId}/repositories`, `PUT /api/profiles/{profileId}/repositories/{id}`, and `DELETE /api/profiles/{profileId}/repositories/{id}` manage the profile's repository rows.

Create body:

```json
{
  "owner": "owner",
  "name": "repo",
  "repositoryTierId": "00000000-0000-0000-0000-000000000000"
}
```

`repositoryTierId` may be omitted on create to use the current default tier. Update body:

```json
{
  "owner": "owner",
  "name": "repo",
  "repositoryTierId": "00000000-0000-0000-0000-000000000000",
  "rowVersion": 1
}
```

Repository names are unique within a profile. Responses include the selected tier, factor score, calculated score, row version, ratings, and validation state. `rowVersion` is required for updates; stale updates return `409` with the latest profile.

`PUT /api/profiles/{profileId}/repositories/{repositoryId}/factor-ratings/{factorId}` accepts:

```json
{ "rating": 5, "rowVersion": 1 }
```

Ratings must be between `1` and `5`. New repository-factor pairs default to rating `1`.

## Repository Priority Factors

`POST /api/profiles/{profileId}/repository-priority-factors`, `PUT /api/profiles/{profileId}/repository-priority-factors/{factorId}`, `DELETE /api/profiles/{profileId}/repository-priority-factors/{factorId}`, and `PUT /api/profiles/{profileId}/repository-priority-factors/order` manage profile-specific repository factors.

Create body:

```json
{
  "name": "Urgency",
  "description": "How soon does this repository need attention?",
  "weight": 8
}
```

Update body:

```json
{
  "name": "Urgency",
  "description": "How soon does this repository need attention?",
  "weight": 8,
  "rowVersion": 1
}
```

Responses include `id`, `name`, `description`, `weight`, `sortOrder`, `rowVersion`, and `updatedAt`. Factor names are unique within a profile, names are limited to 80 characters, descriptions to 500 characters, and weights to `-1000..1000`. Factor updates require `rowVersion`; stale updates return `409` with the latest profile. Reorder accepts `{ "factorIds": ["..."] }`.

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
