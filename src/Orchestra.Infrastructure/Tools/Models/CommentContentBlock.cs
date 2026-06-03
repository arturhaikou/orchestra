namespace Orchestra.Infrastructure.Tools.Models;

/// <summary>
/// Retained for backwards compatibility. Use <see cref="ContentBlock"/> instead.
/// </summary>
[System.Obsolete("Use ContentBlock instead.")]
public record CommentContentBlock(
    string Type,
    string Content,
    string? FileName = null
) : ContentBlock(Type, Content, FileName);
