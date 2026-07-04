using System.Net;
using System.Net.Http.Json;
using TaskSorter.Backend.Profiles;
using TaskSorter.Core.Configuration;

namespace TaskSorter.Tests.Backend;

public sealed class ProfileApiTests(TaskSorterWebApplicationFactory factory) : IClassFixture<TaskSorterWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetRepositoryTiers_ReturnsSeededDefaultTier()
    {
        var tiers = await _client.GetFromJsonAsync<List<RepositoryTierResponse>>("/api/repository-tiers");

        Assert.NotNull(tiers);
        Assert.Contains(tiers, tier => tier.Name == "active" && tier.IsDefault);
    }

    [Fact]
    public async Task CreateProfile_AllowsIncompleteDraft()
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest(UniqueName("Draft"), null));

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileDetailResponse>();
        Assert.NotNull(profile);
        Assert.Empty(profile.Repositories);
    }

    [Fact]
    public async Task CreateProfile_DoesNotReturnGitHubToken()
    {
        const string token = "ghp_test_secret";
        var response = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest(UniqueName("Secret"), token));

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileDetailResponse>();
        Assert.NotNull(profile);
        Assert.True(profile.HasGitHubToken);
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AddRepository_RejectsDuplicatesInTheSameProfile()
    {
        var profile = await CreateProfileAsync("Duplicates", null);
        var defaultTier = await GetDefaultTierAsync();
        var request = new SaveProfileRepositoryRequest("owner", "repo", defaultTier.Id);

        (await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/repositories", request)).EnsureSuccessStatusCode();
        var duplicate = await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/repositories", request);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already configured", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RepositoryTierNames_AreUniqueIgnoringCase()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var first = await _client.PostAsJsonAsync("/api/repository-tiers", new SaveRepositoryTierRequest($"Focus-{suffix}", 700));
        first.EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsJsonAsync("/api/repository-tiers", new SaveRepositoryTierRequest($"focus-{suffix}", 701));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task SetDefaultRepositoryTier_SwitchesTheSingleDefaultTier()
    {
        var originalDefault = await GetDefaultTierAsync();
        var response = await _client.PostAsJsonAsync("/api/repository-tiers", new SaveRepositoryTierRequest(UniqueName("Default switch"), 720));
        var tier = await response.Content.ReadFromJsonAsync<RepositoryTierResponse>();
        Assert.NotNull(tier);

        var setDefault = await _client.PutAsync($"/api/repository-tiers/{tier.Id}/default", null);

        setDefault.EnsureSuccessStatusCode();
        var tiers = await _client.GetFromJsonAsync<List<RepositoryTierResponse>>("/api/repository-tiers");
        Assert.NotNull(tiers);
        Assert.Equal(tier.Id, Assert.Single(tiers, candidate => candidate.IsDefault).Id);
        Assert.False(tiers.Single(candidate => candidate.Id == originalDefault.Id).IsDefault);
    }

    [Fact]
    public async Task DeleteAssignedTier_RequiresConfirmationThenReassignsToDefault()
    {
        var profile = await CreateProfileAsync("Tier reassignment", null);
        var tierResponse = await _client.PostAsJsonAsync("/api/repository-tiers", new SaveRepositoryTierRequest(UniqueName("Temporary tier"), 710));
        var tier = await tierResponse.Content.ReadFromJsonAsync<RepositoryTierResponse>();
        Assert.NotNull(tier);
        (await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/repositories", new SaveProfileRepositoryRequest("owner", "tier-reassign", tier.Id))).EnsureSuccessStatusCode();

        var blocked = await _client.DeleteAsync($"/api/repository-tiers/{tier.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var confirmed = await _client.DeleteAsync($"/api/repository-tiers/{tier.Id}?reassignAssignedRepositories=true");
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        var refreshed = await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}");
        Assert.NotNull(refreshed);
        Assert.Equal((await GetDefaultTierAsync()).Id, Assert.Single(refreshed.Repositories).RepositoryTierId);
    }

    [Fact]
    public async Task RunProfile_UsesPersistedRepositoriesAndCurrentTaskLimit()
    {
        var profile = await CreateRunnableProfileAsync("Run profile", "ghp_many");
        var request = new RunProfileRequest(TaskLimit: 15);

        var response = await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/run", request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(result);
        Assert.Equal(15, result.Items.Count);
    }

    [Fact]
    public async Task RunProfile_RequiresRepositoryAndLabelsBeforeGitHubCall()
    {
        var profile = await CreateProfileAsync("Invalid run", "ghp_test");

        var response = await _client.PostAsync($"/api/profiles/{profile.Id}/run", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RunProfile_UsesSavedTuning()
    {
        var profile = await CreateRunnableProfileAsync("Tuning", "ghp_test", new TaskPriorityFactors { AssignmentBonus = 123 });
        var response = await _client.PostAsync($"/api/profiles/{profile.Id}/run", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(result);
        Assert.Equal(123, result.Items[0].ScoreBreakdown.Assignment);
    }

    [Fact]
    public async Task DiscoverLabels_AddsPendingLabelsAndUsesRepositoryTasks()
    {
        var profile = await CreateRunnableProfileAsync("Label discovery", "ghp_test");

        var response = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LabelDiscoveryResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result.NewLabelCount);
        Assert.Equal(0, result.RemovedLabelCount);
        Assert.All(result.Labels, label => Assert.True(label.IsPending));
        Assert.Equal("github", result.Cache.Status);
        Assert.Equal(1, result.Cache.GitHubRequestCount);
    }

    [Fact]
    public async Task DiscoverLabels_RemovesLabelsThatAreNoLongerInRepositoryIssues()
    {
        var profile = await CreateRunnableProfileAsync("Label reconciliation", "ghp_test");
        var firstDiscovery = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover", null);
        var firstResult = await firstDiscovery.Content.ReadFromJsonAsync<LabelDiscoveryResponse>();
        Assert.NotNull(firstResult);
        var size = firstResult.Labels.Single(label => label.Name == "size/s");
        (await _client.PutAsJsonAsync($"/api/profiles/{profile.Id}/labels/{size.Id}", new UpdateProfileLabelRequest(true))).EnsureSuccessStatusCode();

        var update = new SaveProfileRequest(
            profile.Name,
            profile.LabelLines,
            profile.TaskLimit,
            profile.DelayInMilliseconds,
            "ghp_label_subset",
            profile.PriorityFactors);
        (await _client.PutAsJsonAsync($"/api/profiles/{profile.Id}", update)).EnsureSuccessStatusCode();

        var response = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover?refresh=true", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LabelDiscoveryResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.NewLabelCount);
        Assert.Equal(2, result.RemovedLabelCount);
        Assert.Equal(["priority/high"], result.Labels.Select(label => label.Name));

        var refreshed = await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}");
        Assert.NotNull(refreshed);
        Assert.Equal(["priority/high"], refreshed.Labels.Select(label => label.Name));
    }

    [Fact]
    public async Task DiscoverLabels_WithNoRepositories_RemovesExistingLabelsWithoutGitHubRequest()
    {
        var profile = await CreateRunnableProfileAsync("Label cleanup", "ghp_test");
        var firstDiscovery = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover", null);
        firstDiscovery.EnsureSuccessStatusCode();
        Assert.NotEmpty((await firstDiscovery.Content.ReadFromJsonAsync<LabelDiscoveryResponse>())!.Labels);
        var repository = Assert.Single((await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}"))!.Repositories);
        (await _client.DeleteAsync($"/api/profiles/{profile.Id}/repositories/{repository.Id}")).EnsureSuccessStatusCode();
        factory.GitHubTaskClient.ClearRequests();

        var response = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LabelDiscoveryResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Labels);
        Assert.Equal(0, result.NewLabelCount);
        Assert.Equal(3, result.RemovedLabelCount);
        Assert.Equal("disabled", result.Cache.Status);
        Assert.Equal(0, result.Cache.GitHubRequestCount);
        Assert.Equal("unknown", result.Quota.Status);
        Assert.Empty(factory.GitHubTaskClient.RefreshRequests);

        var refreshed = await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}");
        Assert.NotNull(refreshed);
        Assert.Empty(refreshed.Repositories);
        Assert.Empty(refreshed.Labels);
    }

    [Fact]
    public async Task DiscoverLabels_WithNoRepositories_DoesNotRequireGitHubToken()
    {
        var profile = await CreateProfileAsync("Label cleanup no token", null);

        var response = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover", null);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LabelDiscoveryResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Labels);
        Assert.Equal(0, result.RemovedLabelCount);
        Assert.Equal(0, result.Cache.GitHubRequestCount);
    }

    [Fact]
    public async Task LabelOrders_AreUniqueAndIgnoredLabelsAreNotRanked()
    {
        var profile = await CreateRunnableProfileAsync("Label groups", "ghp_test");
        var discoveryResponse = await _client.PostAsync($"/api/profiles/{profile.Id}/labels/discover", null);
        var discovery = await discoveryResponse.Content.ReadFromJsonAsync<LabelDiscoveryResponse>();
        Assert.NotNull(discovery);

        var high = discovery.Labels.Single(label => label.Name == "priority/high");
        var status = discovery.Labels.Single(label => label.Name == "status/next");
        var size = discovery.Labels.Single(label => label.Name == "size/s");
        var invalidActivation = await _client.PutAsJsonAsync($"/api/profiles/{profile.Id}/labels/order", new ReorderProfileLabelsRequest([high.Id, status.Id]));
        Assert.Equal(HttpStatusCode.BadRequest, invalidActivation.StatusCode);
        (await _client.PutAsJsonAsync($"/api/profiles/{profile.Id}/labels/order", new ReorderProfileLabelsRequest([high.Id]))).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/profiles/{profile.Id}/labels/order", new ReorderProfileLabelsRequest([high.Id, status.Id]))).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/profiles/{profile.Id}/labels/{size.Id}", new UpdateProfileLabelRequest(true))).EnsureSuccessStatusCode();

        var runResponse = await _client.PostAsync($"/api/profiles/{profile.Id}/run", null);
        runResponse.EnsureSuccessStatusCode();
        var run = await runResponse.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(run);
        Assert.Equal(3, run.Items[0].ScoreBreakdown.Labels);
        Assert.Contains("size/s", run.Items[0].UnscoredLabels);

        var restored = await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}");
        Assert.NotNull(restored);
        Assert.True(restored.Labels.Single(label => label.Id == size.Id).IsIgnored);
    }

    [Fact]
    public async Task RepositoryFactors_DefaultToOneAndAffectRepositoryScore()
    {
        var profile = await CreateProfileAsync("Repository factors", "ghp_test");
        var tier = await GetDefaultTierAsync();
        var factorResponse = await _client.PostAsJsonAsync(
            $"/api/profiles/{profile.Id}/repository-priority-factors",
            new SaveRepositoryPriorityFactorRequest("Urgency", "How soon this repository matters.", 8));
        factorResponse.EnsureSuccessStatusCode();
        var factor = await factorResponse.Content.ReadFromJsonAsync<RepositoryPriorityFactorResponse>();
        Assert.NotNull(factor);

        var repository = await CreateRepositoryAsync(profile.Id, "owner", "repo", tier.Id);

        Assert.Equal(8, repository.FactorScore);
        Assert.Equal(tier.Score + 8, repository.Score);
        Assert.Equal(1, Assert.Single(repository.Ratings).Rating);

        var ratingResponse = await _client.PutAsJsonAsync(
            $"/api/profiles/{profile.Id}/repositories/{repository.Id}/factor-ratings/{factor.Id}",
            new UpdateRepositoryFactorRatingRequest(5, repository.Ratings[0].RowVersion));
        ratingResponse.EnsureSuccessStatusCode();

        var refreshed = await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}");
        Assert.NotNull(refreshed);
        var scoredRepository = Assert.Single(refreshed.Repositories);
        Assert.Equal(40, scoredRepository.FactorScore);
        Assert.Equal(tier.Score + 40, scoredRepository.Score);

        var runResponse = await _client.PostAsync($"/api/profiles/{profile.Id}/run", null);
        runResponse.EnsureSuccessStatusCode();
        var run = await runResponse.Content.ReadFromJsonAsync<TaskRunResponse>();
        Assert.NotNull(run);
        Assert.Equal(tier.Score + 40, run.Items[0].ScoreBreakdown.Repository);
    }

    [Fact]
    public async Task UpdateRepository_WithInvalidCoordinates_ReturnsValidationProblem()
    {
        var profile = await CreateProfileAsync("Invalid repository update", "ghp_test");
        var tier = await GetDefaultTierAsync();
        var repository = await CreateRepositoryAsync(profile.Id, "owner", "repo", tier.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/profiles/{profile.Id}/repositories/{repository.Id}",
            new UpdateProfileRepositoryRequest("owner", "bad/name", tier.Id, repository.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Repository owner and name are required", body);

        var refreshed = await _client.GetFromJsonAsync<ProfileDetailResponse>($"/api/profiles/{profile.Id}");
        Assert.NotNull(refreshed);
        Assert.Equal("owner/repo", Assert.Single(refreshed.Repositories).FullName);
    }

    [Fact]
    public async Task UpdateRepositoryFactorRating_WithStaleRowVersion_ReturnsConflictWithLatestProfile()
    {
        var profile = await CreateProfileAsync("Rating conflict", "ghp_test");
        var tier = await GetDefaultTierAsync();
        var factorResponse = await _client.PostAsJsonAsync(
            $"/api/profiles/{profile.Id}/repository-priority-factors",
            new SaveRepositoryPriorityFactorRequest("Need", "How useful this repository is.", 5));
        factorResponse.EnsureSuccessStatusCode();
        var factor = await factorResponse.Content.ReadFromJsonAsync<RepositoryPriorityFactorResponse>();
        Assert.NotNull(factor);
        var repository = await CreateRepositoryAsync(profile.Id, "owner", "conflict", tier.Id);

        var conflict = await _client.PutAsJsonAsync(
            $"/api/profiles/{profile.Id}/repositories/{repository.Id}/factor-ratings/{factor.Id}",
            new UpdateRepositoryFactorRatingRequest(4, 999));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var body = await conflict.Content.ReadAsStringAsync();
        Assert.Contains("latestProfile", body);
        Assert.Contains("Rating changed", body);
    }

    private async Task<ProfileDetailResponse> CreateRunnableProfileAsync(string prefix, string token, TaskPriorityFactors? factors = null)
    {
        var profile = await CreateProfileAsync(prefix, token, factors);
        var tier = await GetDefaultTierAsync();
        var response = await _client.PostAsJsonAsync($"/api/profiles/{profile.Id}/repositories", new SaveProfileRepositoryRequest("owner", "repo", tier.Id));
        response.EnsureSuccessStatusCode();
        return profile;
    }

    private async Task<ProfileRepositoryResponse> CreateRepositoryAsync(Guid profileId, string owner, string name, Guid tierId)
    {
        var response = await _client.PostAsJsonAsync($"/api/profiles/{profileId}/repositories", new SaveProfileRepositoryRequest(owner, name, tierId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProfileRepositoryResponse>())!;
    }

    private async Task<ProfileDetailResponse> CreateProfileAsync(string prefix, string? token, TaskPriorityFactors? factors = null)
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", CreateProfileRequest(UniqueName(prefix), token, factors));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProfileDetailResponse>())!;
    }

    private async Task<RepositoryTierResponse> GetDefaultTierAsync() =>
        (await _client.GetFromJsonAsync<List<RepositoryTierResponse>>("/api/repository-tiers"))!.Single(tier => tier.IsDefault);

    private static SaveProfileRequest CreateProfileRequest(string name, string? token, TaskPriorityFactors? factors = null) => new(
        name,
        "priority/high\nstatus/next\nsize/s",
        10,
        0,
        token,
        factors);

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";
}
