using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubReviewComment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("user")]
    public GitHubUser? User { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
