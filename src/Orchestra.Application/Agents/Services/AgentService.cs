using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Agents.Services;

public class AgentService : IAgentService
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IToolValidationService _toolValidationService;

    public AgentService(
        IAgentDataAccess agentDataAccess,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IToolValidationService toolValidationService)
    {
        _agentDataAccess = agentDataAccess ?? throw new ArgumentNullException(nameof(agentDataAccess));
        _agentToolActionDataAccess = agentToolActionDataAccess ?? throw new ArgumentNullException(nameof(agentToolActionDataAccess));
        _workspaceAuthorizationService = workspaceAuthorizationService ?? throw new ArgumentNullException(nameof(workspaceAuthorizationService));
        _toolValidationService = toolValidationService ?? throw new ArgumentNullException(nameof(toolValidationService));
    }

    public async Task<AgentDto> CreateAgentAsync(Guid userId, CreateAgentRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Validate workspace membership
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            request.WorkspaceId, 
            cancellationToken);

        // 2. Create agent entity using domain factory
        var agent = Agent.Create(
            request.WorkspaceId,
            request.Name,
            request.Role,
            request.Capabilities?.ToList() ?? new List<string>(),
            request.CustomInstructions);

        // 3. Persist using data access layer
        await _agentDataAccess.AddAsync(agent, cancellationToken);

        // 4. Assign tool actions if provided
        if (request.ToolActionIds != null && request.ToolActionIds.Length > 0)
        {
            var toolActionGuids = request.ToolActionIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            if (toolActionGuids.Count > 0)
            {
                // Validate tools are appropriate for workspace
                await _toolValidationService.ValidateToolActionsForWorkspaceAsync(
                    request.WorkspaceId,
                    toolActionGuids,
                    cancellationToken);

                await _agentToolActionDataAccess.AssignToolActionsAsync(
                    agent.Id,
                    toolActionGuids,
                    cancellationToken);
            }
        }

        // 5. Map to DTO and return
        return await MapToDtoAsync(agent, cancellationToken);
    }

    public async Task<List<AgentDto>> GetAgentsByWorkspaceIdAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        // 1. Validate workspace membership
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            workspaceId, 
            cancellationToken);

        // 2. Retrieve agents from data access layer
        var agents = await _agentDataAccess.GetByWorkspaceIdAsync(
            workspaceId, 
            cancellationToken);

        // 3. Map to DTOs and return (with tool actions)
        var agentDtos = new List<AgentDto>();
        foreach (var agent in agents)
        {
            var dto = await MapToDtoAsync(agent, cancellationToken);
            agentDtos.Add(dto);
        }
        return agentDtos;
    }

    public async Task<AgentDto> GetAgentByIdAsync(Guid userId, Guid agentId, CancellationToken cancellationToken = default)
    {
        // 1. Retrieve agent from data access layer
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);

        // 2. Throw exception if agent doesn't exist
        if (agent == null)
        {
            throw new AgentNotFoundException(agentId);
        }

        // 3. Validate workspace membership
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            agent.WorkspaceId, 
            cancellationToken);

        // 4. Map to DTO and return
        return await MapToDtoAsync(agent, cancellationToken);
    }

    public async Task<AgentDto> UpdateAgentAsync(Guid userId, Guid agentId, UpdateAgentRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Retrieve agent from data access layer
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);

        // 2. Throw exception if agent doesn't exist
        if (agent == null)
        {
            throw new AgentNotFoundException(agentId);
        }

        // 3. Validate workspace membership
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            agent.WorkspaceId, 
            cancellationToken);

        // 4. Update agent profile with provided values (partial update)
        var updatedName = request.Name ?? agent.Name;
        var updatedRole = request.Role ?? agent.Role;
        var updatedCapabilities = request.Capabilities ?? agent.Capabilities;
        var updatedCustomInstructions = request.CustomInstructions ?? agent.CustomInstructions;

        agent.UpdateProfile(
            updatedName,
            updatedRole,
            updatedCapabilities,
            updatedCustomInstructions);

        // 5. Persist changes using data access layer
        await _agentDataAccess.UpdateAsync(agent, cancellationToken);

        // 6. Update tool action assignments if provided
        if (request.ToolActionIds != null)
        {
            // Remove all existing tool actions
            await _agentToolActionDataAccess.RemoveAllToolActionsAsync(agentId, cancellationToken);

            // Assign new tool actions if any provided
            if (request.ToolActionIds.Length > 0)
            {
                var toolActionGuids = request.ToolActionIds
                    .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                    .Where(guid => guid != Guid.Empty)
                    .ToList();

                if (toolActionGuids.Count > 0)
                {
                    // Validate tools are appropriate for workspace
                    await _toolValidationService.ValidateToolActionsForWorkspaceAsync(
                        agent.WorkspaceId,
                        toolActionGuids,
                        cancellationToken);

                    await _agentToolActionDataAccess.AssignToolActionsAsync(
                        agentId,
                        toolActionGuids,
                        cancellationToken);
                }
            }
        }

        // 7. Map to DTO and return
        return await MapToDtoAsync(agent, cancellationToken);
    }

    public async Task DeleteAgentAsync(Guid userId, Guid agentId, CancellationToken cancellationToken = default)
    {
        // 1. Retrieve agent from data access layer
        var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);

        // 2. Throw exception if agent doesn't exist
        if (agent == null)
        {
            throw new AgentNotFoundException(agentId);
        }

        // 3. Validate workspace membership
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            agent.WorkspaceId, 
            cancellationToken);

        // 4. Delete agent via data access layer
        await _agentDataAccess.DeleteAsync(agentId, cancellationToken);
    }

    private async Task<AgentDto> MapToDtoAsync(Agent agent, CancellationToken cancellationToken)
    {
        // Query tool action IDs using explicit join
        var toolActionIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(
            agent.Id,
            cancellationToken);

        // Query unique category names for display
        var toolCategories = await _agentToolActionDataAccess.GetUniqueCategoryNamesByAgentIdAsync(
            agent.Id,
            cancellationToken);

        return new AgentDto(
            Id: agent.Id.ToString(),
            WorkspaceId: agent.WorkspaceId.ToString(),
            Name: agent.Name,
            Role: agent.Role,
            Status: agent.Status switch
            {
                Domain.Enums.AgentStatus.Idle => "IDLE",
                Domain.Enums.AgentStatus.Busy => "BUSY",
                Domain.Enums.AgentStatus.Offline => "OFFLINE",
                _ => throw new ArgumentOutOfRangeException(nameof(agent.Status), agent.Status, null)
            },
            Capabilities: agent.Capabilities.ToArray(),
            ToolActionIds: toolActionIds.Select(id => id.ToString()).ToArray(),
            ToolCategories: toolCategories.ToArray(),
            AvatarUrl: agent.AvatarUrl,
            CustomInstructions: agent.CustomInstructions
        );
    }
}