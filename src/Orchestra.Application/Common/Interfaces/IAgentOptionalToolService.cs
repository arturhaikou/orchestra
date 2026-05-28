namespace Orchestra.Application.Common.Interfaces;

public interface IAgentOptionalToolService
{
    Task<List<string>> GetCurrentSelectionsAsync(Guid userId, Guid agentId, CancellationToken cancellationToken = default);
    Task SaveSelectionsAsync(Guid userId, Guid agentId, IReadOnlyList<string> methodNames, CancellationToken cancellationToken = default);
}
