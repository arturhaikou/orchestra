using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Tools.Models.Jira;

/// <summary>
/// Represents the error response structure returned by JIRA API for validation failures.
/// Used to parse 400 Bad Request responses and provide detailed error messages to agents.
/// </summary>
public class JiraErrorResponse
{
    /// <summary>
    /// Collection of field-specific error messages.
    /// Key: field name, Value: error message(s)
    /// </summary>
    [JsonPropertyName("errors")]
    public Dictionary<string, string>? Errors { get; set; }

    /// <summary>
    /// General error messages that are not field-specific.
    /// </summary>
    [JsonPropertyName("errorMessages")]
    public List<string>? ErrorMessages { get; set; }

    /// <summary>
    /// Formats the error response into a single human-readable message.
    /// </summary>
    public string GetFormattedMessage()
    {
        var messages = new List<string>();

        if (ErrorMessages != null && ErrorMessages.Any())
        {
            messages.AddRange(ErrorMessages);
        }

        if (Errors != null && Errors.Any())
        {
            var fieldErrors = Errors.Select(e => $"{e.Key}: {e.Value}");
            messages.AddRange(fieldErrors);
        }

        return messages.Any() 
            ? string.Join("; ", messages) 
            : "Unknown validation error";
    }
}