namespace Orchestra.Application.Common.Interfaces;

public interface IAgentCliSkillDataAccess
{
    Task AssignSkillsAsync(Guid agentId, IReadOnlyList<string> skillNames, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetSkillNamesAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task ReplaceSkillsAsync(Guid agentId, IReadOnlyList<string> skillNames, CancellationToken cancellationToken = default);
}
