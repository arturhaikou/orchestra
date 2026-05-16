using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.Services;

public class AgentService : IAgentService
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IAgentMcpToolDataAccess _agentMcpToolDataAccess;
    private readonly IAgentSubAgentDataAccess _agentSubAgentDataAccess;
    private readonly IAgentSkillDataAccess _agentSkillDataAccess;
    private readonly ISkillDataAccess _skillDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IToolValidationService _toolValidationService;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly ITemplateAvailabilityResolver _availabilityResolver;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IAgentSubAgentAssignmentService _subAgentAssignmentService;

    public AgentService(
        IAgentDataAccess agentDataAccess,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        IAgentMcpToolDataAccess agentMcpToolDataAccess,
        IAgentSubAgentDataAccess agentSubAgentDataAccess,
        IAgentSkillDataAccess agentSkillDataAccess,
        ISkillDataAccess skillDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IToolValidationService toolValidationService,
        IBuiltInAgentTemplateRegistry templateRegistry,
        ITemplateAvailabilityResolver availabilityResolver,
        IToolActionDataAccess toolActionDataAccess,
        IIntegrationDataAccess integrationDataAccess,
        IAgentSubAgentAssignmentService subAgentAssignmentService)
    {
        _agentDataAccess = agentDataAccess ?? throw new ArgumentNullException(nameof(agentDataAccess));
        _agentToolActionDataAccess = agentToolActionDataAccess ?? throw new ArgumentNullException(nameof(agentToolActionDataAccess));
        _agentMcpToolDataAccess = agentMcpToolDataAccess ?? throw new ArgumentNullException(nameof(agentMcpToolDataAccess));
        _agentSubAgentDataAccess = agentSubAgentDataAccess ?? throw new ArgumentNullException(nameof(agentSubAgentDataAccess));
        _agentSkillDataAccess = agentSkillDataAccess ?? throw new ArgumentNullException(nameof(agentSkillDataAccess));
        _skillDataAccess = skillDataAccess ?? throw new ArgumentNullException(nameof(skillDataAccess));
        _workspaceAuthorizationService = workspaceAuthorizationService ?? throw new ArgumentNullException(nameof(workspaceAuthorizationService));
        _toolValidationService = toolValidationService ?? throw new ArgumentNullException(nameof(toolValidationService));
        _templateRegistry = templateRegistry ?? throw new ArgumentNullException(nameof(templateRegistry));
        _availabilityResolver = availabilityResolver ?? throw new ArgumentNullException(nameof(availabilityResolver));
        _toolActionDataAccess = toolActionDataAccess ?? throw new ArgumentNullException(nameof(toolActionDataAccess));
        _integrationDataAccess = integrationDataAccess ?? throw new ArgumentNullException(nameof(integrationDataAccess));
        _subAgentAssignmentService = subAgentAssignmentService ?? throw new ArgumentNullException(nameof(subAgentAssignmentService));
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

        // 6.5. Assign MCP tools if provided
        if (request.McpSelections != null && request.McpSelections.Count > 0)
        {
            foreach (var selection in request.McpSelections)
            {
                var tools = selection.ToolNames
                    .Select(name => AgentMcpTool.Create(agent.Id, selection.McpServerId, name))
                    .ToList();

                await _agentMcpToolDataAccess.ReplaceForAgentAndServerAsync(
                    agent.Id,
                    selection.McpServerId,
                    tools,
                    cancellationToken);
            }
        }

        // 6.6. Assign sub-agents if provided
        if (request.SubAgentIds != null && request.SubAgentIds.Length > 0)
        {
            var subAgentGuids = request.SubAgentIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            if (subAgentGuids.Count > 0)
                await _subAgentAssignmentService.AssignSubAgentsAsync(
                    agent.Id,
                    subAgentGuids,
                    request.WorkspaceId,
                    cancellationToken);
        }

        // 6.7. Assign skills if provided
        if (request.SkillIds != null && request.SkillIds.Length > 0)
        {
            var skillGuids = request.SkillIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            if (skillGuids.Count > 0)
            {
                await ValidateSkillsBelongToWorkspaceAsync(skillGuids, request.WorkspaceId, cancellationToken);
                await _agentSkillDataAccess.AssignSkillsAsync(agent.Id, skillGuids, cancellationToken);
            }
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

        // 3. Pre-resolve provider label once for the entire workspace
        var hasTemplateAgents = agents.Any(a => a.TemplateIdentifier is not null);
        string? preResolvedLabel = null;
        if (hasTemplateAgents)
            preResolvedLabel = await ResolveProviderLabelAsync(workspaceId, cancellationToken);

        // 4. Batch-load all sub-agent assignments for the workspace in a single query
        var subAgentMap = await _agentSubAgentDataAccess.GetSubAgentIdsByWorkspaceIdAsync(
            workspaceId, cancellationToken) ?? new Dictionary<Guid, List<Guid>>();

        var agentDtos = new List<AgentDto>();
        foreach (var agent in agents)
        {
            subAgentMap.TryGetValue(agent.Id, out var subAgentIds);
            var dto = await MapToDtoAsync(agent, cancellationToken, preResolvedLabel, subAgentIds);
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

        EnsureNoLockedFieldViolations(agent, request);

        // 5. Determine instructions mode for updated profile
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

        // 6. Apply profile update with named parameters
        agent.UpdateProfile(
            updatedName,
            updatedRole,
            updatedCapabilities,
            customInstructions: customInstructionsForEntity,
            projectPrinciples: projectPrinciplesForEntity);

        // 6b. Apply model update if the field was explicitly included in the request
        if (request.Model.HasValue)
        {
            agent.SetModel(request.Model.Value);
        }

        // 7. Persist changes using data access layer
        await _agentDataAccess.UpdateAsync(agent, cancellationToken);

        // 8. Update tool action assignments if provided
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

        // 8b. Update sub-agent assignments if provided (null = no-op, empty = remove all)
        if (request.SubAgentIds != null)
        {
            await _agentSubAgentDataAccess.RemoveAllSubAgentsAsync(agentId, cancellationToken);

            if (request.SubAgentIds.Length > 0)
            {
                var subAgentGuids = request.SubAgentIds
                    .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                    .Where(guid => guid != Guid.Empty)
                    .ToList();

                if (subAgentGuids.Count > 0)
                    await _subAgentAssignmentService.AssignSubAgentsAsync(
                        agentId,
                        subAgentGuids,
                        agent.WorkspaceId,
                        cancellationToken);
            }
        }

        // 8c. Update skill assignments if provided (null = no-op, empty = remove all)
        if (request.SkillIds != null)
        {
            var skillGuids = request.SkillIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            if (skillGuids.Count > 0)
                await ValidateSkillsBelongToWorkspaceAsync(skillGuids, agent.WorkspaceId, cancellationToken);

            await _agentSkillDataAccess.ReplaceSkillsAsync(agentId, skillGuids, cancellationToken);
        }

        // 9. Map to DTO and return
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

    public async Task<AgentDto> CreateFromTemplateAsync(Guid userId, CreateAgentFromTemplateRequest request, CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, cancellationToken);

        var template = _templateRegistry.GetByIdentifier(request.TemplateId)
            ?? throw new TemplateNotFoundException(request.TemplateId);

        if (!template.IsCliAgent)
            ValidateProjectPrinciples(request.ProjectPrinciples);

        await _availabilityResolver.ValidatePrerequisitesAsync(request.WorkspaceId, template, cancellationToken);

        var agent = CreateAgentFromTemplate(request, template);

        if (template.IsCliAgent && !request.AiCliIntegrationId.HasValue)
            throw new ArgumentException("A CLI integration must be selected to deploy this agent.");

        if (template.IsCliAgent && request.AiCliIntegrationId.HasValue)
            agent.SetAiCliIntegrationId(request.AiCliIntegrationId.Value);

        await _agentDataAccess.AddAsync(agent, cancellationToken);

        if (!template.IsCliAgent)
        {
            var toolActionIds = await ResolveToolActionIdsFromProviders(request.WorkspaceId, template, cancellationToken);
            await ValidateAndAssignToolActions(request.WorkspaceId, agent.Id, toolActionIds, cancellationToken);
        }

        return await MapToDtoAsync(agent, cancellationToken);
    }

    private static void EnsureNoLockedFieldViolations(Agent agent, UpdateAgentRequest request)
    {
        if (agent.TemplateIdentifier is null)
            return;

        var lockedViolations = new List<string>();

        if (request.Name is not null && !string.Equals(request.Name.Trim(), agent.Name, StringComparison.Ordinal))
            lockedViolations.Add("name");

        if (request.Role is not null && !string.Equals(request.Role.Trim(), agent.Role, StringComparison.Ordinal))
            lockedViolations.Add("role");

        if (request.Capabilities is not null)
        {
            var currentSet = agent.Capabilities.OrderBy(c => c).ToList();
            var requestSet = request.Capabilities.OrderBy(c => c).ToList();
            if (!currentSet.SequenceEqual(requestSet))
                lockedViolations.Add("capabilities");
        }

        if (request.ToolActionIds is not null)
            lockedViolations.Add("tools");

        if (lockedViolations.Count > 0)
        {
            var fieldList = string.Join(", ", lockedViolations);
            throw new ArgumentException(
                $"Cannot modify locked fields on a template-based agent. Locked fields: {fieldList}.");
        }
    }

    private static void ValidateProjectPrinciples(string projectPrinciples)
    {
        if (string.IsNullOrWhiteSpace(projectPrinciples))
            throw new ArgumentException("Project principles are required.");
    }

    private async Task<List<Guid>> ResolveToolActionIdsFromProviders(Guid workspaceId, BuiltInAgentTemplate template, CancellationToken cancellationToken)
    {
        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var codeSourceIntegrations = integrations.Where(i => i.Types.Contains(IntegrationType.CODE_SOURCE)).ToList();

        var methodNames = ResolveMethodNames(codeSourceIntegrations, template);
        var toolActions = await _toolActionDataAccess.GetByNamesAsync(methodNames, cancellationToken);

        return toolActions.Select(ta => ta.Id).ToList();
    }

    private static List<string> ResolveMethodNames(List<Integration> codeSourceIntegrations, BuiltInAgentTemplate template)
    {
        return codeSourceIntegrations
            .Where(i => template.ProviderToolMethodMap.ContainsKey(i.Provider))
            .Select(i => template.ProviderToolMethodMap[i.Provider])
            .Distinct()
            .ToList();
    }

    private static Agent CreateAgentFromTemplate(CreateAgentFromTemplateRequest request, BuiltInAgentTemplate template)
    {
        return Agent.Create(
            workspaceId: request.WorkspaceId,
            name: template.DisplayName,
            role: template.Role,
            capabilities: template.Capabilities,
            customInstructions: template.IsCliAgent ? template.DefaultCustomInstructions : null,
            projectPrinciples: template.IsCliAgent ? null : request.ProjectPrinciples,
            model: request.Model,
            templateIdentifier: template.Identifier,
            templateVersion: template.Version);
    }

    private async Task ValidateAndAssignToolActions(Guid workspaceId, Guid agentId, List<Guid> toolActionIds, CancellationToken cancellationToken)
    {
        if (toolActionIds.Count == 0)
            return;

        await _toolValidationService.ValidateToolActionsForWorkspaceAsync(workspaceId, toolActionIds, cancellationToken);
        await _agentToolActionDataAccess.AssignToolActionsAsync(agentId, toolActionIds, cancellationToken);
    }

    private async Task<AgentDto> MapToDtoAsync(
        Agent agent,
        CancellationToken cancellationToken,
        string? preResolvedLabel = null,
        List<Guid>? preloadedSubAgentIds = null)
    {
        var toolActionIds = await _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(
            agent.Id, cancellationToken);

        var toolCategories = await _agentToolActionDataAccess.GetUniqueCategoryNamesByAgentIdAsync(
            agent.Id, cancellationToken);

        var mcpServerNames = await _agentMcpToolDataAccess.GetMcpServerNamesByAgentIdAsync(
            agent.Id, cancellationToken);

        var subAgentIds = preloadedSubAgentIds
            ?? await _agentSubAgentDataAccess.GetSubAgentIdsByParentAgentIdAsync(agent.Id, cancellationToken)
            ?? [];

        var skills = await _agentSkillDataAccess.GetSkillsByAgentIdAsync(agent.Id, cancellationToken) ?? [];
        var skillDtos = skills.Select(s => new SkillDto(
            Id: s.Id.ToString(),
            WorkspaceId: s.WorkspaceId.ToString(),
            Name: s.Name,
            Description: s.Description,
            Instructions: s.Instructions,
            CreatedAt: s.CreatedAt,
            UpdatedAt: s.UpdatedAt
        )).ToList();

        var guide = await ResolveGuideAsync(agent, cancellationToken, preResolvedLabel);

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
            McpServerNames: mcpServerNames,
            SubAgentIds: subAgentIds.Select(id => id.ToString()).ToArray(),
            AvatarUrl: agent.AvatarUrl,
            CustomInstructions: agent.CustomInstructions,
            ProjectPrinciples: agent.ProjectPrinciples,
            Model: agent.Model,
            TemplateIdentifier: agent.TemplateIdentifier,
            TemplateVersion: agent.TemplateVersion,
            IsBuiltIn: agent.TemplateIdentifier != null,
            Guide: guide,
            AiCliIntegrationId: agent.AiCliIntegrationId?.ToString(),
            Skills: skillDtos
        );
    }

    private async Task<string?> ResolveGuideAsync(
        Agent agent,
        CancellationToken cancellationToken,
        string? preResolvedLabel = null)
    {
        if (agent.TemplateIdentifier is null)
            return null;

        var template = _templateRegistry.GetByIdentifier(agent.TemplateIdentifier);
        if (template?.GuideTemplate is null)
            return null;

        var label = preResolvedLabel ?? await ResolveProviderLabelAsync(agent.WorkspaceId, cancellationToken);
        return template.GuideTemplate.Replace("{providerLabel}", label);
    }

    private async Task<string> ResolveProviderLabelAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);

        var codeSourceIntegration = integrations.FirstOrDefault(
            i => i.Types.Contains(IntegrationType.CODE_SOURCE));

        if (codeSourceIntegration is null)
            return "Pull Request";

        return codeSourceIntegration.Provider == ProviderType.GITLAB
            ? "Merge Request"
            : "Pull Request";
    }

    public async Task<List<AgentTemplateDto>> GetAgentTemplatesAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var resolvedTemplates = await _availabilityResolver.ResolveAvailabilityAsync(userId, workspaceId, cancellationToken);

        return resolvedTemplates.Select(MapToTemplateDto).ToList();
    }

    private AgentTemplateDto MapToTemplateDto(ResolvedTemplate resolved)
    {
        var template = _templateRegistry.GetByIdentifier(resolved.TemplateId);

        var toolLabel = resolved.ProviderLabels.Count > 0
            ? string.Join(" / ", resolved.ProviderLabels.Select(p => p.Label).Distinct())
            : template?.ProviderLabelMap != null
                ? string.Join(" / ", template.ProviderLabelMap.Values.Distinct())
                : string.Empty;

        return new AgentTemplateDto(
            TemplateId: resolved.TemplateId,
            Name: template?.DisplayName ?? resolved.TemplateId,
            Role: template?.Role ?? string.Empty,
            Description: string.Empty,
            Prerequisites: MapPrerequisites(resolved),
            Availability: MapAvailability(resolved),
            Capabilities: template?.Capabilities ?? Array.Empty<string>(),
            ToolLabel: toolLabel,
            UsageGuide: resolved.ResolvedGuide ?? string.Empty,
            TemplateVersion: template?.Version ?? 0,
            IsCliAgent: template?.IsCliAgent ?? false,
            EditableFields: template?.EditableFields ?? Array.Empty<string>());
    }

    private static IReadOnlyList<TemplatePrerequisiteDto> MapPrerequisites(ResolvedTemplate resolved)
    {
        return resolved.ProviderLabels
            .Select(p => new TemplatePrerequisiteDto(
                IntegrationType: p.ProviderType.ToString(),
                ProviderName: p.Label,
                Satisfied: true))
            .ToList();
    }

    private static TemplateAvailabilityDto MapAvailability(ResolvedTemplate resolved)
    {
        var status = resolved.Status switch
        {
            TemplateAvailabilityStatus.Available => "AVAILABLE",
            TemplateAvailabilityStatus.Unavailable => "UNAVAILABLE",
            TemplateAvailabilityStatus.AlreadyDeployed => "ALREADY_DEPLOYED",
            TemplateAvailabilityStatus.Error => "ERROR",
            _ => resolved.Status.ToString().ToUpperInvariant()
        };

        return new TemplateAvailabilityDto(
            Status: status,
            Reason: resolved.UnavailabilityReason,
            ExistingAgentId: resolved.ExistingAgentId);
    }

    private async Task ValidateSkillsBelongToWorkspaceAsync(
        IReadOnlyList<Guid> skillIds,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        foreach (var skillId in skillIds)
        {
            var exists = await _skillDataAccess.ExistsInWorkspaceAsync(skillId, workspaceId, cancellationToken);
            if (!exists)
                throw new ArgumentException($"Skill '{skillId}' does not exist in workspace '{workspaceId}'.");
        }
    }
}