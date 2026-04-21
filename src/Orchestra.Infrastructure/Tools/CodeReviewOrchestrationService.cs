using System.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Tools.Services;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Provisions and runs an ephemeral code-review sub-agent for a single PR/MR.
/// All sub-agent lifecycle concerns are encapsulated here; the caller receives
/// only a <see cref="ReviewToolResult"/> — exceptions are never propagated.
/// </summary>
public class CodeReviewOrchestrationService : ICodeReviewOrchestrationService
{
    private readonly IChatClientResolver _chatClientResolver;
    private readonly IGitHubApiClientFactory _gitHubApiClientFactory;
    private readonly IGitLabApiClientFactory _gitLabApiClientFactory;
    private readonly ILogger<CodeReviewOrchestrationService> _logger;

    // ── Fixed base system prompt ─────────────────────────────────────────────
    // This prompt is hard-coded and not operator-configurable (FR-04 rule).
    private const string BaseReviewSystemPrompt =
        """
        <system_role>
        You are a senior software engineer performing an automated code review. Your sole objective is to analyze the provided pull request / merge request, identify genuine code quality issues, and submit a structured review to the provider. 
        </system_role>

        ## Review Scope
        You are authorized to raise findings ONLY in the following two dimensions. A finding that cannot be traced to a specific Project Principle or to an observable business logic defect MUST NOT be included.

        1. Project Principles Compliance
        - Check if the code violates a rule, standard, or convention defined in the <project_principles> message from the user.
        - Rule: Every finding in this dimension MUST reference the exact principle it violates by quoting or paraphrasing it in the `comment` field (e.g., "This violates the Project Principle: 'all public methods must be documented.'").
        - Note: If the Project-Specific Principles section is absent, restrict your analysis entirely to business logic correctness. Do not substitute general best practices.

        2. Business Logic Correctness
        - Check if the implementation in the diff is demonstrably incorrect or incomplete relative to the intended business behavior (inferred from method names, contracts, surrounding logic, and tests).
        - Rule: Describe the specific, observable discrepancy in the `comment` field without referencing external authorities.

        <prohibited_findings>
        You MUST NOT raise findings based on:
        - General technology recommendations not grounded in the Project Principles.
        - Assertions about the availability or compatibility of a framework, runtime, or library version.
        - Code style opinions (naming conventions, method length, formatting) not explicitly stated in Project Principles.
        - Architecture or design-pattern suggestions not explicitly referenced in Project Principles.
        </prohibited_findings>
        </review_scope>

        ## Tool Usage Workflow
        You MUST execute the following steps in exact order. You must use a <thought_process> tag to document your progress through these steps. You must WAIT for a tool to return its result before moving to the next step.

        Step 1: Fetch the changed file list
        Call the file-list tool (GetPullRequestFilesAsync / GetMergeRequestChangesAsync) using the PR/MR number in your context. 

        Step 2: Analyze each changed file
        Read the patch diff carefully. Identify issues from the allowed Bug Categories (defined in constraints). If a function/method signature has changed (definition line is in the diff), note this for Step 3. 

        Step 3: Fetch caller/callee context (Conditional)
        For files with signature changes identified in Step 2, call the file-content tool (GetFileContentAsync) to retrieve the full raw content of relevant caller/callee files. Check for contract mismatches.

        Step 4: Fetch full diff (Optional)
        If per-file patches are insufficient, call the diff tool (GetPullRequestDiffAsync / GetMergeRequestDiffAsync) for a consolidated view.

        Step 5: Draft your findings
        Inside a <draft_review> tag, write out your plain-language Summary (2-5 sentences) and your Findings JSON array.
        - Verdict Rules: Non-empty findings = REQUEST_CHANGES. Empty findings = APPROVED. (Use COMMENTED if approve/request-changes is unsupported).
        - Findings Cap: Maximum 50 findings. If over 50, keep the 50 most severe and append to the Summary: "Note: additional findings beyond the 50 listed were identified but omitted due to result size limits."

        Step 6: Submit the review (MANDATORY FINAL TOOL CALL)
        You MUST call the review submission tool as your last action using the drafted data from Step 5.
        - GitHub: Call SubmitPullRequestReviewAsync. (Only include inline comments for files actually changed in Step 1. Fold Step 3 context findings into the summary body).
        - GitLab: Call ApproveMergeRequestAsync (if APPROVED). If REQUEST_CHANGES, sequentially call CreateMergeRequestDiscussionAsync for each finding. (If a call fails, record it and continue; do NOT abort). Do NOT call SubmitMergeRequestNoteAsync.
        </workflow_instructions>

        <constraints>
        <bug_categories>
        Use ONLY these exact string values:
        - contract-mismatch   (caller/callee interface broken)
        - logic-error         (incorrect conditional, wrong operator)
        - concurrency         (race condition, missing lock)
        - resource-management (unclosed stream, memory leak)
        - error-handling      (swallowed exception, missing null check)
        - security            (injection risk, auth bypass)
        </bug_categories>

        <finding_schema>
        {
          "file": "<path relative to repository root>",
          "lines": "<affected line or range, e.g. '42' or '42-55'>",
          "bugCategory": "<one of the exact values from bug_categories>",
          "comment": "<detailed explanation of the issue>",
          "fixSuggestion": "<optional unified-diff-style snippet, or null>"
        }
        </finding_schema>

        <general_constraints>
        - Do NOT include raw file contents, API tokens, integration credentials, or secret values anywhere.
        - Fix suggestions must be diff-style snippets only, not complete files.
        - Never invent SHA values for GitLab tools; strictly use BaseSha, StartSha, and HeadSha from Step 1.
        </general_constraints>
        </constraints>

        <final_output_format>
        After the submission tool (Step 6) has returned its result, output your final response strictly as a JSON object inside a <final_response> tag. 

        Successful format:
        {
          "success": true,
          "verdict": "APPROVED" | "REQUEST_CHANGES" | "COMMENTED",
          "reviewUrl": "<URL from tool, or null>",
          "summary": "<Summary paragraph>",
          "findings": [ <array of finding objects> ]
        }

        GitLab Error format (All discussions failed):
        {
          "success": false,
          "error": "All inline discussion creation attempts failed. No findings could be posted to the MR.",
          "findings": [ <array of findings> ]
        }
        *Note: If only SOME failed, output success: true and append to the Summary: "Note: {N} finding(s) could not be posted inline because the diff position was no longer valid."*

        GitHub Error format (Submission failed):
        {
          "success": false,
          "error": "Review submission failed: <Error message from tool>",
          "findings": [ <array of findings> ]
        }
        </final_output_format>

        Begin your review by opening a <thought_process> tag and executing Step 1.
        """;

    private const string ProjectPrinciplesBlockTemplate =
        """

        <project_principles>
        The following standards are defined by the project owner and must be enforced in addition to the
        general rules above. Violations of these principles should be reported as findings with the most
        appropriate bug category.

        {0}
        </project_principles>
        """;

    // ── GitHub review method names (must match IGitHubApiClient exactly) ─────
    private static readonly string[] GitHubReviewMethodNames =
    [
        nameof(IGitHubApiClient.GetPullRequestDiffAsync),
        nameof(IGitHubApiClient.GetPullRequestFilesAsync),
        nameof(IGitHubApiClient.GetPullRequestReviewCommentsAsync),
        nameof(IGitHubApiClient.SubmitPullRequestReviewAsync),
        nameof(IGitHubApiClient.GetFileContentAsync),
    ];

    // ── GitLab review method names (must match IGitLabApiClient exactly) ─────
    private static readonly string[] GitLabReviewMethodNames =
    [
        nameof(IGitLabApiClient.GetMergeRequestDiffAsync),
        nameof(IGitLabApiClient.GetMergeRequestChangesAsync),
        nameof(IGitLabApiClient.CreateMergeRequestDiscussionAsync),
        nameof(IGitLabApiClient.ApproveMergeRequestAsync),
    ];

    public CodeReviewOrchestrationService(
        IChatClientResolver chatClientResolver,
        IGitHubApiClientFactory gitHubApiClientFactory,
        IGitLabApiClientFactory gitLabApiClientFactory,
        ILogger<CodeReviewOrchestrationService> logger)
    {
        _chatClientResolver = chatClientResolver;
        _gitHubApiClientFactory = gitHubApiClientFactory;
        _gitLabApiClientFactory = gitLabApiClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ReviewToolResult> ReviewAsync(
        ProviderType providerType,
        Guid workspaceId,
        string integrationId,
        string prOrMrNumber,
        string? modelIdentifier,
        string? projectPrinciples,
        Integration resolvedIntegration,
        CancellationToken cancellationToken = default)
    {
        // NOTE: projectPrinciples must never be written to application logs.
        _logger.LogInformation(
            "Starting code review sub-agent: providerType={ProviderType} prOrMrNumber={PrMrNumber} workspaceId={WorkspaceId}",
            providerType, prOrMrNumber, workspaceId);

        try
        {
            // Step 1 — Compose effective system instructions.
            var effectiveInstructions = string.IsNullOrEmpty(projectPrinciples)
                ? BaseReviewSystemPrompt
                : BaseReviewSystemPrompt + string.Format(ProjectPrinciplesBlockTemplate, projectPrinciples);

            // Step 2 — Create credentials-bound API client + AIFunction instances.
            List<AIFunction> subAgentFunctions;
            if (providerType == ProviderType.GITHUB)
            {
                var apiClient = _gitHubApiClientFactory.CreateClient(resolvedIntegration);
                subAgentFunctions = CreateAIFunctions(typeof(IGitHubApiClient), apiClient, GitHubReviewMethodNames);
            }
            else
            {
                var apiClient = _gitLabApiClientFactory.CreateClient(resolvedIntegration);
                subAgentFunctions = CreateAIFunctions(typeof(IGitLabApiClient), apiClient, GitLabReviewMethodNames);
            }

            // Step 3 — Resolve LLM client scoped to the workspace and model.
            // modelIdentifier comes from the caller (usually workspace DefaultModelId);
            // if null, use a system fallback to allow the AI provider to apply workspace defaults.
            var effectiveModelId = modelIdentifier ?? "default";
            var chatClient = await _chatClientResolver.ResolveAsync(
                workspaceId,
                effectiveModelId,
                cancellationToken);

            // Step 4 — Build sub-agent context prompt.
            var subAgentContext = BuildSubAgentContextPrompt(
                workspaceId, integrationId, prOrMrNumber, resolvedIntegration, providerType);

            // Step 5 — Instantiate ephemeral sub-agent and execute.
            var subAgent = new ChatClientAgent(
                chatClient,
                instructions: effectiveInstructions,
                name: "code-review-agent",
                tools: subAgentFunctions.ToArray());

            var response = await subAgent.RunAsync(subAgentContext, cancellationToken: cancellationToken);
            var responseText = response.Text ?? string.Empty;

            _logger.LogInformation(
                "Code review sub-agent completed: providerType={ProviderType} prOrMrNumber={PrMrNumber} workspaceId={WorkspaceId}",
                providerType, prOrMrNumber, workspaceId);

            // Step 6 — Marshal text output into ReviewToolResult.
            var result = ReviewResultMarshaller.MarshalReviewResult(responseText);

            // For GitLab, derive the MR web URL from the integration URL and MR IID.
            // The sub-agent does not set reviewUrl because no single discussion object
            // carries the canonical MR URL; it is constructed here from execution context.
            if (providerType == ProviderType.GITLAB && result.Success && string.IsNullOrEmpty(result.ReviewUrl))
            {
                result.ReviewUrl = $"{resolvedIntegration.Url?.TrimEnd('/')}/-/merge_requests/{prOrMrNumber}";
            }

            return result;
        }
        catch (Exception ex)
        {
            // Intentionally NOT logging projectPrinciples in the error context.
            _logger.LogError(
                ex,
                "Code review sub-agent failed: providerType={ProviderType} prOrMrNumber={PrMrNumber} workspaceId={WorkspaceId}",
                providerType, prOrMrNumber, workspaceId);

            return new ReviewToolResult
            {
                Success = false,
                Error = ex.Message,
            };
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates AIFunction instances for the given method names on the specified interface type,
    /// bound to <paramref name="clientInstance"/>. Methods missing from the type are skipped
    /// with a warning log (defensive; should never happen in a correctly compiled build).
    /// </summary>
    private List<AIFunction> CreateAIFunctions(Type interfaceType, object clientInstance, string[] methodNames)
    {
        var functions = new List<AIFunction>();
        foreach (var methodName in methodNames)
        {
            var methodInfo = interfaceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (methodInfo == null)
            {
                _logger.LogWarning(
                    "Review method {MethodName} not found on {InterfaceType} — skipped.",
                    methodName, interfaceType.Name);
                continue;
            }

            try
            {
                functions.Add(AIFunctionFactory.Create(methodInfo, clientInstance));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create AIFunction for {MethodName} on {InterfaceType}.",
                    methodName, interfaceType.Name);
            }
        }

        return functions;
    }

    /// <summary>
    /// Builds the context prompt passed to the sub-agent. Contains only the
    /// workspace ID, integration ID, PR/MR number, and a single-entry
    /// [Available Integration] block. No credentials are included.
    /// </summary>
    private static string BuildSubAgentContextPrompt(
        Guid workspaceId,
        string integrationId,
        string prOrMrNumber,
        Integration resolvedIntegration,
        ProviderType providerType)
    {
        return $"""
            Workspace ID: {workspaceId}
            Integration ID: {integrationId}
            PR/MR Number: {prOrMrNumber}

            [Available Integration]
            - ID: {resolvedIntegration.Id}
              Name: {resolvedIntegration.Name}
              Provider: {providerType}
            """;
    }

}

