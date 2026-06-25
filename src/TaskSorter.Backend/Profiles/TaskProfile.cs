using TaskSorter.Core.Configuration;

namespace TaskSorter.Backend.Profiles;

public sealed class TaskProfile
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string RepositoryLines { get; set; }
    public required string LabelLines { get; set; }
    public int TaskLimit { get; set; }
    public int DelayInMilliseconds { get; set; }
    public string EncryptedGitHubToken { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool HasGitHubToken => !string.IsNullOrWhiteSpace(EncryptedGitHubToken);

    public ProfileConfiguration ToConfiguration() => new(
        RepositoryLines,
        LabelLines,
        TaskLimit,
        DelayInMilliseconds);
}
