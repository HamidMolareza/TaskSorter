using Microsoft.Extensions.Logging;

namespace ConsoleSharpTemplate.Services;

public class ExampleService(ILogger<ExampleService> logger, AppSettings settings) {
    public async Task Run() {
        logger.LogInformation("ExampleService is running. Delay: {delay}", settings.Delay);
        await Task.Delay(settings.Delay); // Simulate some work
        logger.LogInformation("ExampleService completed.");
    }
}