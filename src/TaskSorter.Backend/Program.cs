using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Data;
using TaskSorter.Backend.Endpoints;
using TaskSorter.Backend.Security;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.GitHub;
using TaskSorter.Core.Tasks;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IGitHubTaskClient, GitHubTaskClient>();
builder.Services.AddScoped<TaskRunner>();
builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

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
    await initializer.InitializeAsync();
}

app.Run();

public partial class Program;
