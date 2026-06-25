# FAQ

> Back to [Home](../README.md)

### 1. What is TaskSorter?

TaskSorter is a web app that ranks open GitHub issues and pull requests across configured repositories.

### 2. Does TaskSorter modify GitHub issues?

No. TaskSorter only reads GitHub issues and pull requests. It does not create issues, edit labels, assign users, close tasks, or write back to GitHub.

### 3. Where is configuration stored?

Configuration is stored in PostgreSQL as named profiles. A profile contains repository lines, label lines, task limit, request delay, and an encrypted GitHub token.

### 4. How are GitHub tokens stored?

Tokens are encrypted by the backend with ASP.NET Core Data Protection before they are saved. The frontend can submit or replace a token, but API reads only return `hasGitHubToken`.

### 5. What happens if the Data Protection key volume is deleted?

Previously encrypted tokens may become unreadable. Keep the `data-protection-keys` Docker volume if you want saved profile tokens to keep working.

### 6. How does TaskSorter calculate priority?

TaskSorter scores each task from repository order, optional project tier, matching label priorities, status labels, size labels, assignment, and lock state. Higher scores appear earlier in the queue.

### 7. How should I format repositories?

Use one repository per line, optionally followed by a tier.

```text
owner/core-repo core
owner/active-repo active
owner/maintenance-repo maintenance
```

Valid tiers are `core`, `active`, `maintenance`, `paused`, and `archive`.

### 8. How should I format labels?

Use one label per line in descending priority.

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

### 9. What does config preview do?

Config preview parses repository and label text without calling GitHub. It shows normalized repositories, label values, warnings, and validation errors before you save or run a profile.

### 10. Why does GitHub rate limiting matter?

Each run fetches current-user issues and repository issues. Large profiles can consume more GitHub API quota, so use the delay setting and keep profiles focused.

### 11. Can I use private repositories?

Yes, if the saved GitHub token has read access to those repositories.

### 12. How do I run everything locally?

Use Docker Compose:

```bash
docker compose up --build
```

Then open `http://localhost:5173`.

> Back to [Home](../README.md)
