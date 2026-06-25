using Octokit;

namespace TaskSorter.Helpers;

public static class GitHubHelpers {
    public static string ToStr(this RateLimit rateLimit) {
        return $"""
                Core API Rate Limit:
                  Limit: {rateLimit.Limit}
                  Remaining: {rateLimit.Remaining}
                  Reset: {rateLimit.Reset.ToLocalTime()}
                """;
    }
}