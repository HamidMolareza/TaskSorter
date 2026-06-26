using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskSorter.Backend.Profiles;
using TaskSorter.Core.Configuration;

namespace TaskSorter.Tests.Backend;

public sealed class ProfileApiTests : IClassFixture<TaskSorterWebApplicationFactory>
{
    private const string CorrelationHeaderName = "X-Correlation-ID";

    private readonly HttpClient _client;
    private readonly TaskSorterWebApplicationFactory _factory;

    public ProfileApiTests(TaskSorterWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProfiles_ReturnsSeededDefaultProfile()
    {
        var profiles = await _client.GetFromJsonAsync<List<ProfileSummaryResponse>>("/api/profiles");

        Assert.NotNull(profiles);
        Assert.Contains(profiles, profile => profile.Name == "Default");
    }

    [Fact]
    public async Task CreateProfile_DoesNotReturnGitHubToken()
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Secret profile", "ghp_test"));

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        Assert.NotNull(profile);
        Assert.True(profile.HasGitHubToken);
        Assert.Equal(20, profile.PriorityFactors.AssignmentBonus);
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ghp_test", responseText);
    }

    [Fact]
    public async Task CreateProfile_ReturnsCorrelationHeaderAndDoesNotLogGitHubToken()
    {
        const string token = "ghp_log_test_secret";

        var response = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Logged profile", token));

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains(CorrelationHeaderName));

        var logs = await _factory.ReadLogTextAsync("ProfileCreated");
        Assert.Contains("ProfileCreated", logs);
        Assert.DoesNotContain(token, logs);
    }

    [Fact]
    public async Task CreateProfile_GivenInvalidRepository_ReturnsValidationProblem()
    {
        var request = CreateProfileRequest("Invalid profile", "ghp_test") with
        {
            RepositoryLines = "invalid"
        };

        var response = await _client.PostAsJsonAsync("/api/profiles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RunProfile_GivenSavedToken_ReturnsRankedTasks()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Runnable profile", "ghp_test"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run", content: null);

        runResponse.EnsureSuccessStatusCode();
        var result = await runResponse.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].Rank);
        Assert.True(result.Items[0].Score > 0);
        Assert.Equal("github", result.Cache.Status);
        Assert.True(result.Cache.Enabled);
        Assert.False(result.Cache.RefreshRequested);
        Assert.Equal(1, result.Cache.GitHubRequestCount);
        Assert.Single(result.Cache.Operations);
        Assert.Equal("ok", result.Quota.Status);
        Assert.True(result.Quota.ProtectionEnabled);
        Assert.Equal(4900, result.Quota.Remaining);
        Assert.Equal(1, result.Quota.ActualGitHubRequestCount);
    }

    [Fact]
    public async Task RunProfile_WhenRefreshRequested_PropagatesRefreshFlag()
    {
        _factory.GitHubTaskClient.ClearRequests();
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Refresh profile", "ghp_test"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run?refresh=true", content: null);

        runResponse.EnsureSuccessStatusCode();
        var result = await runResponse.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.Contains(true, _factory.GitHubTaskClient.RefreshRequests);
        Assert.NotNull(result);
        Assert.Equal("refreshed", result.Cache.Status);
        Assert.True(result.Cache.RefreshRequested);
        Assert.Equal("refresh", result.Cache.Operations[0].Source);
    }

    [Fact]
    public async Task RunProfile_WhenQuotaOverrideRequested_PropagatesOverrideFlag()
    {
        _factory.GitHubTaskClient.ClearRequests();
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Quota override profile", "ghp_test"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run?refresh=true&quotaOverride=true", content: null);

        runResponse.EnsureSuccessStatusCode();
        Assert.Contains(true, _factory.GitHubTaskClient.RefreshRequests);
        Assert.Contains(true, _factory.GitHubTaskClient.QuotaOverrideRequests);
    }

    [Fact]
    public async Task RunProfile_WhenQuotaProtected_ReturnsTooManyRequestsProblem()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Quota protected profile", "ghp_quota_protected"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run", content: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, runResponse.StatusCode);
        Assert.Equal("application/problem+json", runResponse.Content.Headers.ContentType?.MediaType);
        Assert.True(runResponse.Headers.Contains("Retry-After"));
        var responseText = await runResponse.Content.ReadAsStringAsync();
        Assert.Contains("GitHub quota protection stopped the run", responseText);
        Assert.Contains("\"status\":\"protected\"", responseText);
        Assert.DoesNotContain("ghp_quota_protected", responseText);
    }

    [Fact]
    public async Task RunProfile_GivenCurrentRequestBody_ReranksWithRequestedTaskLimit()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Current draft profile", "ghp_many"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();
        var runRequest = new RunProfileRequest(
            profile!.RepositoryLines,
            profile.LabelLines,
            TaskLimit: 15,
            DelayInMilliseconds: profile.DelayInMilliseconds,
            PriorityFactors: profile.PriorityFactors);

        var runResponse = await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/run", runRequest);

        runResponse.EnsureSuccessStatusCode();
        var result = await runResponse.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(result);
        Assert.Equal(15, result.Items.Count);
        Assert.Equal(15, result.Items[^1].Rank);
    }

    [Fact]
    public async Task RunProfileStream_GivenCurrentRequestBody_StreamsProgressAndCompletedResult()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Streaming profile", "ghp_many"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();
        var runRequest = new RunProfileRequest(
            profile!.RepositoryLines,
            profile.LabelLines,
            TaskLimit: 15,
            DelayInMilliseconds: profile.DelayInMilliseconds,
            PriorityFactors: profile.PriorityFactors);

        var runResponse = await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/run/stream", runRequest);

        runResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/x-ndjson", runResponse.Content.Headers.ContentType?.MediaType);
        var events = (await runResponse.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<TaskRunStreamEvent>(line, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }))
            .ToList();

        Assert.Contains(events, streamEvent => streamEvent?.Type == "progress");
        var completed = Assert.Single(events, streamEvent => streamEvent?.Type == "completed");
        Assert.NotNull(completed!.Result);
        Assert.Equal(15, completed.Result.Items.Count);
        Assert.Equal("ok", completed.Result.Quota.Status);
        Assert.Equal("ok", completed.Quota?.Status);
    }

    [Fact]
    public async Task RunProfile_WhenGitHubClientFails_ReturnsProblemAndWritesErrorLog()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Failing profile", "ghp_fail"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run", content: null);

        Assert.Equal(HttpStatusCode.InternalServerError, runResponse.StatusCode);
        Assert.True(runResponse.Headers.Contains(CorrelationHeaderName));
        var responseText = await runResponse.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", responseText);

        var logs = await _factory.ReadLogTextAsync("ProfileRunFailed");
        Assert.Contains("ProfileRunFailed", logs);
        Assert.Contains(profile.Id.ToString(), logs);
        Assert.DoesNotContain("ghp_fail", logs);
    }

    [Fact]
    public async Task RunProfile_WhenProfileRunTimesOut_ReturnsGatewayTimeoutAndWritesWarningLog()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("Slow profile", "ghp_slow"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run", content: null);

        Assert.Equal(HttpStatusCode.GatewayTimeout, runResponse.StatusCode);
        Assert.Equal("application/problem+json", runResponse.Content.Headers.ContentType?.MediaType);
        Assert.True(runResponse.Headers.Contains(CorrelationHeaderName));
        var responseText = await runResponse.Content.ReadAsStringAsync();
        Assert.Contains("Profile run timed out", responseText);
        Assert.Contains("correlationId", responseText);

        var logs = await _factory.ReadLogTextAsync("ProfileRunTimedOut");
        Assert.Contains("ProfileRunTimedOut", logs);
        Assert.Contains(profile.Id.ToString(), logs);
        Assert.DoesNotContain("ghp_slow", logs);
    }

    [Fact]
    public async Task RunProfile_WhenGitHubRequestTimesOut_ReturnsGatewayTimeoutAndWritesWarningLog()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest("GitHub timeout profile", "ghp_github_timeout"));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run", content: null);

        Assert.Equal(HttpStatusCode.GatewayTimeout, runResponse.StatusCode);
        Assert.Equal("application/problem+json", runResponse.Content.Headers.ContentType?.MediaType);
        var responseText = await runResponse.Content.ReadAsStringAsync();
        Assert.Contains("GitHub request timed out", responseText);
        Assert.Contains("correlationId", responseText);

        var logs = await _factory.ReadLogTextAsync("ProfileRunGitHubRequestTimedOut");
        Assert.Contains("ProfileRunGitHubRequestTimedOut", logs);
        Assert.Contains("synthetic-github-request", logs);
        Assert.Contains(profile.Id.ToString(), logs);
        Assert.DoesNotContain("ghp_github_timeout", logs);
    }

    [Fact]
    public async Task RunProfile_GivenCustomPriorityFactors_UsesConfiguredWeights()
    {
        var customFactors = new TaskPriorityFactors
        {
            AssignmentBonus = 123
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/profiles",
            CreateProfileRequest("Weighted profile", "ghp_test", customFactors));
        createResponse.EnsureSuccessStatusCode();
        var profile = await createResponse.Content.ReadFromJsonAsync<ProfileDetailResponse>();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile!.Id}/run", content: null);

        runResponse.EnsureSuccessStatusCode();
        var result = await runResponse.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(result);
        Assert.Equal(123, result.Items[0].ScoreBreakdown.Assignment);
    }

    private static SaveProfileRequest CreateProfileRequest(string name, string? token, TaskPriorityFactors? factors = null) => new(
        name,
        "owner/repo core",
        """
        priority/high
        status/next
        size/s
        """,
        10,
        0,
        token,
        factors);
}
