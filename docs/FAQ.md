# FAQ

> Back to [Home](../README.md)

### 1. What is TaskSorter?

TaskSorter is a web app that ranks open GitHub issues and pull requests across configured repositories.

### 2. Does TaskSorter modify GitHub issues?

No. TaskSorter only reads GitHub issues and pull requests. It does not create issues, edit labels, assign users, close tasks, or write back to GitHub.

### 3. Where is configuration stored?

Configuration is stored in PostgreSQL as named profiles, global repository tiers, per-profile repository rows, and per-profile label rows. Each label row is ordered, pending, or ignored.

### 4. How are GitHub tokens stored?

Tokens are encrypted by the backend with ASP.NET Core Data Protection before they are saved. The frontend can submit or replace a token, but API reads only return `hasGitHubToken`.

### 5. How do I switch or manage profiles?

Use the avatar in the header to see the active profile, switch to another profile, or create a new one. The Settings tab contains the active profile name, GitHub token, and confirmed delete action. Profile edits save automatically, and the current profile is saved before a switch.

### 6. What happens if the Data Protection key volume is deleted?

Previously encrypted tokens may become unreadable. Keep the `data-protection-keys` Docker volume if you want saved profile tokens to keep working.

### 7. How does TaskSorter calculate priority?

TaskSorter scores each task from repository row order, its selected repository tier score, matching label priorities, assignment, and lock state. Higher scores appear earlier in the queue. Labels such as `status/*` and `size/*` are normal label priority entries, so their weights are controlled in the Labels tab. The Scoring tab manages global repository tiers and the profile's assignment bonus and lock penalty.

### 8. How do I manage repositories and tiers?

Add each repository as an `owner/repository` row in the Repositories tab, select one tier from the combobox, then drag it or use the move controls to set its order. Rows save automatically and reject duplicate repositories in the same profile. The Scoring tab manages the global tier catalog and its default tier. Deleting an assigned non-default tier asks for confirmation, then reassigns those repositories to the default tier.

### 9. How are labels discovered and ordered?

The Labels tab collects distinct labels from open issues and pull requests in configured repositories. It uses the backend GitHub cache first and fetches only stale or missing repository issue lists. Discovery removes saved labels that no longer occur in those repositories, including ignored and legacy rows. New labels are highlighted until you drag them into the ranked list or ignore them. Every ranked label has one unique position, with higher rows ranking first. Ignored labels stay out of ranking; restoring one returns it to the highlighted pending list.

### 10. How does validation work?

Repository rows show validation status and reject invalid or duplicate names. A profile can be created before it is complete, but running requires at least one repository and a GitHub token. Pending or ignored labels remain unscored until ranked.

### 11. Why does GitHub rate limiting matter?

Each uncached run fetches current-user issues and repository issues. Large profiles can consume more GitHub API quota, so use the delay setting, keep profiles focused, and rely on the default GitHub cache for repeated runs. TaskSorter stores a small quota snapshot per token fingerprint and blocks fresh GitHub reads when the snapshot is at or below `GitHub:QuotaReserveRequests` unless you explicitly confirm a quota override.

### 12. How does GitHub caching work?

TaskSorter caches successful GitHub reads in the backend for 5 minutes by default. The backend keeps a hot in-memory copy and stores cache entries in PostgreSQL until their TTL expires, so Docker backend restarts can reuse recent cached data. It caches normalized GitHub task data, not final ranked results. If you change `Top`, label order, repository row order, tier assignment, tier score, or tuning, TaskSorter re-ranks cached items with the current settings. Use the Cache tab `Clear cache and refresh` button when fresh GitHub data is required.

### 13. Why can a run time out?

The run button waits for GitHub reads across the whole profile. Large profiles, slow GitHub responses, or high delay settings can exceed the configured timeout. TaskSorter returns a JSON timeout error and logs the correlation id so you can search backend logs.

### 14. Can I use private repositories?

Yes, if the saved GitHub token has read access to those repositories.

### 15. How do I run everything locally?

Use Docker Compose:

```bash
docker compose up --build
```

Then open `http://localhost:5173`.

> Back to [Home](../README.md)
