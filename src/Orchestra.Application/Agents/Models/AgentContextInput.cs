namespace Orchestra.Application.Agents.Models;

/// <summary>
/// The enriched input context for agent execution, combining the text prompt
/// with optional image references extracted from the ticket.
/// </summary>
/// <param name="TextPrompt">
/// The fully enriched text prompt containing ticket context, comment history,
/// available integrations, project principles, and an [Images] section listing
/// each image's source path/URL so the agent can reference them in tool calls.
/// </param>
/// <param name="Images">
/// Image references extracted from the ticket's description and comments.
/// Empty when the ticket contains no images.
/// </param>
public record AgentContextInput(
    string TextPrompt,
    IReadOnlyList<AgentImageRef> Images)
{
    /// <summary>Returns an input with no images.</summary>
    public static AgentContextInput TextOnly(string textPrompt) =>
        new(textPrompt, []);
}
