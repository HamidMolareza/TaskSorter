using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OnRails.Extensions.OnFail;
using OnRails.Extensions.OnSuccess;
using TaskSorter;
using TaskSorter.Helpers;
using TaskSorter.Settings;
using TaskSorter.Tasks;

var services = new ServiceCollection();

// Add configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();
services.AddSingleton<IConfiguration>(configuration);

// Add logging using configuration
services.AddLogging(builder => {
    builder.AddConfiguration(configuration.GetSection("Logging")); // Bind to "Logging" section
    builder.AddConsole();
});

var appSettings = configuration.GetSection("App")
    .Get<AppSettings>() ?? throw new ArgumentException("Can not load app settings data.");
services.AddSingleton(appSettings);

// Parse inputs and update the appSettings
var commandExitCode = await CommandLine.InvokeAsync(args, appSettings, new FileSystem());
if (commandExitCode != 0 || !CommandLine.ShouldRunApplication(args)) {
    Environment.ExitCode = commandExitCode;
    return;
}

// Add application services
services.AddTransient<App>();
services.AddTransient<TasksService>();
services.AddSingleton<IFileSystem, FileSystem>();

// -----------------------------------------------------------------
await using var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application Starting...");

try {
    var app = serviceProvider.GetRequiredService<App>();
    await app.RunAsync()
        .OnSuccessTee(() => logger.LogDebug("Operations completed successfully."))
        .OnFailTee(result => logger.LogError("{detail}",
            result.Detail?.ToString() ?? "No Data!"));
}
catch (Exception ex) {
    logger.LogError(ex, "An error occurred.");
}
