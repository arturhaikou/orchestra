using System;
using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

public class GitLabMergeRequest
{
    [JsonPropertyName("iid")]
    public int Iid { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("merged_at")]
    public DateTime? MergedAt { get; set; }

    [JsonPropertyName("web_url")]
    public string WebUrl { get; set; } = string.Empty;

    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; set; } = string.Empty;

    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; set; } = string.Empty;

    /// <summary>
    /// Computed boolean indicating whether this merge request has been merged.
    /// Returns true if MergedAt has a value; false otherwise.
    /// </summary>
    public bool Merged => MergedAt.HasValue;
}
