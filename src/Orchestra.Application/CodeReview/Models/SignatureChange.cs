namespace Orchestra.Application.CodeReview.Models;

public record SignatureChange
{
    public required string FilePath { get; init; }
    public required string MethodName { get; init; }
    public required string ChangeDescription { get; init; }
}
