namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Result of an agent execution attempt.
/// </summary>
public class AgentExecutionResult
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }

    private AgentExecutionResult() { }

    public static AgentExecutionResult Success(string message)
        => new() { IsSuccess = true, Message = message };

    public static AgentExecutionResult Failure(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
