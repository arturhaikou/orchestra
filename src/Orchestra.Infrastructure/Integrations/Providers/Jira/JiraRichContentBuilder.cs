using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Tools.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

public class JiraRichContentBuilder : IJiraRichContentBuilder
{
    // Matches ![alt](file://path) — captures (alt, path)
    private static readonly Regex LocalImagePattern =
        new(@"!\[([^\]]*)\]\(file://([^)]+)\)", RegexOptions.Compiled);

    private readonly IAdfConversionService _adfConversionService;

    public JiraRichContentBuilder(IAdfConversionService adfConversionService)
    {
        _adfConversionService = adfConversionService;
    }

    public bool ContainsLocalImageRefs(string markdown) =>
        !string.IsNullOrEmpty(markdown) && LocalImagePattern.IsMatch(markdown);

    public string StripLocalImageRefs(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return markdown;
        var stripped = LocalImagePattern.Replace(markdown, string.Empty);
        // Collapse runs of blank lines left by removed image refs
        return Regex.Replace(stripped, @"\n{3,}", "\n\n").Trim();
    }

    public async Task<JsonElement> BuildAdfAsync(
        IJiraApiClient apiClient,
        string issueKey,
        string markdown,
        CancellationToken ct = default)
    {
        var allNodes = new JsonArray();
        var matches = LocalImagePattern.Matches(markdown);
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            // Text segment before this image
            var textSegment = markdown[lastIndex..match.Index].Trim();
            if (!string.IsNullOrEmpty(textSegment))
            {
                var adfDoc = await _adfConversionService.ConvertMarkdownToAdfAsync(textSegment, ct);
                if (adfDoc.TryGetProperty("content", out var contentProp))
                {
                    foreach (var node in contentProp.EnumerateArray())
                        allNodes.Add(JsonNode.Parse(node.GetRawText()));
                }
            }

            // Image node
            var altText = match.Groups[1].Value;
            var filePath = NormalizeLocalFilePath(match.Groups[2].Value);
            if (!Path.IsPathRooted(filePath))
                throw new ArgumentException(
                    $"Image path must be absolute. Received relative path: '{match.Groups[2].Value}'. Please provide the full absolute path (e.g. C:\\Users\\...\\image.png).");
            var fileName = string.IsNullOrEmpty(altText) ? Path.GetFileName(filePath) : altText;
            var mimeType = ResolveMimeType(fileName);

            await using var stream = File.OpenRead(filePath);
            var attachment = await apiClient.UploadAttachmentAsync(issueKey, stream, fileName, mimeType, ct);
            allNodes.Add(!string.IsNullOrEmpty(attachment.MediaApiFileId)
                ? BuildMediaSingleNode(attachment.MediaApiFileId)
                : BuildExternalMediaSingleNode(attachment.Content!));

            lastIndex = match.Index + match.Length;
        }

        // Remaining text after last image
        var tail = markdown[lastIndex..].Trim();
        if (!string.IsNullOrEmpty(tail))
        {
            var adfDoc = await _adfConversionService.ConvertMarkdownToAdfAsync(tail, ct);
            if (adfDoc.TryGetProperty("content", out var contentProp))
            {
                foreach (var node in contentProp.EnumerateArray())
                    allNodes.Add(JsonNode.Parse(node.GetRawText()));
            }
        }

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["version"] = 1,
            ["content"] = allNodes
        };

        return JsonSerializer.Deserialize<JsonElement>(doc.ToJsonString());
    }

    public async Task<JsonElement> BuildAdfFromBlocksAsync(
        IJiraApiClient apiClient,
        string issueKey,
        IReadOnlyList<ContentBlock> blocks,
        CancellationToken ct = default)
    {
        var allNodes = new JsonArray();

        foreach (var block in blocks)
        {
            if (block.Type == "text")
            {
                var adfDoc = await _adfConversionService.ConvertMarkdownToAdfAsync(block.Content, ct);
                if (adfDoc.TryGetProperty("content", out var contentProp))
                {
                    foreach (var node in contentProp.EnumerateArray())
                        allNodes.Add(JsonNode.Parse(node.GetRawText()));
                }
            }
            else if (block.Type == "image")
            {
                var content = block.Content;
                if (content.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    content.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    allNodes.Add(BuildExternalMediaSingleNode(content));
                }
                else
                {
                    var localPath = NormalizeLocalFilePath(content);
                    if (!Path.IsPathRooted(localPath))
                        throw new ArgumentException(
                            $"Image path must be absolute. Received relative path: '{content}'. Please provide the full absolute path (e.g. C:\\Users\\...\\image.png).");
                    var fileName = block.FileName ?? Path.GetFileName(localPath);
                    var mimeType = ResolveMimeType(fileName);
                    await using var stream = File.OpenRead(localPath);
                    var attachment = await apiClient.UploadAttachmentAsync(issueKey, stream, fileName, mimeType, ct);
                    allNodes.Add(!string.IsNullOrEmpty(attachment.MediaApiFileId)
                        ? BuildMediaSingleNode(attachment.MediaApiFileId)
                        : BuildExternalMediaSingleNode(attachment.Content!));
                }
            }
        }

        var doc = new JsonObject
        {
            ["type"] = "doc",
            ["version"] = 1,
            ["content"] = allNodes
        };

        return JsonSerializer.Deserialize<JsonElement>(doc.ToJsonString());
    }

    private static JsonObject BuildMediaSingleNode(string mediaId) =>
        new()
        {
            ["type"] = "mediaSingle",
            ["attrs"] = new JsonObject { ["layout"] = "center" },
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "media",
                    ["attrs"] = new JsonObject
                    {
                        ["id"] = mediaId,
                        ["type"] = "file",
                        ["collection"] = "jira-attachment-collection-v2"
                    }
                }
            }
        };

    private static JsonObject BuildExternalMediaSingleNode(string url) =>
        new()
        {
            ["type"] = "mediaSingle",
            ["attrs"] = new JsonObject { ["layout"] = "center" },
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "media",
                    ["attrs"] = new JsonObject
                    {
                        ["type"] = "external",
                        ["url"] = url
                    }
                }
            }
        };

    private static string NormalizeLocalFilePath(string path) =>
        path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
            ? path[8..]
            : path.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? path[7..]
                : path;

    private static string ResolveMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"            => "image/gif",
            ".svg"            => "image/svg+xml",
            ".webp"           => "image/webp",
            _                 => "image/png"
        };
}
