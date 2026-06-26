using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaskSorter.Backend.Data;
using TaskSorter.Backend.GitHub;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Tests.Backend;

public sealed class PersistentGitHubQuotaStoreTests
{
    [Fact]
    public async Task UpsertAsync_ReusesPersistedSnapshotAcrossStoreInstances()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString("N");
        var snapshot = new GitHubQuotaSnapshot(
            "token-fingerprint",
            "core",
            Limit: 5000,
            Remaining: 1234,
            Used: 3766,
            ResetAt: DateTimeOffset.Parse("2026-06-25T12:00:00Z"),
            CapturedAt: DateTimeOffset.Parse("2026-06-25T11:00:00Z"),
            Source: "headers");

        using (var firstProvider = CreateProvider(databaseName, databaseRoot))
        {
            var firstStore = CreateStore(firstProvider);
            await firstStore.UpsertAsync(snapshot, CancellationToken.None);
        }

        using (var secondProvider = CreateProvider(databaseName, databaseRoot))
        {
            var secondStore = CreateStore(secondProvider);
            var persisted = await secondStore.GetAsync("token-fingerprint", "core", CancellationToken.None);

            Assert.NotNull(persisted);
            Assert.Equal(1234, persisted.Remaining);
            Assert.Equal("headers", persisted.Source);
        }
    }

    private static ServiceProvider CreateProvider(string databaseName, InMemoryDatabaseRoot databaseRoot)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        return services.BuildServiceProvider();
    }

    private static PersistentGitHubQuotaStore CreateStore(IServiceProvider serviceProvider) =>
        new(
            serviceProvider.GetRequiredService<AppDbContext>(),
            NullLogger<PersistentGitHubQuotaStore>.Instance);
}
