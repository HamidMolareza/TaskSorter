# FAQ

> Back to [Home](../README.md)

### 1. **What is TaskSorter?**

TaskSorter is a read-only CLI that fetches GitHub issues and pull requests across configured repositories and generates a ranked task queue.

### 2. **Who is TaskSorter for?**

TaskSorter is for developers who maintain several repositories and need a quick daily view of the most important issues to work on.

### 3. **Does TaskSorter modify GitHub issues?**

No. TaskSorter only reads issues and pull requests, then writes local report files. It does not create issues, edit labels, change assignees, or close tasks.

### 4. **How does TaskSorter calculate priority?**

TaskSorter scores each task from repository order, optional project tier, matching label priorities, status labels, size labels, assignment, and lock state. Higher scores appear earlier in the report.

### 5. **How should I format the repository priority file?**

List repositories in descending priority order. Add an optional tier after the repository name.

```text
owner/core-repo core
owner/active-repo active
owner/maintenance-repo maintenance
```

Valid tiers are `core`, `active`, `maintenance`, `paused`, and `archive`. Repositories without a tier default to `active`.

### 6. **How should I format the label priority file?**

List labels in descending priority order.

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

### 7. **What label set should I use across personal projects?**

Use a small shared set: `priority/*`, `type/*`, `status/*`, and `size/*`. The recommended labels are documented in [Personal Project Workflow](./PERSONAL_PROJECT_WORKFLOW.md).

### 8. **How many tasks should the report show?**

Use `--top` to keep the report focused. The default is 10, and `--top 5` is usually enough for a short daily planning session.

### 9. **How does this work with `projects-status`?**

Run `projects-status` first to find local repository hygiene work such as commits, pushes, missing remotes, or upstream setup. Then run TaskSorter to choose product/task work from GitHub Issues.

### 10. **Can TaskSorter be used with private repositories?**

Yes. Provide a GitHub token with read access to the private repositories you configure.

### 11. **Can I customize the scoring system?**

You can customize the order of repositories and labels through the input files. The built-in scoring weights for tiers, status, size, assignment, and lock state are currently fixed in code.

### 12. **Does TaskSorter support automated scheduling?**

TaskSorter does not include a scheduler. You can run it manually or call it from cron, systemd timers, or another local automation script.

> Back to [Home](../README.md)
