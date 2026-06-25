namespace TaskSorter.Core.Configuration;

public sealed class ProfileConfigurationException(IReadOnlyList<ValidationIssue> errors)
    : Exception("Profile configuration is invalid.")
{
    public IReadOnlyList<ValidationIssue> Errors { get; } = errors;
}
