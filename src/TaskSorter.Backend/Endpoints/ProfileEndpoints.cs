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
                .Include(profile => profile.Repositories)
                .ThenInclude(repository => repository.RepositoryTier)
                .Include(profile => profile.Labels)
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
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var validationErrors = ValidateProfileRequest(request);
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
                RepositoryLines = string.Empty,
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
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var validationErrors = ValidateProfileRequest(request);
            if (validationErrors.Count > 0)
            {
                logger.LogWarning(
                    "ProfileUpdateRejected for {ProfileId} with {ValidationErrorCount} validation error(s).",
                    id,
                    validationErrors.Count);
                return ValidationProblem(validationErrors);
            }

            var profile = await dbContext.TaskProfiles
                .Include(profile => profile.Repositories)
                .ThenInclude(repository => repository.RepositoryTier)
                .Include(profile => profile.Labels)
                .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
            {
                logger.LogWarning("ProfileUpdateNotFound for {ProfileId}.", id);
                return Results.NotFound();
            }

            profile.Name = request.Name.Trim();
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

        MapRepositoryTierEndpoints(group);
        MapProfileRepositoryEndpoints(group);
        MapProfileLabelEndpoints(group);

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
                .Include(profile => profile.Repositories)
                .ThenInclude(repository => repository.RepositoryTier)
                .Include(profile => profile.Labels)
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
                .Include(profile => profile.Repositories)
                .ThenInclude(repository => repository.RepositoryTier)
                .Include(profile => profile.Labels)
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
        return runRequest?.ApplyTo(profile) ?? profile.ToConfiguration();
    }

    private static void MapRepositoryTierEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/repository-tiers", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tiers = await dbContext.RepositoryTiers
                .AsNoTracking()
                .OrderByDescending(tier => tier.IsDefault)
                .ThenBy(tier => tier.Name)
                .Select(tier => new RepositoryTierResponse(
                    tier.Id,
                    tier.Name,
                    tier.Score,
                    tier.IsDefault,
                    tier.ProfileRepositories.Count,
                    tier.UpdatedAt))
                .ToListAsync(cancellationToken);
            return Results.Ok(tiers);
        });

        group.MapPost("/repository-tiers", async (
            SaveRepositoryTierRequest request,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var name = NormalizeTierName(request.Name);
            if (name is null)
                return ValidationProblem([new ValidationIssue("name", "Tier name is required and must be 80 characters or fewer.")]);

            if (await dbContext.RepositoryTiers.AnyAsync(tier => tier.NormalizedName == name, cancellationToken))
                return ValidationProblem([new ValidationIssue("name", "Tier name must be unique.")]);

            var now = DateTimeOffset.UtcNow;
            var tier = new RepositoryTier
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                NormalizedName = name,
                Score = request.Score,
                IsDefault = !await dbContext.RepositoryTiers.AnyAsync(cancellationToken),
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.RepositoryTiers.Add(tier);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("RepositoryTierCreated for {RepositoryTierId} named {RepositoryTierName}.", tier.Id, tier.Name);
            return Results.Created($"/api/repository-tiers/{tier.Id}", RepositoryTierResponse.FromEntity(tier, 0));
        });

        group.MapPut("/repository-tiers/{id:guid}", async (
            Guid id,
            SaveRepositoryTierRequest request,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var tier = await dbContext.RepositoryTiers.FirstOrDefaultAsync(tier => tier.Id == id, cancellationToken);
            if (tier is null)
                return Results.NotFound();

            var name = NormalizeTierName(request.Name);
            if (name is null)
                return ValidationProblem([new ValidationIssue("name", "Tier name is required and must be 80 characters or fewer.")]);
            if (await dbContext.RepositoryTiers.AnyAsync(other => other.Id != id && other.NormalizedName == name, cancellationToken))
                return ValidationProblem([new ValidationIssue("name", "Tier name must be unique.")]);

            tier.Name = request.Name.Trim();
            tier.NormalizedName = name;
            tier.Score = request.Score;
            tier.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            CreateLogger(loggerFactory).LogInformation("RepositoryTierUpdated for {RepositoryTierId} named {RepositoryTierName}.", tier.Id, tier.Name);
            var assignedCount = await dbContext.ProfileRepositories.CountAsync(repository => repository.RepositoryTierId == id, cancellationToken);
            return Results.Ok(RepositoryTierResponse.FromEntity(tier, assignedCount));
        });

        group.MapPut("/repository-tiers/{id:guid}/default", async (
            Guid id,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var tier = await dbContext.RepositoryTiers.FirstOrDefaultAsync(tier => tier.Id == id, cancellationToken);
            if (tier is null)
                return Results.NotFound();

            if (!tier.IsDefault)
            {
                var updatedAt = DateTimeOffset.UtcNow;
                if (dbContext.Database.IsRelational())
                {
                    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                    await dbContext.RepositoryTiers
                        .Where(candidate => candidate.IsDefault)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(candidate => candidate.IsDefault, false),
                            cancellationToken);
                    tier.IsDefault = true;
                    tier.UpdatedAt = updatedAt;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    var currentDefault = await dbContext.RepositoryTiers.Where(candidate => candidate.IsDefault).ToListAsync(cancellationToken);
                    foreach (var candidate in currentDefault)
                        candidate.IsDefault = false;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    tier.IsDefault = true;
                    tier.UpdatedAt = updatedAt;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            CreateLogger(loggerFactory).LogInformation("RepositoryTierSetDefault for {RepositoryTierId} named {RepositoryTierName}.", tier.Id, tier.Name);
            var assignedCount = await dbContext.ProfileRepositories.CountAsync(repository => repository.RepositoryTierId == id, cancellationToken);
            return Results.Ok(RepositoryTierResponse.FromEntity(tier, assignedCount));
        });

        group.MapDelete("/repository-tiers/{id:guid}", async (
            Guid id,
            bool? reassignAssignedRepositories,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var tier = await dbContext.RepositoryTiers.FirstOrDefaultAsync(tier => tier.Id == id, cancellationToken);
            if (tier is null)
                return Results.NotFound();
            if (tier.IsDefault)
                return Results.Conflict(new { message = "The default tier cannot be deleted. Choose another default tier first." });

            var assigned = await dbContext.ProfileRepositories
                .Where(repository => repository.RepositoryTierId == id)
                .ToListAsync(cancellationToken);
            if (assigned.Count > 0 && reassignAssignedRepositories != true)
                return Results.Conflict(new { message = $"{assigned.Count} repository assignment(s) use this tier.", assignedRepositoryCount = assigned.Count });

            if (assigned.Count > 0)
            {
                var defaultTier = await dbContext.RepositoryTiers.FirstOrDefaultAsync(candidate => candidate.IsDefault, cancellationToken);
                if (defaultTier is null)
                    return Results.Problem("A default repository tier is required before reassignment.", statusCode: StatusCodes.Status409Conflict);
                foreach (var repository in assigned)
                {
                    repository.RepositoryTierId = defaultTier.Id;
                    repository.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            dbContext.RepositoryTiers.Remove(tier);
            await dbContext.SaveChangesAsync(cancellationToken);
            CreateLogger(loggerFactory).LogInformation("RepositoryTierDeleted for {RepositoryTierId} named {RepositoryTierName}; reassigned {ReassignedCount} repository assignment(s).", id, tier.Name, assigned.Count);
            return Results.NoContent();
        });
    }

    private static void MapProfileRepositoryEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/profiles/{profileId:guid}/repositories", async (Guid profileId, AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var repositories = await dbContext.ProfileRepositories
                .AsNoTracking()
                .Include(repository => repository.RepositoryTier)
                .Where(repository => repository.ProfileId == profileId)
                .OrderBy(repository => repository.SortOrder)
                .ToListAsync(cancellationToken);
            return Results.Ok(repositories.Select((repository, index) => ProfileRepositoryResponse.FromEntity(repository, repositories.Count - index + 1)).ToList());
        });

        group.MapPost("/profiles/{profileId:guid}/repositories", async (
            Guid profileId,
            SaveProfileRepositoryRequest request,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!await dbContext.TaskProfiles.AnyAsync(profile => profile.Id == profileId, cancellationToken))
                return Results.NotFound();
            var coordinates = NormalizeRepository(request.Owner, request.Name);
            if (coordinates is null)
                return ValidationProblem([new ValidationIssue("repository", "Repository must use an owner and name without slashes.")]);
            if (await dbContext.ProfileRepositories.AnyAsync(repository => repository.ProfileId == profileId && repository.Owner == coordinates.Value.Owner && repository.Name == coordinates.Value.Name, cancellationToken))
                return ValidationProblem([new ValidationIssue("repository", "This repository is already configured for the profile.")]);

            var tier = await ResolveTierAsync(request.RepositoryTierId, dbContext, cancellationToken);
            if (tier is null)
                return ValidationProblem([new ValidationIssue("repositoryTierId", "Choose a valid repository tier.")]);
            var sortOrder = await dbContext.ProfileRepositories.CountAsync(repository => repository.ProfileId == profileId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var repository = new ProfileRepository
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                Owner = coordinates.Value.Owner,
                Name = coordinates.Value.Name,
                RepositoryTierId = tier.Id,
                RepositoryTier = tier,
                SortOrder = sortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ProfileRepositories.Add(repository);
            await dbContext.SaveChangesAsync(cancellationToken);
            CreateLogger(loggerFactory).LogInformation("ProfileRepositoryCreated for {ProfileId}: {RepositoryFullName} using tier {RepositoryTierId}.", profileId, repository.FullName, tier.Id);
            return Results.Created($"/api/profiles/{profileId}/repositories/{repository.Id}", ProfileRepositoryResponse.FromEntity(repository, sortOrder + 1));
        });

        group.MapPut("/profiles/{profileId:guid}/repositories/{id:guid}", async (
            Guid profileId,
            Guid id,
            UpdateProfileRepositoryRequest request,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var repository = await dbContext.ProfileRepositories
                .Include(candidate => candidate.RepositoryTier)
                .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProfileId == profileId, cancellationToken);
            if (repository is null)
                return Results.NotFound();
            var coordinates = NormalizeRepository(request.Owner, request.Name);
            if (coordinates is null)
                return ValidationProblem([new ValidationIssue("repository", "Repository must use an owner and name without slashes.")]);
            if (await dbContext.ProfileRepositories.AnyAsync(candidate => candidate.ProfileId == profileId && candidate.Id != id && candidate.Owner == coordinates.Value.Owner && candidate.Name == coordinates.Value.Name, cancellationToken))
                return ValidationProblem([new ValidationIssue("repository", "This repository is already configured for the profile.")]);
            var tier = await ResolveTierAsync(request.RepositoryTierId, dbContext, cancellationToken);
            if (tier is null)
                return ValidationProblem([new ValidationIssue("repositoryTierId", "Choose a valid repository tier.")]);

            repository.Owner = coordinates.Value.Owner;
            repository.Name = coordinates.Value.Name;
            repository.RepositoryTierId = tier.Id;
            repository.RepositoryTier = tier;
            repository.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            CreateLogger(loggerFactory).LogInformation("ProfileRepositoryUpdated for {ProfileId}: {RepositoryId}.", profileId, id);
            var count = await dbContext.ProfileRepositories.CountAsync(candidate => candidate.ProfileId == profileId && candidate.SortOrder <= repository.SortOrder, cancellationToken);
            var total = await dbContext.ProfileRepositories.CountAsync(candidate => candidate.ProfileId == profileId, cancellationToken);
            return Results.Ok(ProfileRepositoryResponse.FromEntity(repository, total - count + 1));
        });

        group.MapDelete("/profiles/{profileId:guid}/repositories/{id:guid}", async (Guid profileId, Guid id, AppDbContext dbContext, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var repository = await dbContext.ProfileRepositories.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProfileId == profileId, cancellationToken);
            if (repository is null)
                return Results.NotFound();
            dbContext.ProfileRepositories.Remove(repository);
            await dbContext.SaveChangesAsync(cancellationToken);
            await NormalizeRepositorySortOrderAsync(profileId, dbContext, cancellationToken);
            CreateLogger(loggerFactory).LogInformation("ProfileRepositoryDeleted for {ProfileId}: {RepositoryId}.", profileId, id);
            return Results.NoContent();
        });

        group.MapPut("/profiles/{profileId:guid}/repositories/order", async (Guid profileId, ReorderProfileRepositoriesRequest request, AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var repositories = await dbContext.ProfileRepositories.Where(repository => repository.ProfileId == profileId).ToListAsync(cancellationToken);
            if (repositories.Count != request.RepositoryIds.Count || repositories.Select(repository => repository.Id).Except(request.RepositoryIds).Any())
                return ValidationProblem([new ValidationIssue("repositoryIds", "The reorder list must contain every repository exactly once.")]);
            await ApplyRepositoryOrderAsync(repositories, request.RepositoryIds, dbContext, cancellationToken);
            return Results.NoContent();
        });
    }

    private static void MapProfileLabelEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/profiles/{profileId:guid}/labels", async (Guid profileId, AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var labels = await dbContext.ProfileLabels
                .AsNoTracking()
                .Where(label => label.ProfileId == profileId)
                .OrderBy(label => label.IsIgnored)
                .ThenBy(label => !label.SortOrder.HasValue)
                .ThenBy(label => label.SortOrder)
                .ToListAsync(cancellationToken);
            return Results.Ok(labels.Select(ProfileLabelResponse.FromEntity).ToList());
        });

        group.MapPost("/profiles/{profileId:guid}/labels/discover", async (
            Guid profileId,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            IGitHubTaskClient gitHubTaskClient,
            IConfiguration configuration,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = CreateLogger(loggerFactory);
            var profile = await dbContext.TaskProfiles
                .Include(candidate => candidate.Repositories)
                .ThenInclude(repository => repository.RepositoryTier)
                .Include(candidate => candidate.Labels)
                .FirstOrDefaultAsync(candidate => candidate.Id == profileId, cancellationToken);
            if (profile is null)
                return Results.NotFound();
            if (profile.Repositories.Count == 0)
                return ValidationProblem([new ValidationIssue("repositories", "Add at least one repository before discovering labels.")]);

            var token = secretProtector.Unprotect(profile.EncryptedGitHubToken);
            if (string.IsNullOrWhiteSpace(token))
                return ValidationProblem([new ValidationIssue("githubToken", "GitHub token is required before discovering labels.")]);

            var refresh = ReadRefreshQuery(httpContext);
            var requestTimeout = ReadPositiveTimeout(
                configuration,
                "GitHub:RequestTimeoutSeconds",
                DefaultGitHubRequestTimeoutSeconds);
            var repositories = profile.ToConfiguration().ConfiguredRepositories ?? [];
            using (LogContext.PushProperty("ProfileId", profile.Id))
            using (LogContext.PushProperty("ProfileName", profile.Name))
            {
                logger.LogInformation(
                    "ProfileLabelDiscoveryRequested for {ProfileId} with {RepositoryCount} repository target(s), cache refresh {RefreshGitHubCache}.",
                    profile.Id,
                    repositories.Count,
                    refresh);

                var fetch = await gitHubTaskClient.GetRepositoryTasksAsync(
                    repositories,
                    token,
                    profile.DelayInMilliseconds,
                    requestTimeout,
                    refresh,
                    quotaOverride: false,
                    cancellationToken);
                var discoveredAt = DateTimeOffset.UtcNow;
                var discovered = fetch.Tasks
                    .SelectMany(task => task.Labels)
                    .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
                var discoveredNames = discovered
                    .Select(label => label.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var removedLabels = profile.Labels
                    .Where(label => !discoveredNames.Contains(label.NormalizedName))
                    .ToList();
                dbContext.ProfileLabels.RemoveRange(removedLabels);
                foreach (var label in removedLabels)
                    profile.Labels.Remove(label);

                var existing = profile.Labels.ToDictionary(label => label.NormalizedName, StringComparer.OrdinalIgnoreCase);
                var newLabelCount = 0;

                foreach (var label in discovered)
                {
                    if (existing.TryGetValue(label.Name, out var existingLabel))
                    {
                        existingLabel.LastDiscoveredAt = discoveredAt;
                        existingLabel.UpdatedAt = discoveredAt;
                        continue;
                    }

                    var profileLabel = new ProfileLabel
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profile.Id,
                        Profile = profile,
                        Name = label.DisplayName,
                        NormalizedName = label.Name,
                        SortOrder = null,
                        IsIgnored = false,
                        FirstDiscoveredAt = discoveredAt,
                        LastDiscoveredAt = discoveredAt,
                        UpdatedAt = discoveredAt
                    };
                    dbContext.ProfileLabels.Add(profileLabel);
                    existing.Add(profileLabel.NormalizedName, profileLabel);
                    newLabelCount++;
                }

                profile.UpdatedAt = discoveredAt;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "ProfileLabelDiscoveryCompleted for {ProfileId} with {DiscoveredLabelCount} distinct label(s), {NewLabelCount} new pending label(s), {RemovedLabelCount} removed label(s), and {GitHubRequestCount} GitHub request(s).",
                    profile.Id,
                    discovered.Count,
                    newLabelCount,
                    removedLabels.Count,
                    fetch.Cache.GitHubRequestCount);

                return Results.Ok(new LabelDiscoveryResponse(
                    ProfileLabelResponses(profile.Labels),
                    newLabelCount,
                    removedLabels.Count,
                    TaskRunCacheResponse.FromSummary(fetch.Cache),
                    TaskRunQuotaResponse.FromSummary(fetch.Quota),
                    discoveredAt));
            }
        });

        group.MapPut("/profiles/{profileId:guid}/labels/{id:guid}", async (
            Guid profileId,
            Guid id,
            UpdateProfileLabelRequest request,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var label = await dbContext.ProfileLabels.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProfileId == profileId, cancellationToken);
            if (label is null)
                return Results.NotFound();
            label.IsIgnored = request.IsIgnored;
            label.SortOrder = null;
            label.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            CreateLogger(loggerFactory).LogInformation("ProfileLabelUpdated for {ProfileId}: {ProfileLabelId}, ignored {IsIgnored}; restored labels require ranking.", profileId, label.Id, label.IsIgnored);
            return Results.Ok(ProfileLabelResponse.FromEntity(label));
        });

        group.MapPut("/profiles/{profileId:guid}/labels/order", async (
            Guid profileId,
            ReorderProfileLabelsRequest request,
            AppDbContext dbContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var labels = await dbContext.ProfileLabels
                .Where(label => label.ProfileId == profileId)
                .ToListAsync(cancellationToken);
            var rankedLabels = labels.Where(label => !label.IsIgnored && label.SortOrder is not null).ToList();
            var pendingLabels = labels.Where(label => !label.IsIgnored && label.SortOrder is null).ToList();
            var submittedIds = request.LabelIds.ToList();
            var submittedIdSet = submittedIds.ToHashSet();
            var rankedIdSet = rankedLabels.Select(label => label.Id).ToHashSet();
            var pendingIdSet = pendingLabels.Select(label => label.Id).ToHashSet();
            var activatedIds = submittedIds.Where(id => !rankedIdSet.Contains(id)).ToList();
            if (submittedIds.Count != submittedIdSet.Count
                || rankedLabels.Select(label => label.Id).Except(submittedIdSet).Any()
                || submittedIds.Any(id => !rankedIdSet.Contains(id) && !pendingIdSet.Contains(id))
                || activatedIds.Count > 1
                || submittedIds.Count != rankedLabels.Count + activatedIds.Count)
            {
                return ValidationProblem([new ValidationIssue("labelIds", "The reorder request must contain every ranked label once and may add one pending label.")]);
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var label in rankedLabels)
            {
                label.SortOrder = null;
                label.UpdatedAt = now;
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            for (var index = 0; index < submittedIds.Count; index++)
            {
                var label = labels.Single(candidate => candidate.Id == submittedIds[index]);
                label.SortOrder = index;
                label.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            CreateLogger(loggerFactory).LogInformation("ProfileLabelOrderUpdated for {ProfileId} with {ProfileLabelCount} ranked label(s) and {ActivatedLabelCount} newly ranked label(s).", profileId, submittedIds.Count, activatedIds.Count);
            return Results.Ok(ProfileLabelResponses(labels));
        });
    }

    private static IReadOnlyList<ProfileLabelResponse> ProfileLabelResponses(IEnumerable<ProfileLabel> labels) =>
        labels
            .OrderBy(label => label.IsIgnored)
            .ThenBy(label => label.SortOrder is null)
            .ThenBy(label => label.SortOrder)
            .Select(ProfileLabelResponse.FromEntity)
            .ToList();

    private static async Task<RepositoryTier?> ResolveTierAsync(Guid? repositoryTierId, AppDbContext dbContext, CancellationToken cancellationToken) =>
        repositoryTierId is { } id
            ? await dbContext.RepositoryTiers.FirstOrDefaultAsync(tier => tier.Id == id, cancellationToken)
            : await dbContext.RepositoryTiers.FirstOrDefaultAsync(tier => tier.IsDefault, cancellationToken);

    private static async Task NormalizeRepositorySortOrderAsync(Guid profileId, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var repositories = await dbContext.ProfileRepositories.Where(repository => repository.ProfileId == profileId).OrderBy(repository => repository.SortOrder).ToListAsync(cancellationToken);
        await ApplyRepositoryOrderAsync(repositories, repositories.Select(repository => repository.Id).ToList(), dbContext, cancellationToken);
    }

    private static async Task ApplyRepositoryOrderAsync(
        IReadOnlyList<ProfileRepository> repositories,
        IReadOnlyList<Guid> orderedRepositoryIds,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (repositories.Count == 0)
            return;

        var temporaryOffset = repositories.Max(repository => repository.SortOrder) + repositories.Count + 1;
        foreach (var repository in repositories)
            repository.SortOrder += temporaryOffset;
        await dbContext.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < orderedRepositoryIds.Count; index++)
            repositories.Single(repository => repository.Id == orderedRepositoryIds[index]).SortOrder = index;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static (string Owner, string Name)? NormalizeRepository(string owner, string name)
    {
        var normalizedOwner = owner?.Trim().ToLowerInvariant();
        var normalizedName = name?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalizedOwner) || string.IsNullOrWhiteSpace(normalizedName)
               || normalizedOwner.Contains('/') || normalizedName.Contains('/')
            ? null
            : (normalizedOwner, normalizedName);
    }

    private static string? NormalizeTierName(string? name)
    {
        var normalizedName = name?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 80 ? null : normalizedName;
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

    private static IReadOnlyList<ValidationIssue> ValidateProfileRequest(SaveProfileRequest request)
    {
        var errors = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(new ValidationIssue("name", "Profile name is required."));

        if (request.TaskLimit <= 0)
            errors.Add(new ValidationIssue("taskLimit", "Task limit must be greater than 0."));

        if (request.DelayInMilliseconds < 0)
            errors.Add(new ValidationIssue("delayInMilliseconds", "Delay must be 0 or greater."));

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
