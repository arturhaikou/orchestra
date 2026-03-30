using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubReviewSubmissionResult
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
