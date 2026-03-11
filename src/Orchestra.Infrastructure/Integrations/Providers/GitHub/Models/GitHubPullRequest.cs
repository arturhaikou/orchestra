using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubPullRequest
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("merged")]
    public bool Merged { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("head")]
    public GitHubBranch? Head { get; set; }

    [JsonPropertyName("base")]
    public GitHubBranch? Base { get; set; }

    [JsonPropertyName("mergeable")]
    public bool? Mergeable { get; set; }
}

public class GitHubBranch
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;
}
