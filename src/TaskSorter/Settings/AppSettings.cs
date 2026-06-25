using System.Reflection;
using System.Text;

namespace TaskSorter.Settings;

public class AppSettings {
    public const string AppName = "TaskSorter";
    public const string DefaultGithubTokenEnvName = "GITHUB_TOKEN";
    public string RepositoryFile { get; set; } = default!;
    public string LabelsFile { get; set; } = default!;
    [SecureConfig] public string GithubToken { get; set; } = default!;
    public string? GithubTokenEnvName { get; set; }
    public List<string> OutputTypes { get; set; } = [];
    public string OutputDir { get; set; } = string.Empty;
    public int DelayInMilliSeconds { get; set; }
    public int TaskLimit { get; set; } = 10;

    public override string ToString() {
        var sb = new StringBuilder();
        var properties = GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties) {
            // Skip properties with the SecureConfig attribute
            if (property.GetCustomAttribute<SecureConfigAttribute>() != null)
                continue;

            var value = property.GetValue(this);
            var formattedValue = value switch {
                null => "null",
                IEnumerable<string> list => string.Join(", ", list),
                _ => value.ToString()
            };

            sb.AppendLine($"{property.Name}: {formattedValue}");
        }

        return sb.ToString();
    }
}
