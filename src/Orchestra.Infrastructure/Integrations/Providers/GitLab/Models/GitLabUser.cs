using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

public class GitLabUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }
}
