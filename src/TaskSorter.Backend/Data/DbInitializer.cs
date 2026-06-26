using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Profiles;
using TaskSorter.Backend.Security;
using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Data;

public sealed class DbInitializer(
    AppDbContext dbContext,
    ISecretProtector secretProtector,
    IWebHostEnvironment environment,
    ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            logger.LogInformation("Database migration started.");
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migration completed.");
        }
        else
        {
            logger.LogInformation("Database ensure-created started for non-relational provider.");
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("Database ensure-created completed for non-relational provider.");
        }

        await PruneExpiredGitHubCacheEntriesAsync(cancellationToken);
        await PruneExpiredGitHubQuotaSnapshotsAsync(cancellationToken);

        if (await dbContext.TaskProfiles.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Default profile seeding skipped because profiles already exist.");
            return;
        }

        var repositoryLines = await ReadSeedFileAsync("repo.txt", cancellationToken);
        var labelLines = await ReadSeedFileAsync("labels.txt", cancellationToken);

        dbContext.TaskProfiles.Add(new TaskProfile
        {
            Name = "Default",
            RepositoryLines = repositoryLines,
            LabelLines = labelLines,
            TaskLimit = AppDefaults.DefaultTaskLimit,
            DelayInMilliseconds = AppDefaults.DefaultDelayInMilliseconds,
            PriorityFactorsJson = TaskPriorityFactors.Default.ToJson(),
            EncryptedGitHubToken = secretProtector.Protect(string.Empty),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default TaskSorter profile.");
    }

    private async Task PruneExpiredGitHubCacheEntriesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredEntries = await dbContext.GitHubCacheEntries
            .Where(entry => entry.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredEntries.Count == 0)
            return;

        dbContext.GitHubCacheEntries.RemoveRange(expiredEntries);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Removed {GitHubCacheEntryCount} expired GitHub cache entries.", expiredEntries.Count);
    }

    private async Task PruneExpiredGitHubQuotaSnapshotsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredEntries = await dbContext.GitHubQuotaSnapshots
            .Where(entry => entry.ResetAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredEntries.Count == 0)
            return;

        dbContext.GitHubQuotaSnapshots.RemoveRange(expiredEntries);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Removed {GitHubQuotaSnapshotCount} expired GitHub quota snapshot(s).", expiredEntries.Count);
    }

    private async Task<string> ReadSeedFileAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, "SeedData", fileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("Seed file {SeedFileName} was not found at {SeedFilePath}.", fileName, path);
            return string.Empty;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
