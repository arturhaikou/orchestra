using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

public class GitHubFileContent
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = string.Empty;
}
