using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TaskSorter.Backend.Data;
using TaskSorter.Backend.Endpoints;
using TaskSorter.Backend.GitHub;
using TaskSorter.Backend.Security;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.GitHub;
using TaskSorter.Core.Tasks;

const string CorrelationHeaderName = "X-Correlation-ID";
const string CorrelationItemName = "CorrelationId";
const int ClientClosedRequestStatusCode = 499;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", AppDefaults.AppName)
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting {Application}.", AppDefaults.AppName);

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", AppDefaults.AppName)
        .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName));

    builder.Services.AddOpenApi();
    builder.Services.AddCors(options =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        options.AddPolicy("Frontend", policy =>
        {
            if (origins.Length > 0)
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            else
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
    });

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        if (builder.Environment.IsEnvironment("Testing"))
            options.UseInMemoryDatabase("tasksorter-tests");
        else
            options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    });

    var gitHubCacheOptions = ReadGitHubCacheOptions(builder.Configuration);
    var gitHubQuotaOptions = ReadGitHubQuotaOptions(builder.Configuration);
    builder.Services.AddSingleton(gitHubCacheOptions);
    builder.Services.AddSingleton(gitHubQuotaOptions);
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = gitHubCacheOptions.MaxEntries;
    });

    var keysPath = builder.Configuration["DataProtection:KeysPath"];
    if (builder.Environment.IsEnvironment("Testing"))
        keysPath = Path.Combine(Path.GetTempPath(), "tasksorter-test-keys");
    var dataProtection = builder.Services
        .AddDataProtection()
        .SetApplicationName(AppDefaults.AppName);
    if (!string.IsNullOrWhiteSpace(keysPath))
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

    builder.Services.AddScoped<DbInitializer>();
    builder.Services.AddSingleton<ProfileConfigurationParser>();
    builder.Services.AddSingleton<TaskRanker>();
    builder.Services.AddSingleton<IGitHubRequestCache, PersistentGitHubRequestCache>();
    builder.Services.AddScoped<IGitHubQuotaStore, PersistentGitHubQuotaStore>();
    builder.Services.AddScoped<GitHubQuotaService>();
    builder.Services.AddScoped<IGitHubTaskClient, GitHubTaskClient>();
    builder.Services.AddScoped<TaskRunner>();
    builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.Use(async (context, next) =>
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[CorrelationItemName] = correlationId;
        context.Response.Headers[CorrelationHeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next();
        }
    });

    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                ex,
                "Request canceled by client for {RequestMethod} {RequestPath}.",
                context.Request.Method,
                context.Request.Path.Value);

            context.Response.StatusCode = ClientClosedRequestStatusCode;
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var correlationId = context.Items[CorrelationItemName]?.ToString();
            logger.LogError(
                ex,
                "Unhandled request exception for {RequestMethod} {RequestPath}.",
                context.Request.Method,
                context.Request.Path.Value);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Use the correlation id to find the matching backend log event."
            };
            problem.Extensions["correlationId"] = correlationId;

            await context.Response.WriteAsJsonAsync(problem);
        }
    });

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex is not null || httpContext.Response.StatusCode >= 500)
                return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400 || elapsed > 5000)
                return LogEventLevel.Warning;
            return LogEventLevel.Information;
        };
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseCors("Frontend");

    app.MapGet("/health", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
    {
        var databaseOk = await dbContext.Database.CanConnectAsync(cancellationToken);
        return databaseOk
            ? Results.Ok(new { status = "ok", database = "ok" })
            : Results.Problem("Database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    });

    app.MapProfileEndpoints();

    using (var scope = app.Services.CreateScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database initialization started.");
        await initializer.InitializeAsync();
        logger.LogInformation("Database initialization completed.");
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "{Application} backend terminated unexpectedly.", AppDefaults.AppName);
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static string GetOrCreateCorrelationId(HttpContext context)
{
    var headerValue = context.Request.Headers[CorrelationHeaderName].FirstOrDefault();
    return string.IsNullOrWhiteSpace(headerValue)
        ? Guid.NewGuid().ToString("N")
        : headerValue.Trim();
}

static GitHubCacheOptions ReadGitHubCacheOptions(IConfiguration configuration)
{
    const int defaultCacheDurationSeconds = 300;
    const int defaultCacheMaxEntries = 1000;

    return new GitHubCacheOptions(
        configuration.GetValue<bool?>("GitHub:CacheEnabled") ?? true,
        TimeSpan.FromSeconds(ReadPositiveInt(configuration, "GitHub:CacheDurationSeconds", defaultCacheDurationSeconds)),
        ReadPositiveInt(configuration, "GitHub:CacheMaxEntries", defaultCacheMaxEntries));
}

static GitHubQuotaOptions ReadGitHubQuotaOptions(IConfiguration configuration)
{
    const int defaultQuotaReserveRequests = 50;
    const int defaultQuotaWarningRemaining = 250;
    const int defaultQuotaSnapshotTtlSeconds = 60;

    return new GitHubQuotaOptions(
        configuration.GetValue<bool?>("GitHub:QuotaProtectionEnabled") ?? true,
        ReadNonNegativeInt(configuration, "GitHub:QuotaReserveRequests", defaultQuotaReserveRequests),
        ReadNonNegativeInt(configuration, "GitHub:QuotaWarningRemaining", defaultQuotaWarningRemaining),
        TimeSpan.FromSeconds(ReadPositiveInt(configuration, "GitHub:QuotaSnapshotTtlSeconds", defaultQuotaSnapshotTtlSeconds)));
}

static int ReadPositiveInt(IConfiguration configuration, string key, int defaultValue)
{
    var value = configuration.GetValue<int?>(key);
    return value is > 0 ? value.Value : defaultValue;
}

static int ReadNonNegativeInt(IConfiguration configuration, string key, int defaultValue)
{
    var value = configuration.GetValue<int?>(key);
    return value is >= 0 ? value.Value : defaultValue;
}

public partial class Program;
