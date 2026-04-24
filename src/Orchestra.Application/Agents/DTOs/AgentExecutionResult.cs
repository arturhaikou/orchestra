namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Result of an agent execution attempt.
/// </summary>
public class AgentExecutionResult
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ReviewUrl { get; init; }

    private AgentExecutionResult() { }

    public static AgentExecutionResult Success(string message, string? reviewUrl = null)
        => new() { IsSuccess = true, Message = message, ReviewUrl = reviewUrl };

    public static AgentExecutionResult Failure(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
