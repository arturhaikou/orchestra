using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

/// <summary>
/// Represents a single inline comment posted as part of a pull request review.
/// Maps to the entries in the <c>comments</c> array of the GitHub Reviews API payload.
/// </summary>
public class GitHubInlineReviewComment
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "RIGHT";

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
