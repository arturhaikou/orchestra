using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubCreatedPullRequest
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("head")]
    public GitHubCreatedPullRequestBranch? Head { get; set; }

    [JsonPropertyName("base")]
    public GitHubCreatedPullRequestBranch? Base { get; set; }
}

public class GitHubCreatedPullRequestBranch
{
    [JsonPropertyName("ref")]
    public string? Ref { get; set; }
}
