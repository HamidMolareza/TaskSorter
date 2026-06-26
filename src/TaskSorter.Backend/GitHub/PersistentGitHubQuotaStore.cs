using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Data;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Backend.GitHub;

public sealed class PersistentGitHubQuotaStore(
    AppDbContext dbContext,
    ILogger<PersistentGitHubQuotaStore> logger) : IGitHubQuotaStore
{
    public async Task<GitHubQuotaSnapshot?> GetAsync(
        string tokenFingerprint,
        string resource,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.GitHubQuotaSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.TokenFingerprint == tokenFingerprint && entry.Resource == resource,
                cancellationToken);

        return entry is null
            ? null
            : new GitHubQuotaSnapshot(
                entry.TokenFingerprint,
                entry.Resource,
                entry.Limit,
                entry.Remaining,
                entry.Used,
                entry.ResetAt,
                entry.CapturedAt,
                entry.Source);
    }

    public async Task UpsertAsync(
        GitHubQuotaSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await dbContext.GitHubQuotaSnapshots
                .FirstOrDefaultAsync(
                    entry => entry.TokenFingerprint == snapshot.TokenFingerprint && entry.Resource == snapshot.Resource,
                    cancellationToken);

            if (entry is null)
            {
                dbContext.GitHubQuotaSnapshots.Add(new GitHubQuotaSnapshotEntry
                {
                    TokenFingerprint = snapshot.TokenFingerprint,
                    Resource = snapshot.Resource,
                    Limit = snapshot.Limit,
                    Remaining = snapshot.Remaining,
                    Used = snapshot.Used,
                    ResetAt = snapshot.ResetAt,
                    CapturedAt = snapshot.CapturedAt,
                    Source = snapshot.Source
                });
            }
            else
            {
                entry.Limit = snapshot.Limit;
                entry.Remaining = snapshot.Remaining;
                entry.Used = snapshot.Used;
                entry.ResetAt = snapshot.ResetAt;
                entry.CapturedAt = snapshot.CapturedAt;
                entry.Source = snapshot.Source;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "GitHubQuotaSnapshotStoreFailed for resource {GitHubQuotaResource}.", snapshot.Resource);
        }
    }
}
