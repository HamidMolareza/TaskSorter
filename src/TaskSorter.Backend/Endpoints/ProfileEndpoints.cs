using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Data;
using TaskSorter.Backend.Profiles;
using TaskSorter.Backend.Security;
using TaskSorter.Core.Configuration;
using TaskSorter.Core.Tasks;

namespace TaskSorter.Backend.Endpoints;

public static class ProfileEndpoints
{
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
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request, parser);
            if (validation is not null)
                return validation;

            var now = DateTimeOffset.UtcNow;
            var profile = new TaskProfile
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                RepositoryLines = request.RepositoryLines,
                LabelLines = request.LabelLines,
                TaskLimit = request.TaskLimit,
                DelayInMilliseconds = request.DelayInMilliseconds,
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
                return ValidationProblem([new ValidationIssue("name", "Profile name must be unique.")]);
            }

            return Results.Created($"/api/profiles/{profile.Id}", ProfileDetailResponse.FromEntity(profile));
        });

        group.MapPut("/profiles/{id:guid}", async (
            Guid id,
            SaveProfileRequest request,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            ProfileConfigurationParser parser,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request, parser);
            if (validation is not null)
                return validation;

            var profile = await dbContext.TaskProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
                return Results.NotFound();

            profile.Name = request.Name.Trim();
            profile.RepositoryLines = request.RepositoryLines;
            profile.LabelLines = request.LabelLines;
            profile.TaskLimit = request.TaskLimit;
            profile.DelayInMilliseconds = request.DelayInMilliseconds;
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.GitHubToken))
                profile.EncryptedGitHubToken = secretProtector.Protect(request.GitHubToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ValidationProblem([new ValidationIssue("name", "Profile name must be unique.")]);
            }

            return Results.Ok(ProfileDetailResponse.FromEntity(profile));
        });

        group.MapDelete("/profiles/{id:guid}", async (
            Guid id,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var profile = await dbContext.TaskProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
                return Results.NotFound();

            dbContext.TaskProfiles.Remove(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/profiles/{id:guid}/run", async (
            Guid id,
            AppDbContext dbContext,
            ISecretProtector secretProtector,
            TaskRunner taskRunner,
            CancellationToken cancellationToken) =>
        {
            var profile = await dbContext.TaskProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
            if (profile is null)
                return Results.NotFound();

            try
            {
                var token = secretProtector.Unprotect(profile.EncryptedGitHubToken);
                var result = await taskRunner.RunAsync(profile.ToConfiguration(), token, cancellationToken);
                return Results.Ok(TaskRunResponse.FromResult(result));
            }
            catch (ProfileConfigurationException ex)
            {
                return ValidationProblem(ex.Errors);
            }
        });

        group.MapPost("/preview-config", (PreviewConfigRequest request, ProfileConfigurationParser parser) =>
        {
            var preview = parser.Preview(request.ToConfiguration());
            return Results.Ok(ConfigPreviewResponse.FromPreview(preview));
        });

        return group;
    }

    private static IResult? ValidateRequest(SaveProfileRequest request, ProfileConfigurationParser parser)
    {
        var errors = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(new ValidationIssue("name", "Profile name is required."));

        var preview = parser.Preview(request.ToConfiguration());
        errors.AddRange(preview.Errors);

        return errors.Count == 0 ? null : ValidationProblem(errors);
    }

    private static IResult ValidationProblem(IReadOnlyList<ValidationIssue> errors) =>
        Results.ValidationProblem(errors
            .GroupBy(error => error.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray()));
}
