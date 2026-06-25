using System.Net;
using System.Net.Http.Json;
using TaskSorter.Backend.Profiles;

namespace TaskSorter.Tests.Backend;

public sealed class ProfileApiTests : IClassFixture<TaskSorterWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProfileApiTests(TaskSorterWebApplicationFactory factory)
    {
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
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ghp_test", responseText);
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
    }

    private static SaveProfileRequest CreateProfileRequest(string name, string? token) => new(
        name,
        "owner/repo core",
        """
        priority/high
        status/next
        size/s
        """,
        10,
        0,
        token);
}
