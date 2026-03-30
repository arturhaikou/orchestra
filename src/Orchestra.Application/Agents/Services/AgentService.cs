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

        // 2. Resolve tool action GUIDs and detect review tool presence
        List<Guid> toolActionGuids = new();
        if (request.ToolActionIds != null && request.ToolActionIds.Length > 0)
        {
            toolActionGuids = request.ToolActionIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();
        }

        bool hasReviewTool = toolActionGuids.Count > 0
            && await _agentToolActionDataAccess.ContainsReviewToolActionAsync(toolActionGuids, cancellationToken);

        // 3. Enforce mutual exclusivity (service-layer rule per FR-03 design)
        string? customInstructionsForEntity;
        string? projectPrinciplesForEntity;

        if (hasReviewTool)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectPrinciples))
                throw new ArgumentException("Project principles are required when a code review tool is assigned.");

            if (!string.IsNullOrWhiteSpace(request.CustomInstructions))
                throw new ArgumentException("Custom instructions and project principles cannot both be provided.");

            customInstructionsForEntity = null;
            projectPrinciplesForEntity = request.ProjectPrinciples;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.CustomInstructions))
                throw new ArgumentException("CustomInstructions cannot be null or empty.", nameof(request.CustomInstructions));

            customInstructionsForEntity = request.CustomInstructions;
            projectPrinciplesForEntity = null; // silently discard if supplied without a review tool
        }

        // 4. Create agent entity using domain factory
        var agent = Agent.Create(
            request.WorkspaceId,
            request.Name,
            request.Role,
            request.Capabilities?.ToList() ?? new List<string>(),
            customInstructions: customInstructionsForEntity,
            projectPrinciples: projectPrinciplesForEntity,
            model: request.Model);

        // 5. Persist using data access layer
        await _agentDataAccess.AddAsync(agent, cancellationToken);

        // 6. Validate and assign tool actions if provided
        if (toolActionGuids.Count > 0)
        {
            await _toolValidationService.ValidateToolActionsForWorkspaceAsync(
                request.WorkspaceId,
                toolActionGuids,
                cancellationToken);

            await _agentToolActionDataAccess.AssignToolActionsAsync(
                agent.Id,
                toolActionGuids,
                cancellationToken);
        }

        // 7. Map to DTO and return
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

        // 4. Determine instructions mode for updated profile
        var updatedName = request.Name ?? agent.Name;
        var updatedRole = request.Role ?? agent.Role;
        var updatedCapabilities = request.Capabilities ?? agent.Capabilities;

        string? customInstructionsForEntity;
        string? projectPrinciplesForEntity;

        if (request.ToolActionIds != null)
        {
            // Tool assignments are explicitly changing — re-evaluate instructions mode
            var toolActionGuids = request.ToolActionIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            bool hasReviewTool = toolActionGuids.Count > 0
                && await _agentToolActionDataAccess.ContainsReviewToolActionAsync(toolActionGuids, cancellationToken);

            if (hasReviewTool)
            {
                // Switching to / staying in review mode
                var newProjectPrinciples = request.ProjectPrinciples ?? agent.ProjectPrinciples;
                if (string.IsNullOrWhiteSpace(newProjectPrinciples))
                    throw new ArgumentException("Project principles are required when a code review tool is assigned.");

                customInstructionsForEntity = null;
                projectPrinciplesForEntity = newProjectPrinciples;
            }
            else
            {
                // Switching to / staying in non-review mode
                var newCustomInstructions = request.CustomInstructions ?? agent.CustomInstructions;
                if (string.IsNullOrWhiteSpace(newCustomInstructions))
                    throw new ArgumentException("CustomInstructions cannot be null or empty.", nameof(request.CustomInstructions));

                customInstructionsForEntity = newCustomInstructions;
                projectPrinciplesForEntity = null; // clear ProjectPrinciples when review tool removed
            }
        }
        else
        {
            // Tool assignments not changing — preserve current instructions mode
            if (agent.ProjectPrinciples != null)
            {
                // Agent is currently a review agent — preserve/update ProjectPrinciples
                var newProjectPrinciples = request.ProjectPrinciples ?? agent.ProjectPrinciples;
                customInstructionsForEntity = null;
                projectPrinciplesForEntity = newProjectPrinciples;
            }
            else
            {
                // Agent is currently a non-review agent — preserve/update CustomInstructions
                var newCustomInstructions = request.CustomInstructions ?? agent.CustomInstructions;
                if (string.IsNullOrWhiteSpace(newCustomInstructions))
                    throw new ArgumentException("CustomInstructions cannot be null or empty.", nameof(request.CustomInstructions));

                customInstructionsForEntity = newCustomInstructions;
                projectPrinciplesForEntity = null;
            }
        }

        // 5. Apply profile update with named parameters
        agent.UpdateProfile(
            updatedName,
            updatedRole,
            updatedCapabilities,
            customInstructions: customInstructionsForEntity,
            projectPrinciples: projectPrinciplesForEntity);

        // 5b. Apply model update if the field was explicitly included in the request
        if (request.Model.HasValue)
        {
            agent.SetModel(request.Model.Value);
        }

        // 6. Persist changes using data access layer
        await _agentDataAccess.UpdateAsync(agent, cancellationToken);

        // 7. Update tool action assignments if provided
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

        // 8. Map to DTO and return
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
            CustomInstructions: agent.CustomInstructions,
            ProjectPrinciples: agent.ProjectPrinciples,
            Model: agent.Model
        );
    }
}