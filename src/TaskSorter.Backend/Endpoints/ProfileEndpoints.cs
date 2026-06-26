using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using TaskSorter.Backend.Data;
using TaskSorter.Backend.Profiles;
using TaskSorter.Backend.Security;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.GitHub;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Backend.Endpoints;

public static class ProfileEndpoints
{
    private const int DefaultProfileRunTimeoutSeconds = 240;
    private const int DefaultGitHubRequestTimeoutSeconds = 45;
    private const int ClientClosedRequestStatusCode = 499;
    private const string CorrelationItemName = "CorrelationId";
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapGet("/profiles", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var profiles = await dbContext.TaskProfiles
                .AsNoTracking()
                .OrderBy(profile => profile.Name)
                .Select(profile => ProfileSummaryResponse.FromEntity(profile))
                .ToListAsync(cancellationToken);

            return Results.Ok(profiles);
        });

        group.MapGet("/profiles/{id:guid}", async (
            Guid id,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var profile = await dbContext.TaskProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

            return profile is null
                ? Results.NotFound()
                : Results.Ok(ProfileDetailResponse.FromEntity(profile));
        });

        group.MapPost("/profiles", async (
            SaveProfileRequest request,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            ProfileConfigurationParser parser,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var validationErrors = ValidateRequest(request, parser);
            if (validationErrors.Count > 0)
            {
                logger.LogWarning(
                    "ProfileCreateRejected for {ProfileName} with {ValidationErrorCount} validation error(s).",
                    request.Name,
                    validationErrors.Count);
                return ValidationProblem(validationErrors);
            }

            var now = DateTimeOffset.UtcNow;
            var profile = new TaskProfile
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                RepositoryLines = request.RepositoryLines,
                LabelLines = request.LabelLines,
                TaskLimit = request.TaskLimit,
                DelayInMilliseconds = request.DelayInMilliseconds,
                PriorityFactorsJson = (request.PriorityFactors ?? TaskPriorityFactors.Default).ToJson(),
                EncryptedGitHubToken = secretProtector.Protect(request.GitHubToken ?? string.Empty),
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.TaskProfiles.Add(profile);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                logger.LogWarning(
                    "ProfileCreateRejected for {ProfileName} because the profile name is not unique.",
                    profile.Name);
                return ValidationProblem([new ValidationIssue("name", "Profile name must be unique.")]);
            }

            logger.LogInformation(
                "ProfileCreated for {ProfileId} named {ProfileName} with task limit {TaskLimit} and delay {DelayInMilliseconds} ms.",
                profile.Id,
                profile.Name,
                profile.TaskLimit,
                profile.DelayInMilliseconds);

            return Results.Created($"/api/profiles/{profile.Id}", ProfileDetailResponse.FromEntity(profile));
        });

        group.MapPut("/profiles/{id:guid}", async (
            Guid id,
            SaveProfileRequest request,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            ProfileConfigurationParser parser,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var validationErrors = ValidateRequest(request, parser);
            if (validationErrors.Count > 0)
            {
                logger.LogWarning(
                    "ProfileUpdateRejected for {ProfileId} with {ValidationErrorCount} validation error(s).",
                    id,
                    validationErrors.Count);
                return ValidationProblem(validationErrors);
            }

            var profile = await dbContext.TaskProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
            {
                logger.LogWarning("ProfileUpdateNotFound for {ProfileId}.", id);
                return Results.NotFound();
            }

            profile.Name = request.Name.Trim();
            profile.RepositoryLines = request.RepositoryLines;
            profile.LabelLines = request.LabelLines;
            profile.TaskLimit = request.TaskLimit;
            profile.DelayInMilliseconds = request.DelayInMilliseconds;
            profile.PriorityFactorsJson = (request.PriorityFactors ?? TaskPriorityFactors.Default).ToJson();
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.GitHubToken))
                profile.EncryptedGitHubToken = secretProtector.Protect(request.GitHubToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                logger.LogWarning(
                    "ProfileUpdateRejected for {ProfileId} because profile name {ProfileName} is not unique.",
                    id,
                    profile.Name);
                return ValidationProblem([new ValidationIssue("name", "Profile name must be unique.")]);
            }

            logger.LogInformation(
                "ProfileUpdated for {ProfileId} named {ProfileName} with task limit {TaskLimit} and delay {DelayInMilliseconds} ms.",
                profile.Id,
                profile.Name,
                profile.TaskLimit,
                profile.DelayInMilliseconds);

            return Results.Ok(ProfileDetailResponse.FromEntity(profile));
        });

        group.MapDelete("/profiles/{id:guid}", async (
            Guid id,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var profile = await dbContext.TaskProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
            {
                logger.LogWarning("ProfileDeleteNotFound for {ProfileId}.", id);
                return Results.NotFound();
            }

            dbContext.TaskProfiles.Remove(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("ProfileDeleted for {ProfileId} named {ProfileName}.", id, profile.Name);
            return Results.NoContent();
        });

        group.MapPost("/profiles/{id:guid}/run", async (
            Guid id,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            TaskRunner taskRunner,
            IConfiguration configuration,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var profile = await dbContext.TaskProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
            {
                logger.LogWarning("ProfileRunNotFound for {ProfileId}.", id);
                return Results.NotFound();
            }

            try
            {
                var refreshGitHubCache = ReadRefreshQuery(httpContext);
                var quotaOverride = ReadQuotaOverrideQuery(httpContext);
                var profileRunTimeout = ReadPositiveTimeout(
                    configuration,
                    "ProfileRun:TimeoutSeconds",
                    DefaultProfileRunTimeoutSeconds);
                var gitHubRequestTimeout = ReadPositiveTimeout(
                    configuration,
                    "GitHub:RequestTimeoutSeconds",
                    DefaultGitHubRequestTimeoutSeconds);
                using var timeoutCts = new CancellationTokenSource(profileRunTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var token = secretProtector.Unprotect(profile.EncryptedGitHubToken);
                var runConfiguration = await ReadRunConfigurationAsync(httpContext.Request, profile, cancellationToken);
                using (LogContext.PushProperty("ProfileId", profile.Id))
                using (LogContext.PushProperty("ProfileName", profile.Name))
                {
                    logger.LogInformation(
                        "ProfileRunRequested for {ProfileId} named {ProfileName} with run timeout {ProfileRunTimeoutSeconds} second(s), GitHub request timeout {GitHubRequestTimeoutSeconds} second(s), cache refresh {RefreshGitHubCache}, quota override {GitHubQuotaOverride}.",
                        profile.Id,
                        profile.Name,
                        profileRunTimeout.TotalSeconds,
                        gitHubRequestTimeout.TotalSeconds,
                        refreshGitHubCache,
                        quotaOverride);
                    var result = await taskRunner.RunAsync(
                        runConfiguration,
                        token,
                        gitHubRequestTimeout,
                        refreshGitHubCache,
                        quotaOverride,
                        linkedCts.Token);
                    return Results.Ok(TaskRunResponse.FromResult(result));
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunRejected for {ProfileId} because the request body was invalid JSON.",
                    profile.Id);
                return ValidationProblem([new ValidationIssue("body", "Run request body must be valid JSON.")]);
            }
            catch (ProfileConfigurationException ex)
            {
                logger.LogWarning(
                    "ProfileRunRejected for {ProfileId} with {ValidationErrorCount} validation error(s).",
                    profile.Id,
                    ex.Errors.Count);
                return ValidationProblem(ex.Errors);
            }
            catch (GitHubRequestTimeoutException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunGitHubRequestTimedOut for {ProfileId} during {GitHubOperation}.",
                    profile.Id,
                    ex.Operation);
                return TimeoutProblem(
                    "GitHub request timed out.",
                    "GitHub did not respond before the configured request timeout. Try again later or reduce the number of repositories in the profile.",
                    ex.Timeout,
                    GetCorrelationId(httpContext));
            }
            catch (GitHubQuotaProtectedException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunGitHubQuotaProtected for {ProfileId} during {GitHubOperation}.",
                    profile.Id,
                    ex.Operation);
                return QuotaProblem(
                    httpContext,
                    "GitHub quota protection stopped the run.",
                    "TaskSorter stopped before making another GitHub request because the saved quota snapshot is at or below the configured reserve.",
                    ex.Quota,
                    ex.RetryAfter,
                    GetCorrelationId(httpContext));
            }
            catch (GitHubQuotaExceededException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunGitHubQuotaExceeded for {ProfileId} during {GitHubOperation}.",
                    profile.Id,
                    ex.Operation);
                return QuotaProblem(
                    httpContext,
                    ex.Quota.Status == "secondary-limited"
                        ? "GitHub secondary rate limit reached."
                        : "GitHub rate limit reached.",
                    "GitHub asked TaskSorter to stop sending requests for this token. Wait for the reset time or use cached results.",
                    ex.Quota,
                    ex.RetryAfter,
                    GetCorrelationId(httpContext));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("ProfileRunClientAborted for {ProfileId}.", profile.Id);
                return Results.StatusCode(ClientClosedRequestStatusCode);
            }
            catch (OperationCanceledException)
            {
                var profileRunTimeout = ReadPositiveTimeout(
                    configuration,
                    "ProfileRun:TimeoutSeconds",
                    DefaultProfileRunTimeoutSeconds);
                logger.LogWarning(
                    "ProfileRunTimedOut for {ProfileId} after {ProfileRunTimeoutSeconds} second(s).",
                    profile.Id,
                    profileRunTimeout.TotalSeconds);
                return TimeoutProblem(
                    "Profile run timed out.",
                    "The profile run exceeded the configured timeout. Reduce the repository count, lower the delay, or increase ProfileRun:TimeoutSeconds.",
                    profileRunTimeout,
                    GetCorrelationId(httpContext));
            }
        });

        group.MapPost("/profiles/{id:guid}/run/stream", async (
            Guid id,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            TaskRunner taskRunner,
            IConfiguration configuration,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var profile = await dbContext.TaskProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
            {
                logger.LogWarning("ProfileRunStreamNotFound for {ProfileId}.", id);
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var refreshGitHubCache = ReadRefreshQuery(httpContext);
            var quotaOverride = ReadQuotaOverrideQuery(httpContext);
            var profileRunTimeout = ReadPositiveTimeout(
                configuration,
                "ProfileRun:TimeoutSeconds",
                DefaultProfileRunTimeoutSeconds);
            var gitHubRequestTimeout = ReadPositiveTimeout(
                configuration,
                "GitHub:RequestTimeoutSeconds",
                DefaultGitHubRequestTimeoutSeconds);

            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "application/x-ndjson";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                await WriteRunEventAsync(
                    httpContext,
                    new TaskRunStreamEvent(
                        "started",
                        "starting",
                        $"Starting run for {profile.Name}.",
                        CompletedOperations: 0,
                        TotalOperations: 0,
                        RefreshRequested: refreshGitHubCache,
                        ProfileName: profile.Name),
                    cancellationToken);

                using var timeoutCts = new CancellationTokenSource(profileRunTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                var token = secretProtector.Unprotect(profile.EncryptedGitHubToken);
                var runConfiguration = await ReadRunConfigurationAsync(httpContext.Request, profile, cancellationToken);
                var progressReporter = new StreamTaskRunProgressReporter(httpContext);

                using (LogContext.PushProperty("ProfileId", profile.Id))
                using (LogContext.PushProperty("ProfileName", profile.Name))
                {
                    logger.LogInformation(
                        "ProfileRunStreamRequested for {ProfileId} named {ProfileName} with run timeout {ProfileRunTimeoutSeconds} second(s), GitHub request timeout {GitHubRequestTimeoutSeconds} second(s), cache refresh {RefreshGitHubCache}, quota override {GitHubQuotaOverride}.",
                        profile.Id,
                        profile.Name,
                        profileRunTimeout.TotalSeconds,
                        gitHubRequestTimeout.TotalSeconds,
                        refreshGitHubCache,
                        quotaOverride);

                    var result = await taskRunner.RunAsync(
                        runConfiguration,
                        token,
                        gitHubRequestTimeout,
                        refreshGitHubCache,
                        quotaOverride,
                        linkedCts.Token,
                        progressReporter);

                    await WriteRunEventAsync(
                        httpContext,
                        new TaskRunStreamEvent(
                            "completed",
                            "completed",
                            $"Completed with {result.Items.Count} ranked task(s).",
                            CompletedOperations: result.Cache.OperationCount,
                            TotalOperations: result.Cache.OperationCount,
                            ItemCount: result.Items.Count,
                            RefreshRequested: refreshGitHubCache,
                            ProfileName: profile.Name,
                            Quota: TaskRunQuotaResponse.FromSummary(result.Quota),
                            Result: TaskRunResponse.FromResult(result)),
                        cancellationToken);
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunStreamRejected for {ProfileId} because the request body was invalid JSON.",
                    profile.Id);
                await WriteRunFailedEventAsync(
                    httpContext,
                    "Run request body must be valid JSON.",
                    GetCorrelationId(httpContext),
                    cancellationToken);
            }
            catch (ProfileConfigurationException ex)
            {
                logger.LogWarning(
                    "ProfileRunStreamRejected for {ProfileId} with {ValidationErrorCount} validation error(s).",
                    profile.Id,
                    ex.Errors.Count);
                await WriteRunFailedEventAsync(
                    httpContext,
                    $"Profile configuration is invalid: {string.Join(" ", ex.Errors.Select(error => $"{error.Field}: {error.Message}"))}",
                    GetCorrelationId(httpContext),
                    cancellationToken);
            }
            catch (GitHubRequestTimeoutException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunStreamGitHubRequestTimedOut for {ProfileId} during {GitHubOperation}.",
                    profile.Id,
                    ex.Operation);
                await WriteRunFailedEventAsync(
                    httpContext,
                    "GitHub did not respond before the configured request timeout. Try again later or reduce the number of repositories in the profile.",
                    GetCorrelationId(httpContext),
                    cancellationToken);
            }
            catch (GitHubQuotaProtectedException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunStreamGitHubQuotaProtected for {ProfileId} during {GitHubOperation}.",
                    profile.Id,
                    ex.Operation);
                await WriteRunFailedEventAsync(
                    httpContext,
                    "TaskSorter stopped before making another GitHub request because the saved quota snapshot is at or below the configured reserve.",
                    GetCorrelationId(httpContext),
                    cancellationToken,
                    TaskRunQuotaResponse.FromSummary(ex.Quota));
            }
            catch (GitHubQuotaExceededException ex)
            {
                logger.LogWarning(
                    ex,
                    "ProfileRunStreamGitHubQuotaExceeded for {ProfileId} during {GitHubOperation}.",
                    profile.Id,
                    ex.Operation);
                await WriteRunFailedEventAsync(
                    httpContext,
                    ex.Quota.Status == "secondary-limited"
                        ? "GitHub secondary rate limit reached. Wait before running again."
                        : "GitHub rate limit reached. Wait for the reset time or use cached results.",
                    GetCorrelationId(httpContext),
                    cancellationToken,
                    TaskRunQuotaResponse.FromSummary(ex.Quota));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("ProfileRunStreamClientAborted for {ProfileId}.", profile.Id);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "ProfileRunStreamTimedOut for {ProfileId} after {ProfileRunTimeoutSeconds} second(s).",
                    profile.Id,
                    profileRunTimeout.TotalSeconds);
                await WriteRunFailedEventAsync(
                    httpContext,
                    "The profile run exceeded the configured timeout. Reduce the repository count, lower the delay, or increase ProfileRun:TimeoutSeconds.",
                    GetCorrelationId(httpContext),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ProfileRunStreamFailed for {ProfileId}.", profile.Id);
                await WriteRunFailedEventAsync(
                    httpContext,
                    "Profile run failed. Check logs with the correlation id.",
                    GetCorrelationId(httpContext),
                    cancellationToken);
            }
        });

        group.MapPost("/preview-config", (PreviewConfigRequest request, ProfileConfigurationParser parser) =>
        {
            var preview = parser.Preview(request.ToConfiguration());
            return Results.Ok(ConfigPreviewResponse.FromPreview(preview, request.PriorityFactors ?? TaskPriorityFactors.Default));
        });

        return group;
    }

    private static async Task<ProfileConfiguration> ReadRunConfigurationAsync(
        HttpRequest request,
        TaskProfile profile,
        CancellationToken cancellationToken)
    {
        if (!HasJsonBody(request))
            return profile.ToConfiguration();

        var runRequest = await request.ReadFromJsonAsync<RunProfileRequest>(
            StreamJsonOptions,
            cancellationToken);
        return runRequest?.ToConfiguration() ?? profile.ToConfiguration();
    }

    private static bool HasJsonBody(HttpRequest request) =>
        request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");

    private static async Task WriteRunEventAsync(
        HttpContext httpContext,
        TaskRunStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            streamEvent,
            StreamJsonOptions,
            cancellationToken);
        await httpContext.Response.WriteAsync("\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private static Task WriteRunFailedEventAsync(
        HttpContext httpContext,
        string message,
        string? correlationId,
        CancellationToken cancellationToken,
        TaskRunQuotaResponse? quota = null) =>
        WriteRunEventAsync(
            httpContext,
            new TaskRunStreamEvent(
                "failed",
                "failed",
                message,
                CompletedOperations: 0,
                TotalOperations: 0,
                Quota: quota,
                Error: message,
                CorrelationId: correlationId),
            cancellationToken);

    private static IReadOnlyList<ValidationIssue> ValidateRequest(SaveProfileRequest request, ProfileConfigurationParser parser)
    {
        var errors = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(new ValidationIssue("name", "Profile name is required."));

        var preview = parser.Preview(request.ToConfiguration());
        errors.AddRange(preview.Errors);

        return errors;
    }

    private static IResult ValidationProblem(IReadOnlyList<ValidationIssue> errors) =>
        Results.ValidationProblem(errors
            .GroupBy(error => error.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray()));

    private static TimeSpan ReadPositiveTimeout(IConfiguration configuration, string key, int defaultSeconds)
    {
        var seconds = configuration.GetValue<int?>(key);
        return TimeSpan.FromSeconds(seconds is > 0 ? seconds.Value : defaultSeconds);
    }

    private static bool ReadRefreshQuery(HttpContext httpContext)
    {
        return ReadBooleanQuery(httpContext, "refresh");
    }

    private static bool ReadQuotaOverrideQuery(HttpContext httpContext) =>
        ReadBooleanQuery(httpContext, "quotaOverride");

    private static bool ReadBooleanQuery(HttpContext httpContext, string name)
    {
        var value = httpContext.Request.Query[name].FirstOrDefault();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static IResult TimeoutProblem(string title, string detail, TimeSpan timeout, string? correlationId)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = StatusCodes.Status504GatewayTimeout,
            Detail = detail
        };
        problem.Extensions["timeoutSeconds"] = (int)Math.Ceiling(timeout.TotalSeconds);
        if (!string.IsNullOrWhiteSpace(correlationId))
            problem.Extensions["correlationId"] = correlationId;

        return Results.Json(
            problem,
            contentType: "application/problem+json",
            statusCode: StatusCodes.Status504GatewayTimeout);
    }

    private static IResult QuotaProblem(
        HttpContext httpContext,
        string title,
        string detail,
        GitHubQuotaSummary quota,
        TimeSpan? retryAfter,
        string? correlationId)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = StatusCodes.Status429TooManyRequests,
            Detail = detail
        };
        problem.Extensions["quota"] = TaskRunQuotaResponse.FromSummary(quota);
        if (!string.IsNullOrWhiteSpace(correlationId))
            problem.Extensions["correlationId"] = correlationId;
        if (retryAfter is not null)
        {
            var retryAfterSeconds = Math.Max(0, (int)Math.Ceiling(retryAfter.Value.TotalSeconds));
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }

        return Results.Json(
            problem,
            contentType: "application/problem+json",
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    private static string? GetCorrelationId(HttpContext httpContext) =>
        httpContext.Items[CorrelationItemName]?.ToString();

    private static ILogger CreateLogger(ILoggerFactory loggerFactory) =>
        loggerFactory.CreateLogger("TaskSorter.Backend.ProfileEndpoints");

    private sealed class StreamTaskRunProgressReporter(HttpContext httpContext) : ITaskRunProgressReporter
    {
        public ValueTask ReportAsync(TaskRunProgress progress, CancellationToken cancellationToken) =>
            new(WriteRunEventAsync(
                httpContext,
                new TaskRunStreamEvent(
                    "progress",
                    progress.Phase,
                    progress.Message,
                    progress.CompletedOperations,
                    progress.TotalOperations,
                    progress.Operation,
                    progress.Target,
                    progress.Source,
                    progress.ItemCount,
                    Quota: progress.Quota is null ? null : TaskRunQuotaResponse.FromSummary(progress.Quota)),
                cancellationToken));
    }
}
