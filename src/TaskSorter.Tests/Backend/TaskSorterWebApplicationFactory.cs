using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Tests.Backend;

public sealed class TaskSorterWebApplicationFactory : WebApplicationFactory<Program>
{
    internal FakeGitHubTaskClient GitHubTaskClient { get; } = new();

    public string LogDirectory { get; } = Path.Combine(
        Path.GetTempPath(),
        "tasksorter-test-logs",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
        {
            Directory.CreateDirectory(LogDirectory);
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "tasksorter-test-keys"),
                ["ProfileRun:TimeoutSeconds"] = "1",
                ["GitHub:RequestTimeoutSeconds"] = "1",
                ["Serilog:WriteTo:1:Args:path"] = Path.Combine(LogDirectory, "backend-.clef"),
                ["Serilog:WriteTo:1:Args:flushToDiskInterval"] = "00:00:00.050"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubTaskClient>();
            services.AddSingleton(GitHubTaskClient);
            services.AddScoped<IGitHubTaskClient>(serviceProvider => serviceProvider.GetRequiredService<FakeGitHubTaskClient>());
        });
    }

    public async Task<string> ReadLogTextAsync(string expectedText, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var logs = ReadCurrentLogText();
            if (logs.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                return logs;

            await Task.Delay(100, cancellationToken);
        }

        return ReadCurrentLogText();
    }

    private string ReadCurrentLogText()
    {
        if (!Directory.Exists(LogDirectory))
            return string.Empty;

        var files = Directory.GetFiles(LogDirectory, "*.clef");
        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }
}
