using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaskSorter.Backend.Data;
using TaskSorter.Backend.GitHub;
using TaskSorter.Core.GitHub;
using TaskSorter.Core.Models;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Tests.Backend;

public sealed class PersistentGitHubRequestCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReusesPersistedTaskListAcrossCacheInstances()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString("N");
        var request = CreateRequest();
        var calls = 0;

        using (var firstProvider = CreateProvider(databaseName, databaseRoot))
        {
            var firstCache = CreateCache(firstProvider);
            var first = await firstCache.GetOrCreateAsync(
                request,
                _ =>
                {
                    calls++;
                    return Task.FromResult(CreateTaskList("Cached issue"));
                },
                CancellationToken.None);

            Assert.True(first.FetchedFromSource);
        }

        using (var secondProvider = CreateProvider(databaseName, databaseRoot))
        {
            var secondCache = CreateCache(secondProvider);
            var second = await secondCache.GetOrCreateAsync(
                request,
                _ =>
                {
                    calls++;
                    return Task.FromResult(CreateTaskList("Fresh issue"));
                },
                CancellationToken.None);

            Assert.False(second.FetchedFromSource);
            Assert.Equal("Cached issue", second.Value[0].Title);
            Assert.Equal("owner/repo", second.Value[0].Repository.ToString());
            Assert.Equal("priority/high", second.Value[0].Labels[0].Name);
        }

        Assert.Equal(1, calls);
    }

    private static ServiceProvider CreateProvider(string databaseName, InMemoryDatabaseRoot databaseRoot)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache(options => options.SizeLimit = 1000);
        services.AddSingleton(new GitHubCacheOptions(true, TimeSpan.FromMinutes(5), 1000));
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        return services.BuildServiceProvider();
    }

    private static PersistentGitHubRequestCache CreateCache(IServiceProvider serviceProvider) =>
        new(
            serviceProvider.GetRequiredService<IMemoryCache>(),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<GitHubCacheOptions>(),
            NullLogger<PersistentGitHubRequestCache>.Instance);

    private static GitHubCacheRequest CreateRequest() =>
        new(
            "github:repository-issues:test-token:owner/repo",
            "repository-issues",
            "owner/repo",
            Enabled: true,
            Refresh: false,
            Duration: TimeSpan.FromMinutes(5));

    private static List<TaskData> CreateTaskList(string title) =>
    [
        new TaskData
        {
            Id = 1,
            Title = title,
            Type = TaskTypes.Issue,
            Repository = new Repository("Owner", "Repo"),
            Labels = [new Label("priority/high")],
            Url = "https://github.com/owner/repo/issues/1",
            Assigned = true,
            Locked = false,
            CreatedAt = DateTimeOffset.Parse("2026-06-25T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-06-25T01:00:00Z")
        }
    ];
}
