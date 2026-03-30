namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// Provider-agnostic diff context assembled from a pull request or merge request.
/// Passed to the LLM analyzer as structured input.
/// </summary>
public record NormalizedDiffContext
{
    public required string PrOrMrNumber { get; init; }
    public required List<NormalizedFileDiff> Files { get; init; }
    public string? ProjectPrinciples { get; init; }
}

public record NormalizedFileDiff
{
    /// <summary>File path relative to the repository root.</summary>
    public required string Path { get; init; }

    /// <summary>One of: added, modified, deleted, renamed.</summary>
    public required string Status { get; init; }

    public required int Additions { get; init; }
    public required int Deletions { get; init; }

    /// <summary>Unified diff patch.</summary>
    public required string Patch { get; init; }

    /// <summary>Full file content, populated in pass 2 when additional context is needed.</summary>
    public string? FullContent { get; set; }
}
