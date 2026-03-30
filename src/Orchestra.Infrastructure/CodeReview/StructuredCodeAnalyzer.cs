using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;

namespace Orchestra.Infrastructure.CodeReview;

/// <summary>
/// LLM-based code analyzer using structured output via <c>GetResponseAsync&lt;T&gt;</c>.
/// Falls back to text-based JSON parsing when the model does not support structured output.
/// No tool calls, no multi-turn conversation — a single structured LLM call per pass.
/// </summary>
public partial class StructuredCodeAnalyzer : ICodeAnalyzer
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<StructuredCodeAnalyzer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string JsonFallbackInstruction =
        """

        <output_format>
        You MUST respond with a single raw JSON object only. No markdown fences, no commentary, no explanation outside the JSON.
        The JSON schema:
        {
          "summary": "string",
          "findings": [
            {
              "file": "string",
              "lines": "string",
              "bugCategory": "string",
              "comment": "string",
              "fixSuggestion": "string or null"
            }
          ],
          "needsAdditionalContext": false,
          "requestedFiles": [
            { "path": "string", "reason": "string" }
          ]
        }
        </output_format>
        """;

    [GeneratedRegex(@"```(?:json)?\s*\n?(.*?)\n?\s*```", RegexOptions.Singleline)]
    private static partial Regex MarkdownCodeBlockRegex();

    [GeneratedRegex(@"@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@")]
    private static partial Regex HunkHeaderRegex();

    private const string AnalysisSystemPrompt =
        """
        You are a senior software engineer performing a code review. Analyze the provided diff context and identify genuine code quality issues.

        <review_scope>
        You may raise findings ONLY in two dimensions:

        1. Project Principles Compliance (only when principles are provided)
        - Every finding MUST reference the exact principle it violates.
        - If no project principles are provided, skip this dimension entirely.

        2. Business Logic Correctness
        - Identify code that is demonstrably incorrect or incomplete.
        - Describe the specific, observable discrepancy.

        You MUST NOT raise findings based on:
        - General technology recommendations not grounded in Project Principles.
        - Framework/runtime/library version compatibility assertions.
        - Code style opinions not in Project Principles.
        - Architecture/design-pattern suggestions not in Project Principles.
        </review_scope>

        <bug_categories>
        Use ONLY these exact string values:
        - contract-mismatch   (caller/callee interface broken)
        - logic-error         (incorrect conditional, wrong operator)
        - concurrency         (race condition, missing lock)
        - resource-management (unclosed stream, memory leak)
        - error-handling      (swallowed exception, missing null check)
        - security            (injection risk, auth bypass)
        </bug_categories>

        <instructions>
        If you have enough context to complete the review, set needsAdditionalContext to false and provide your findings.
        If a method signature changed and you need to see caller/callee files to check for contract mismatches, set needsAdditionalContext to true and list the files you need in requestedFiles.
        Fix suggestions must contain ONLY the corrected replacement code for the affected lines — no diff markers (+/-), no line numbers, no fencing. The suggestion replaces the original lines verbatim, so provide only the final desired text. Not complete files. Max 2000 characters per suggestion.
        Maximum 50 findings. Prioritize by severity if more are found.
        </instructions>

        <line_numbers>
        Each diff line is prefixed with its new-file line number (e.g. "   42 | +code").
        The "lines" field in each finding MUST use these prefixed numbers.
        For a single line use "42". For a range use "42-55". Never count lines yourself — always read the number from the prefix.
        </line_numbers>
        """;

    private const string DeepAnalysisSystemPrompt =
        """
        You are a senior software engineer performing a code review. You previously requested additional file context. Analyze the original diff together with the additional files provided and identify genuine code quality issues.

        <review_scope>
        You may raise findings ONLY in two dimensions:

        1. Project Principles Compliance (only when principles are provided)
        - Every finding MUST reference the exact principle it violates.
        - If no project principles are provided, skip this dimension entirely.

        2. Business Logic Correctness
        - Identify code that is demonstrably incorrect or incomplete.
        - Describe the specific, observable discrepancy.

        You MUST NOT raise findings based on:
        - General technology recommendations not grounded in Project Principles.
        - Framework/runtime/library version compatibility assertions.
        - Code style opinions not in Project Principles.
        - Architecture/design-pattern suggestions not in Project Principles.
        </review_scope>

        <bug_categories>
        Use ONLY these exact values:
        - contract-mismatch, logic-error, concurrency, resource-management, error-handling, security
        </bug_categories>

        <instructions>
        Provide your final findings. Fix suggestions must contain ONLY the corrected replacement code for the affected lines — no diff markers (+/-), no line numbers, no fencing. The suggestion replaces the original lines verbatim, so provide only the final desired text. Not complete files. Max 2000 characters each. Maximum 50 findings.
        </instructions>

        <line_numbers>
        Each diff line is prefixed with its new-file line number (e.g. "   42 | +code").
        The "lines" field in each finding MUST use these prefixed numbers.
        For a single line use "42". For a range use "42-55". Never count lines yourself — always read the number from the prefix.
        </line_numbers>
        """;

    public StructuredCodeAnalyzer(IChatClient chatClient, ILogger<StructuredCodeAnalyzer> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<CodeAnalysisOutcome> AnalyzeAsync(
        NormalizedDiffContext context, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AnalysisSystemPrompt),
            new(ChatRole.User, FormatDiffContext(context)),
        };

        var result = await GetStructuredResponseAsync<AnalysisResponse>(
            messages, cancellationToken);

        if (result is { NeedsAdditionalContext: true, RequestedFiles: { Count: > 0 } files })
        {
            return CodeAnalysisOutcome.FromContextRequest(
                new AdditionalContextRequest
                {
                    Files = files.Select(f => new FileContextRequest
                    {
                        Path = f.Path,
                        Reason = f.Reason,
                    }).ToList(),
                });
        }

        return CodeAnalysisOutcome.FromResult(new AnalysisResult
        {
            Summary = result.Summary,
            Findings = result.Findings,
        });
    }

    public async Task<AnalysisResult> AnalyzeWithContextAsync(
        NormalizedDiffContext context,
        Dictionary<string, string> additionalFiles,
        CancellationToken cancellationToken)
    {
        var userContent = new StringBuilder();
        userContent.Append(FormatDiffContext(context));
        userContent.AppendLine();
        userContent.AppendLine("--- ADDITIONAL FILE CONTEXT ---");

        foreach (var (path, content) in additionalFiles)
        {
            userContent.AppendLine($"\n## File: {path}");
            userContent.AppendLine("```");
            userContent.AppendLine(content);
            userContent.AppendLine("```");
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, DeepAnalysisSystemPrompt),
            new(ChatRole.User, userContent.ToString()),
        };

        var result = await GetStructuredResponseAsync<AnalysisResponse>(
            messages, cancellationToken);

        return new AnalysisResult
        {
            Summary = result.Summary,
            Findings = result.Findings,
        };
    }

    /// <summary>
    /// Tries structured output via <c>GetResponseAsync&lt;T&gt;</c> first.
    /// If the model does not support structured output (throws or returns null),
    /// falls back to a text-based call with explicit JSON instructions and manual parsing.
    /// </summary>
    private async Task<T> GetStructuredResponseAsync<T>(
        List<ChatMessage> messages, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var response = await _chatClient.GetResponseAsync<T>(
                messages, cancellationToken: cancellationToken);

            if (response.Result is not null)
                return response.Result;

            _logger.LogWarning(
                "Structured output returned null Result for {Type}. Falling back to text-based JSON parsing",
                typeof(T).Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Structured output failed for {Type}. Falling back to text-based JSON parsing",
                typeof(T).Name);
        }

        return await GetTextBasedJsonResponseAsync<T>(messages, cancellationToken);
    }

    private async Task<T> GetTextBasedJsonResponseAsync<T>(
        List<ChatMessage> messages, CancellationToken cancellationToken) where T : class
    {
        var fallbackMessages = new List<ChatMessage>(messages.Count);
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.System)
            {
                // Append JSON format instructions to the system prompt
                fallbackMessages.Add(new ChatMessage(ChatRole.System, msg.Text + JsonFallbackInstruction));
            }
            else
            {
                fallbackMessages.Add(msg);
            }
        }

        var textResponse = await _chatClient.GetResponseAsync(
            fallbackMessages, cancellationToken: cancellationToken);

        return ExtractAndDeserialize<T>(textResponse.Text)
               ?? throw new InvalidOperationException(
                   $"Failed to parse {typeof(T).Name} from LLM text response. " +
                   $"Raw text (truncated): {Truncate(textResponse.Text, 500)}");
    }

    private static T? ExtractAndDeserialize<T>(string? text) where T : class
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();

        // 1. Try direct deserialization (response is raw JSON)
        if (TryDeserialize<T>(trimmed, out var result))
            return result;

        // 2. Try extracting from markdown code blocks: ```json ... ```
        var match = MarkdownCodeBlockRegex().Match(trimmed);
        if (match.Success && TryDeserialize<T>(match.Groups[1].Value.Trim(), out result))
            return result;

        // 3. Try extracting the outermost JSON object from surrounding text
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var candidate = trimmed[firstBrace..(lastBrace + 1)];
            if (TryDeserialize<T>(candidate, out result))
                return result;
        }

        return null;
    }

    private static bool TryDeserialize<T>(string json, out T? result) where T : class
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return result is not null;
        }
        catch (JsonException)
        {
            result = null;
            return false;
        }
    }

    private static string Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= maxLength ? value
        : value[..maxLength] + "…";

    private static string FormatDiffContext(NormalizedDiffContext context)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(context.ProjectPrinciples))
        {
            sb.AppendLine("<project_principles>");
            sb.AppendLine(context.ProjectPrinciples);
            sb.AppendLine("</project_principles>");
            sb.AppendLine();
        }

        sb.AppendLine($"PR/MR Number: {context.PrOrMrNumber}");
        sb.AppendLine($"Changed files: {context.Files.Count}");
        sb.AppendLine();

        foreach (var file in context.Files)
        {
            sb.AppendLine($"## {file.Path} ({file.Status}, +{file.Additions}/-{file.Deletions})");

            if (!string.IsNullOrEmpty(file.Patch))
            {
                sb.AppendLine("```diff");
                sb.AppendLine(AnnotateDiffWithLineNumbers(file.Patch));
                sb.AppendLine("```");
            }

            if (!string.IsNullOrEmpty(file.FullContent))
            {
                sb.AppendLine("### Full file content:");
                sb.AppendLine("```");
                sb.AppendLine(file.FullContent);
                sb.AppendLine("```");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Annotates each line of a unified diff patch with the new-file line number
    /// so the LLM can reference exact line numbers without counting.
    /// </summary>
    private static string AnnotateDiffWithLineNumbers(string patch)
    {
        if (string.IsNullOrEmpty(patch)) return patch;

        var lines = patch.TrimEnd('\n', '\r').Split('\n');
        var sb = new StringBuilder();
        int newLine = 0;
        int oldLine = 0;

        foreach (var line in lines)
        {
            var hunkMatch = HunkHeaderRegex().Match(line);
            if (hunkMatch.Success)
            {
                oldLine = int.Parse(hunkMatch.Groups[1].Value);
                newLine = int.Parse(hunkMatch.Groups[2].Value);
                sb.AppendLine(line);
                continue;
            }

            if (line.StartsWith('+'))
            {
                sb.AppendLine($"{newLine,5} | {line}");
                newLine++;
            }
            else if (line.StartsWith('-'))
            {
                sb.AppendLine($"      | {line}");
                oldLine++;
            }
            else
            {
                // Context line (present in both old and new)
                sb.AppendLine($"{newLine,5} | {line}");
                oldLine++;
                newLine++;
            }
        }

        return sb.ToString().TrimEnd();
    }
}
