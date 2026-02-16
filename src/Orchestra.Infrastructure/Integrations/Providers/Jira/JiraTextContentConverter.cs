using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Implementation of IJiraTextContentConverter that handles conversion between Markdown and Jira formats.
/// Uses IAdfConversionService for Cloud (ADF) and HtmlConverter for On-Premise (HTML).
/// </summary>
public class JiraTextContentConverter : IJiraTextContentConverter
{
    private readonly IAdfConversionService _adfConversionService;
    private readonly ILogger<JiraTextContentConverter> _logger;

    public JiraTextContentConverter(
        IAdfConversionService adfConversionService,
        ILogger<JiraTextContentConverter> logger)
    {
        _adfConversionService = adfConversionService;
        _logger = logger;
    }

    public async Task<object> ConvertMarkdownToCommentBodyAsync(
        string markdown,
        JiraType jiraType,
        CancellationToken cancellationToken = default)
    {
        return jiraType switch
        {
            JiraType.Cloud => await _adfConversionService.ConvertMarkdownToAdfAsync(markdown, cancellationToken),
            JiraType.OnPremise => markdown, // v2 uses plain text or HTML
            _ => throw new InvalidOperationException($"Unsupported JiraType: {jiraType}")
        };
    }

    public async Task<object> ConvertMarkdownToDescriptionAsync(
        string markdown,
        JiraType jiraType,
        CancellationToken cancellationToken = default)
    {
        return jiraType switch
        {
            JiraType.Cloud => await _adfConversionService.ConvertMarkdownToAdfAsync(markdown, cancellationToken),
            JiraType.OnPremise => markdown, // v2 uses plain text or HTML
            _ => throw new InvalidOperationException($"Unsupported JiraType: {jiraType}")
        };
    }

    public async Task<string?> ConvertCommentBodyToMarkdownAsync(
        JsonElement body,
        JiraType jiraType,
        CancellationToken cancellationToken = default)
    {
        return jiraType switch
        {
            JiraType.Cloud => await _adfConversionService.ConvertAdfToMarkdownAsync(body, cancellationToken),
            JiraType.OnPremise => ExtractHtmlOrPlainText(body),
            _ => throw new InvalidOperationException($"Unsupported JiraType: {jiraType}")
        };
    }

    public async Task<string?> ConvertDescriptionToMarkdownAsync(
        JsonElement description,
        JiraType jiraType,
        CancellationToken cancellationToken = default)
    {
        return jiraType switch
        {
            JiraType.Cloud => await _adfConversionService.ConvertAdfToMarkdownAsync(description, cancellationToken),
            JiraType.OnPremise => ExtractHtmlOrPlainText(description),
            _ => throw new InvalidOperationException($"Unsupported JiraType: {jiraType}")
        };
    }

    /// <summary>
    /// Extracts text from HTML or plain text for On-Premise Jira.
    /// Currently returns the raw value; future enhancement can add HTML-to-Markdown conversion.
    /// </summary>
    private string? ExtractHtmlOrPlainText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }
        
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }
        
        // If it's a complex object (unlikely for simple fields), serialize it
        return element.ToString();
    }
}
