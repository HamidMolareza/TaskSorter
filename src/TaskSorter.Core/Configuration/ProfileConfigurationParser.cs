using TaskSorter.Core.Models;

namespace TaskSorter.Core.Configuration;

public sealed class ProfileConfigurationParser
{
    public ConfigPreview Preview(ProfileConfiguration configuration)
    {
        var warnings = new List<string>();
        var errors = new List<ValidationIssue>();

        var repositoryLines = CleanLines(configuration.RepositoryLines)
            .Where(line => !line.StartsWith('#'))
            .ToList();
        var labelLines = CleanLines(configuration.LabelLines)
            .Where(line => !line.StartsWith('#'))
            .ToList();

        if (configuration.TaskLimit <= 0)
            errors.Add(new ValidationIssue("taskLimit", "Task limit must be greater than 0."));

        if (configuration.DelayInMilliseconds < 0)
            errors.Add(new ValidationIssue("delayInMilliseconds", "Delay must be 0 or greater."));

        if (repositoryLines.Count == 0)
            errors.Add(new ValidationIssue("repositoryLines", "At least one repository is required."));

        if (labelLines.Count == 0)
            errors.Add(new ValidationIssue("labelLines", "At least one label is required."));

        var repositories = new List<Repository>();
        var maximumRepositoryValue = repositoryLines.Count + 1;
        for (var i = 0; i < repositoryLines.Count; i++)
        {
            var repository = Repository.Parse(repositoryLines[i], maximumRepositoryValue - i);
            if (repository is null)
            {
                errors.Add(new ValidationIssue(
                    $"repositoryLines[{i}]",
                    $"Repository line is invalid. Use 'owner/repo' or 'owner/repo tier'. Valid tiers: {ProjectTier.ValidNames}."));
                continue;
            }

            repositories.Add(repository);
        }

        var duplicateRepositories = repositories
            .GroupBy(repository => repository.ToString(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateRepositories.Count > 0)
            warnings.Add($"Duplicate repositories ignored by GitHub task de-duplication: {string.Join(", ", duplicateRepositories)}.");

        var maximumLabelValue = labelLines.Count + 1;
        var labels = labelLines
            .Select((line, index) => new Label(line, maximumLabelValue - index))
            .ToList();

        return new ConfigPreview(repositories, labels, warnings, errors);
    }

    public ParsedProfileConfiguration Parse(ProfileConfiguration configuration)
    {
        var preview = Preview(configuration);
        if (!preview.IsValid)
            throw new ProfileConfigurationException(preview.Errors);

        return new ParsedProfileConfiguration(
            preview.Repositories,
            preview.Labels,
            configuration.TaskLimit,
            configuration.DelayInMilliseconds,
            configuration.EffectivePriorityFactors,
            preview.Warnings);
    }

    private static IEnumerable<string> CleanLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }
}
