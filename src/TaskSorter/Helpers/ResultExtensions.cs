using System.Text;
using OnRails.ResultDetails;

namespace TaskSorter.Helpers;

public static class ResultExtensions {
    public static string ToStr(this ResultDetail detail) {
        var sb = new StringBuilder();
        if (detail.StatusCode is not null)
            sb.Append($"{detail.StatusCode} - ");
        sb.AppendLine($"{detail.Title}: {detail.Message}");

        if (detail.MoreDetails is not null) {
            sb.AppendLine("More Details: ");
            foreach (var moreDetail in detail.MoreDetails)
                sb.AppendLine(moreDetail.ToString());
        }

        return sb.ToString();
    }
}