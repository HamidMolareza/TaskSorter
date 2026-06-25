using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskSorter.Core.GitHub;

namespace TaskSorter.Tests.Backend;

public sealed class TaskSorterWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "tasksorter-test-keys")
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubTaskClient>();
            services.AddScoped<IGitHubTaskClient, FakeGitHubTaskClient>();
        });
    }
}
