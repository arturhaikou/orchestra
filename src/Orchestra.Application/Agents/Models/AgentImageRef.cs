namespace Orchestra.Application.Agents.Models;

/// <summary>
/// Represents a single image reference to be injected into the agent's context.
/// The image can originate from a local file path or an authenticated remote URL.
/// </summary>
/// <param name="Source">
/// The image source — either an absolute local file path (e.g. "C:\uploads\screenshot.png")
/// or an HTTPS URL. For authenticated Jira URLs the bytes will be pre-fetched and stored
/// in <see cref="Bytes"/>; the Source is still preserved so the agent can reference it
/// in tool calls (e.g. CommentContentBlock.Content).
/// </param>
/// <param name="MimeType">The MIME type of the image (e.g. "image/png").</param>
/// <param name="FileName">Human-readable file name shown in the [Images] context section.</param>
/// <param name="Bytes">
/// Pre-fetched image bytes. Populated for authenticated remote images (e.g. Jira attachments)
/// that the LLM cannot fetch directly. Null for local file paths and public URLs.
/// </param>
public record AgentImageRef(
    string Source,
    string MimeType,
    string FileName,
    byte[]? Bytes = null);
