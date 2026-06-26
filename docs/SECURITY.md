# Security Policy

## Token Storage

TaskSorter stores GitHub tokens only in the backend database and encrypts them with ASP.NET Core Data Protection.
The decrypted token is never returned by API responses. Keep the PostgreSQL volume and Data Protection key volume
private; access to both can allow token recovery.

## GitHub Cache

Successful GitHub API responses are cached in the backend and persisted in PostgreSQL until their TTL expires. Cache
keys use a SHA-256 token fingerprint plus operation details, not the raw token. Cache contents can include issue and
pull request metadata returned by GitHub, so keep the PostgreSQL volume private. API cache status responses expose
operation names, repository targets, counts, and sources only; they do not expose cache keys, token fingerprints, or
raw tokens.

## GitHub Quota Snapshots

TaskSorter persists GitHub quota snapshots in PostgreSQL so quota protection survives backend container restarts.
Snapshots are keyed by the same SHA-256 token fingerprint pattern and store only quota numbers, reset time, capture
time, resource name, and source. They do not store raw GitHub tokens or cache keys. API responses expose quota status,
remaining count, reserve, reset time, and source so the UI can warn before spending more GitHub requests.

## Logging

TaskSorter logs operational metadata such as profile ids, profile names, repository names, task counts, status codes, elapsed times, and correlation ids. It must not log GitHub tokens, decrypted secrets, or request bodies.

## Reporting a Vulnerability

If there are any vulnerabilities in this project, don't hesitate to _report them_.

1. Use any of the contact addresses.
2. Describe the vulnerability.

   If you have a fix, that is most welcome -- please attach or summarize it in your message!

3. We will evaluate the vulnerability and, if necessary, release a fix or mitigating steps to address it. We will
   contact you to let you know the outcome, and will credit you in the report.

   Please **do not disclose the vulnerability publicly** until a fix is released!

4. Once we have either a) published a fix, or b) declined to address the vulnerability for whatever reason, you are free
   to publicly disclose it.
