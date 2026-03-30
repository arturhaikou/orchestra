using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

public class GitLabApproval
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("web_url")]
    public string WebUrl { get; set; } = string.Empty;
}
