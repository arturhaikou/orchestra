using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

/// <summary>
/// Represents the note object nested inside a <see cref="GitLabDiscussion"/> response.
/// </summary>
public class GitLabDiscussionNote
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

/// <summary>
/// Deserializes the response body returned by:
/// POST /api/v4/projects/:id/merge_requests/:iid/discussions
/// </summary>
public class GitLabDiscussion
{
    /// <summary>The SHA-based string ID of the discussion thread.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Notes in this discussion. The first element is the root note created
    /// by the POST request.
    /// </summary>
    [JsonPropertyName("notes")]
    public List<GitLabDiscussionNote> Notes { get; set; } = new();
}
