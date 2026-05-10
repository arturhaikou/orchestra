using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;

namespace Orchestra.Application.McpServers;

public class McpServerService : IMcpServerService
{
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IMcpServerDataAccess _mcpServerDataAccess;

    public McpServerService(
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IMcpServerDataAccess mcpServerDataAccess)
    {
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _mcpServerDataAccess = mcpServerDataAccess;
    }

    public async Task<bool> IsNameUniqueAsync(
        Guid requestingUserId,
        Guid workspaceId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.ValidateMembershipAsync(
            requestingUserId, workspaceId, cancellationToken);

        var exists = await _mcpServerDataAccess.ExistsByNameAsync(
            workspaceId, name, excludeId, cancellationToken);

        return !exists;
    }
}
