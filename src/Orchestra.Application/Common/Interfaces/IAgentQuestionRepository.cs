using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

public interface IAgentQuestionRepository
{
    Task<AgentQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<AgentQuestion>> GetPendingByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(AgentQuestion question, CancellationToken cancellationToken = default);

    Task SaveAnswerAsync(
        Guid questionId,
        string answersJson,
        CancellationToken cancellationToken = default);
}
