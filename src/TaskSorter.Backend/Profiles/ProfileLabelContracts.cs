using TaskSorter.Core.GitHub;

namespace TaskSorter.Backend.Profiles;

public sealed record ProfileLabelResponse(
    Guid Id,
    string Name,
    bool IsIgnored,
    bool IsPending,
    DateTimeOffset FirstDiscoveredAt,
    DateTimeOffset? LastDiscoveredAt)
{
    public static ProfileLabelResponse FromEntity(ProfileLabel label) => new(
        label.Id,
        label.Name,
        label.IsIgnored,
        label.IsPending,
        label.FirstDiscoveredAt,
        label.LastDiscoveredAt);
}

public sealed record UpdateProfileLabelRequest(bool IsIgnored);
public sealed record ReorderProfileLabelsRequest(IReadOnlyList<Guid> LabelIds);

public sealed record LabelDiscoveryResponse(
    IReadOnlyList<ProfileLabelResponse> Labels,
    int NewLabelCount,
    int RemovedLabelCount,
    TaskRunCacheResponse Cache,
    TaskRunQuotaResponse Quota,
    DateTimeOffset DiscoveredAt);
