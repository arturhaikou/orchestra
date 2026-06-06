namespace Orchestra.Application.Agents.DTOs;

public record GlobalAgentQuestionDto(
    Guid WorkspaceId,
    string WorkspaceName,
    Guid QuestionId,
    Guid? TicketId,
    string? TicketTitle,
    string AgentName,
    string QuestionsJson,
    DateTime CreatedAt);
