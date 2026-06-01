using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Skills.Services;

public class SkillFolderService : ISkillFolderService
{
    private readonly ISkillFolderDataAccess _skillFolderDataAccess;
    private readonly ISkillFolderDiscoveryService _discoveryService;
    private readonly IWorkspaceAuthorizationService _authorizationService;

    public SkillFolderService(
        ISkillFolderDataAccess skillFolderDataAccess,
        ISkillFolderDiscoveryService discoveryService,
        IWorkspaceAuthorizationService authorizationService)
    {
        _skillFolderDataAccess = skillFolderDataAccess ?? throw new ArgumentNullException(nameof(skillFolderDataAccess));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    public async Task<List<SkillFolderDto>> GetSkillFoldersAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var folders = await _skillFolderDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return folders.Select(MapToDto).ToList();
    }

    public async Task<SkillFolderDto?> GetSkillFolderByIdAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillFolderId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var folder = await _skillFolderDataAccess.GetByIdAsync(skillFolderId, cancellationToken);
        if (folder is null || folder.WorkspaceId != workspaceId)
            return null;

        return MapToDto(folder);
    }

    public async Task<SkillFolderDto> CreateSkillFolderAsync(
        Guid userId,
        CreateSkillFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, cancellationToken);

        if (!Directory.Exists(request.FolderPath))
            throw new ArgumentException($"Folder path '{request.FolderPath}' does not exist on the server.", nameof(request.FolderPath));

        var folder = SkillFolder.Create(request.WorkspaceId, request.Name, request.FolderPath);
        await _skillFolderDataAccess.AddAsync(folder, cancellationToken);

        return MapToDto(folder);
    }

    public async Task<SkillFolderDto?> UpdateSkillFolderAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillFolderId,
        UpdateSkillFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var folder = await _skillFolderDataAccess.GetByIdAsync(skillFolderId, cancellationToken);
        if (folder is null || folder.WorkspaceId != workspaceId)
            return null;

        if (!Directory.Exists(request.FolderPath))
            throw new ArgumentException($"Folder path '{request.FolderPath}' does not exist on the server.", nameof(request.FolderPath));

        folder.Update(request.Name, request.FolderPath);
        await _skillFolderDataAccess.UpdateAsync(folder, cancellationToken);

        return MapToDto(folder);
    }

    public async Task DeleteSkillFolderAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillFolderId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var exists = await _skillFolderDataAccess.ExistsInWorkspaceAsync(skillFolderId, workspaceId, cancellationToken);
        if (!exists)
            return;

        await _skillFolderDataAccess.DeleteAsync(skillFolderId, cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveredSkillDto>> GetAvailableSkillsAsync(
        Guid userId,
        Guid workspaceId,
        Guid skillFolderId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var folder = await _skillFolderDataAccess.GetByIdAsync(skillFolderId, cancellationToken);
        if (folder is null || folder.WorkspaceId != workspaceId)
            return [];

        return await _discoveryService.DiscoverSkillsAsync(folder.FolderPath, cancellationToken);
    }

    private static SkillFolderDto MapToDto(SkillFolder folder) =>
        new(
            Id: folder.Id.ToString(),
            WorkspaceId: folder.WorkspaceId.ToString(),
            Name: folder.Name,
            FolderPath: folder.FolderPath,
            CreatedAt: folder.CreatedAt,
            UpdatedAt: folder.UpdatedAt
        );
}
