using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Templates;

namespace Orchestra.Application.Common.Interfaces;

public interface ITemplateAvailabilityResolver
{
    Task<List<ResolvedTemplate>> ResolveAvailabilityAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task ValidatePrerequisitesAsync(
        Guid workspaceId,
        BuiltInAgentTemplate template,
        CancellationToken cancellationToken = default);
}
