using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubSearchResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public List<GitHubIssue> Items { get; set; } = new();
}
