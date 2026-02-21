using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

/// <summary>
/// GitLab's equivalent of a comment on an issue.
/// </summary>
public class GitLabNote
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public GitLabUser? Author { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
