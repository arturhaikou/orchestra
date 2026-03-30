using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubPullRequestFile
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    [JsonPropertyName("changes")]
    public int Changes { get; set; }

    [JsonPropertyName("patch")]
    public string? Patch { get; set; }
}
