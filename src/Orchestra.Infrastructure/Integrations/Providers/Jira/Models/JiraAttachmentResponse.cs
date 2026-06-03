using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models;

public class JiraAttachmentResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("mediaApiFileId")]
    public string? MediaApiFileId { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
