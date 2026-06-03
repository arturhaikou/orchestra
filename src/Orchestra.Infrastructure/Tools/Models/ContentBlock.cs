using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Tools.Models;

public record ContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("fileName")] string? FileName = null
);
