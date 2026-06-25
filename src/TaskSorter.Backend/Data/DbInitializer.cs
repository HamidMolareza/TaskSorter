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
            await dbContext.Database.MigrateAsync(cancellationToken);
        else
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (await dbContext.TaskProfiles.AnyAsync(cancellationToken))
            return;

        var repositoryLines = await ReadSeedFileAsync("repo.txt", cancellationToken);
        var labelLines = await ReadSeedFileAsync("labels.txt", cancellationToken);

        dbContext.TaskProfiles.Add(new TaskProfile
        {
            Name = "Default",
            RepositoryLines = repositoryLines,
            LabelLines = labelLines,
            TaskLimit = AppDefaults.DefaultTaskLimit,
            DelayInMilliseconds = AppDefaults.DefaultDelayInMilliseconds,
            EncryptedGitHubToken = secretProtector.Protect(string.Empty),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default TaskSorter profile.");
    }

    private async Task<string> ReadSeedFileAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.ContentRootPath, "SeedData", fileName);
        if (!File.Exists(path))
            return string.Empty;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
