using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

public class GitLabMergeRequestChange
{
    [JsonPropertyName("old_path")]
    public string OldPath { get; set; } = string.Empty;

    [JsonPropertyName("new_path")]
    public string NewPath { get; set; } = string.Empty;

    [JsonPropertyName("diff")]
    public string Diff { get; set; } = string.Empty;

    [JsonPropertyName("new_file")]
    public bool NewFile { get; set; }

    [JsonPropertyName("deleted_file")]
    public bool DeletedFile { get; set; }

    [JsonPropertyName("renamed_file")]
    public bool RenamedFile { get; set; }
}
