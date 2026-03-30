namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// Returned by pass 1 of the LLM analyzer when it needs full file content
/// for files not included in the original diff context.
/// </summary>
public record AdditionalContextRequest
{
    public required List<FileContextRequest> Files { get; init; }
}

public record FileContextRequest
{
    public required string Path { get; init; }
    public required string Reason { get; init; }
}
