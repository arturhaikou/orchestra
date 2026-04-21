using Microsoft.Extensions.Logging;
using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.CodeReview;

/// <summary>
/// Deterministic code review pipeline that orchestrates data fetching,
/// LLM analysis (1-2 structured calls), and review submission.
/// All I/O is deterministic; the LLM is constrained to code analysis only.
/// </summary>
public class HybridReviewPipeline : ICodeReviewPipeline
{
    private const int MaxFindings = 50;
    private const int MaxFixSuggestionLength = 2_000;

    private readonly ICodeReviewProviderFactory _providerFactory;
    private readonly ISignatureChangeDetector _signatureDetector;
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IIntegrationResolver _integrationResolver;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HybridReviewPipeline> _logger;

    public HybridReviewPipeline(
        ICodeReviewProviderFactory providerFactory,
        ISignatureChangeDetector signatureDetector,
        IChatClientResolver chatClientResolver,
        IIntegrationResolver integrationResolver,
        ILoggerFactory loggerFactory,
        ILogger<HybridReviewPipeline> logger)
    {
        _providerFactory = providerFactory;
        _signatureDetector = signatureDetector;
        _chatClientResolver = chatClientResolver;
        _integrationResolver = integrationResolver;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<ReviewToolResult> ExecuteAsync(
        ReviewRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting hybrid review pipeline: prOrMrNumber={PrMrNumber} workspaceId={WorkspaceId}",
            request.PrOrMrNumber, request.WorkspaceId);

        try
        {
            // Step 1: Resolve provider (deterministic)
            var integration = await _integrationResolver.ResolveAsync(
                request.WorkspaceId, request.IntegrationId, request.ProviderType, cancellationToken);
            var provider = _providerFactory.Create(integration);

            // Step 2: Resolve LLM client scoped to the request's workspace and model.
            // request.ModelIdentifier is set by CodeReviewOrchestrationService before building ReviewRequest.
            var chatClient = await _chatClientResolver.ResolveAsync(
                request.WorkspaceId,
                request.ModelIdentifier ?? throw new InvalidOperationException(
                    $"ReviewRequest.ModelIdentifier is required for workspace {request.WorkspaceId}."),
                cancellationToken);
            var analyzer = new StructuredCodeAnalyzer(
                chatClient, _loggerFactory.CreateLogger<StructuredCodeAnalyzer>());

            // Step 3: Fetch diff data (deterministic)
            var files = await provider.FetchChangedFilesAsync(request.PrOrMrNumber, cancellationToken);

            if (files.Count == 0)
            {
                _logger.LogInformation("No changed files found for PR/MR {PrMrNumber}", request.PrOrMrNumber);
                return new ReviewToolResult
                {
                    Success = true,
                    Verdict = "APPROVED",
                    Summary = "No changed files detected in this PR/MR.",
                    Findings = [],
                };
            }

            // Step 3: Detect signature changes (deterministic)
            var signatureChanges = _signatureDetector.Detect(files);

            // Step 4: Pre-fetch caller/callee context for signature changes (deterministic)
            foreach (var change in signatureChanges)
            {
                var fileDiff = files.FirstOrDefault(f => f.Path == change.FilePath);
                if (fileDiff is { FullContent: null })
                {
                    var content = await provider.FetchFileContentAsync(change.FilePath, null, cancellationToken);
                    if (content != null)
                    {
                        fileDiff.FullContent = content;
                    }
                }
            }

            // Step 5: Build normalized context
            var context = new NormalizedDiffContext
            {
                PrOrMrNumber = request.PrOrMrNumber,
                Files = files,
                ProjectPrinciples = request.ProjectPrinciples,
            };

            // Step 6: LLM analysis — Pass 1 (single call)
            var outcome = await analyzer.AnalyzeAsync(context, cancellationToken);

            AnalysisResult analysis;
            if (outcome.NeedsMoreContext)
            {
                // Step 6b: Deterministic fetch of requested files
                var additionalFiles = new Dictionary<string, string>();
                foreach (var fileReq in outcome.ContextRequest!.Files)
                {
                    var content = await provider.FetchFileContentAsync(fileReq.Path, null, cancellationToken);
                    if (content != null)
                        additionalFiles[fileReq.Path] = content;
                }

                // Step 6c: LLM analysis — Pass 2 (single call, final)
                analysis = await analyzer.AnalyzeWithContextAsync(context, additionalFiles, cancellationToken);
            }
            else
            {
                analysis = outcome.Result!;
            }

            // Step 7: Deterministic verdict resolution
            var findings = analysis.Findings;
            ApplyFindingsCap(findings, analysis.Summary, out var cappedFindings, out var effectiveSummary);
            TruncateFixSuggestions(cappedFindings);

            var verdict = VerdictResolver.Resolve(cappedFindings);

            // Step 8: Submit review (deterministic, provider-specific)
            var submission = new ReviewSubmission
            {
                PrOrMrNumber = request.PrOrMrNumber,
                Verdict = verdict,
                Summary = effectiveSummary,
                Findings = cappedFindings.ToArray(),
            };

            var submissionResult = await provider.SubmitReviewAsync(submission, cancellationToken);

            // Step 9: Construct final URL for GitLab if not returned by provider
            var reviewUrl = submissionResult.Url;
            if (string.IsNullOrEmpty(reviewUrl) && provider.ProviderType == Domain.Enums.ProviderType.GITLAB)
            {
                reviewUrl = $"{integration.Url?.TrimEnd('/')}/-/merge_requests/{request.PrOrMrNumber}";
            }

            _logger.LogInformation(
                "Hybrid review pipeline completed: verdict={Verdict} findings={FindingCount} prOrMrNumber={PrMrNumber}",
                verdict, cappedFindings.Count, request.PrOrMrNumber);

            // Step 10: Map to ReviewToolResult (deterministic)
            return new ReviewToolResult
            {
                Success = submissionResult.Success,
                Verdict = verdict,
                ReviewUrl = reviewUrl,
                Summary = effectiveSummary,
                Findings = cappedFindings.ToArray(),
                Error = submissionResult.Error,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hybrid review pipeline failed: prOrMrNumber={PrMrNumber} workspaceId={WorkspaceId}",
                request.PrOrMrNumber, request.WorkspaceId);

            return new ReviewToolResult
            {
                Success = false,
                Error = ex.Message,
            };
        }
    }

    private static void ApplyFindingsCap(
        List<ReviewFinding> findings,
        string summary,
        out List<ReviewFinding> capped,
        out string effectiveSummary)
    {
        if (findings.Count <= MaxFindings)
        {
            capped = findings;
            effectiveSummary = summary;
            return;
        }

        var overflow = findings.Count - MaxFindings;
        capped = findings.Take(MaxFindings).ToList();
        effectiveSummary = summary +
            $" Note: {overflow} additional findings were identified but have been truncated due to result size limits.";
    }

    private static void TruncateFixSuggestions(List<ReviewFinding> findings)
    {
        foreach (var finding in findings)
        {
            if (finding.FixSuggestion?.Length > MaxFixSuggestionLength)
                finding.FixSuggestion = finding.FixSuggestion[..MaxFixSuggestionLength] + "...";
        }
    }
}
