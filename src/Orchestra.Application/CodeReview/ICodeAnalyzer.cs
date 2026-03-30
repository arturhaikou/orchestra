using Orchestra.Application.CodeReview.Models;

namespace Orchestra.Application.CodeReview;

/// <summary>
/// Narrows the LLM scope to code analysis only. No tool calls, no multi-turn.
/// Uses structured I/O via <c>IChatClient.GetResponseAsync&lt;T&gt;</c>.
/// </summary>
public interface ICodeAnalyzer
{
    /// <summary>
    /// Pass 1: Analyze diffs. May return findings OR a request for more context.
    /// </summary>
    Task<CodeAnalysisOutcome> AnalyzeAsync(
        NormalizedDiffContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Pass 2: Re-analyze with additional file context. Always returns final findings.
    /// </summary>
    Task<AnalysisResult> AnalyzeWithContextAsync(
        NormalizedDiffContext context,
        Dictionary<string, string> additionalFiles,
        CancellationToken cancellationToken);
}

/// <summary>
/// Discriminated outcome of the first analysis pass.
/// </summary>
public record CodeAnalysisOutcome
{
    public AnalysisResult? Result { get; init; }
    public AdditionalContextRequest? ContextRequest { get; init; }

    public bool NeedsMoreContext => ContextRequest != null;

    public static CodeAnalysisOutcome FromResult(AnalysisResult result)
        => new() { Result = result };

    public static CodeAnalysisOutcome FromContextRequest(AdditionalContextRequest request)
        => new() { ContextRequest = request };
}
