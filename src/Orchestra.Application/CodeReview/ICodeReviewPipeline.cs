using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.CodeReview;

/// <summary>
/// Deterministic code review pipeline. Orchestrates data fetching, LLM analysis,
/// and review submission without giving the LLM direct API access.
/// </summary>
public interface ICodeReviewPipeline
{
    Task<ReviewToolResult> ExecuteAsync(
        ReviewRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Input to the code review pipeline.
/// </summary>
public record ReviewRequest
{
    public required Guid WorkspaceId { get; init; }
    public required string IntegrationId { get; init; }
    public required string PrOrMrNumber { get; init; }
    public required ProviderType ProviderType { get; init; }
    public string? ModelIdentifier { get; init; }
    public string? ProjectPrinciples { get; init; }
}
