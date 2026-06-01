using Orchestra.Application.Skills.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface ISkillFolderDiscoveryService
{
    Task<IReadOnlyList<DiscoveredSkillDto>> DiscoverSkillsAsync(string folderPath, CancellationToken cancellationToken = default);
}
