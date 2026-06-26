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
        await EnsureRepositoryTiersAsync(cancellationToken);

        if (!await dbContext.TaskProfiles.AnyAsync(cancellationToken))
        {
            var repositoryLines = await ReadSeedFileAsync("repo.txt", cancellationToken);
            var labelLines = await ReadSeedFileAsync("labels.txt", cancellationToken);
            var now = DateTimeOffset.UtcNow;
            dbContext.TaskProfiles.Add(new TaskProfile
            {
                Id = Guid.NewGuid(),
                Name = "Default",
                RepositoryLines = repositoryLines,
                LabelLines = labelLines,
                TaskLimit = AppDefaults.DefaultTaskLimit,
                DelayInMilliseconds = AppDefaults.DefaultDelayInMilliseconds,
                PriorityFactorsJson = TaskPriorityFactors.Default.ToJson(),
                EncryptedGitHubToken = secretProtector.Protect(string.Empty),
                CreatedAt = now,
                UpdatedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded default TaskSorter profile.");
        }

        await BackfillProfileRepositoriesAsync(cancellationToken);
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

    private async Task EnsureRepositoryTiersAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.RepositoryTiers.AnyAsync(cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;
        var defaults = new[]
        {
            new RepositoryTier { Id = Guid.NewGuid(), Name = "core", NormalizedName = "core", Score = 500, IsDefault = false, CreatedAt = now, UpdatedAt = now },
            new RepositoryTier { Id = Guid.NewGuid(), Name = "active", NormalizedName = "active", Score = 300, IsDefault = true, CreatedAt = now, UpdatedAt = now },
            new RepositoryTier { Id = Guid.NewGuid(), Name = "maintenance", NormalizedName = "maintenance", Score = 100, IsDefault = false, CreatedAt = now, UpdatedAt = now },
            new RepositoryTier { Id = Guid.NewGuid(), Name = "paused", NormalizedName = "paused", Score = -200, IsDefault = false, CreatedAt = now, UpdatedAt = now },
            new RepositoryTier { Id = Guid.NewGuid(), Name = "archive", NormalizedName = "archive", Score = -500, IsDefault = false, CreatedAt = now, UpdatedAt = now }
        };
        dbContext.RepositoryTiers.AddRange(defaults);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {RepositoryTierCount} default repository tiers.", defaults.Length);
    }

    private async Task BackfillProfileRepositoriesAsync(CancellationToken cancellationToken)
    {
        var profiles = await dbContext.TaskProfiles
            .Where(profile => !dbContext.ProfileRepositories.Any(repository => repository.ProfileId == profile.Id))
            .ToListAsync(cancellationToken);
        if (profiles.Count == 0)
            return;

        var tiers = await dbContext.RepositoryTiers.ToDictionaryAsync(tier => tier.NormalizedName, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var defaultTier = tiers.Values.Single(tier => tier.IsDefault);
        var now = DateTimeOffset.UtcNow;
        var addedCount = 0;

        foreach (var profile in profiles)
        {
            var order = 0;
            foreach (var line in profile.RepositoryLines.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split([' ', '\t', '|', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var repositoryParts = parts.FirstOrDefault()?.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (repositoryParts?.Length != 2)
                {
                    logger.LogWarning("RepositoryBackfillSkippedInvalidLine for {ProfileId}: {RepositoryLine}.", profile.Id, line);
                    continue;
                }

                var owner = repositoryParts[0].ToLowerInvariant();
                var name = repositoryParts[1].ToLowerInvariant();
                if (await dbContext.ProfileRepositories.AnyAsync(repository => repository.ProfileId == profile.Id && repository.Owner == owner && repository.Name == name, cancellationToken))
                {
                    logger.LogWarning("RepositoryBackfillSkippedDuplicate for {ProfileId}: {RepositoryFullName}.", profile.Id, $"{owner}/{name}");
                    continue;
                }

                var tierName = parts.Length > 1 ? parts[1].ToLowerInvariant() : defaultTier.NormalizedName;
                var tier = tiers.GetValueOrDefault(tierName) ?? defaultTier;
                if (tier == defaultTier && !string.Equals(tierName, defaultTier.NormalizedName, StringComparison.OrdinalIgnoreCase))
                    logger.LogWarning("RepositoryBackfillUnknownTier for {ProfileId}: {RepositoryFullName} requested {TierName}; default tier used.", profile.Id, $"{owner}/{name}", tierName);

                dbContext.ProfileRepositories.Add(new ProfileRepository
                {
                    Id = Guid.NewGuid(),
                    ProfileId = profile.Id,
                    Owner = owner,
                    Name = name,
                    RepositoryTierId = tier.Id,
                    SortOrder = order++,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                addedCount++;
            }
        }

        if (addedCount == 0)
            return;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Backfilled {ProfileRepositoryCount} repository assignment(s) from legacy profile configuration.", addedCount);
    }

}
