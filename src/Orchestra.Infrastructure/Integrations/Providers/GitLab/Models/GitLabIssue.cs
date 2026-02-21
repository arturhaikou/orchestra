using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

public class GitLabIssue
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Project-scoped issue number (used as ExternalTicketId).
    /// </summary>
    [JsonPropertyName("iid")]
    public int Iid { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("web_url")]
    public string WebUrl { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public GitLabUser? Author { get; set; }

    /// <summary>
    /// Labels are plain strings in GitLab (not objects).
    /// </summary>
    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
