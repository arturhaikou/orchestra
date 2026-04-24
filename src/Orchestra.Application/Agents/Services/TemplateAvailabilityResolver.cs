using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.Services;

public class TemplateAvailabilityResolver : ITemplateAvailabilityResolver
{
    private readonly IIntegrationService _integrationService;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly ILogger<TemplateAvailabilityResolver> _logger;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;

    public TemplateAvailabilityResolver(
        IIntegrationService integrationService,
        IIntegrationDataAccess integrationDataAccess,
        IAgentDataAccess agentDataAccess,
        IToolActionDataAccess toolActionDataAccess,
        ILogger<TemplateAvailabilityResolver> logger,
        IBuiltInAgentTemplateRegistry templateRegistry)
    {
        _integrationService = integrationService;
        _integrationDataAccess = integrationDataAccess;
        _agentDataAccess = agentDataAccess;
        _toolActionDataAccess = toolActionDataAccess;
        _logger = logger;
        _templateRegistry = templateRegistry;
    }

    public async Task<List<ResolvedTemplate>> ResolveAvailabilityAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var integrations = await _integrationService.GetWorkspaceIntegrationsAsync(userId, workspaceId, cancellationToken);
        var deployedAgents = await _agentDataAccess.GetTemplateAgentsByWorkspaceIdAsync(workspaceId, cancellationToken);
        var templates = _templateRegistry.GetAll();

        var results = new List<ResolvedTemplate>();
        foreach (var template in templates)
        {
            var resolved = await ResolveTemplateAsync(template, integrations, deployedAgents, cancellationToken);
            results.Add(resolved);
        }

        return results;
    }

    private async Task<ResolvedTemplate> ResolveTemplateAsync(
        BuiltInAgentTemplate template,
        List<IntegrationDto> integrations,
        List<Agent> deployedAgents,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveTemplateCoreAsync(template, integrations, deployedAgents, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve template '{TemplateId}'", template.Identifier);
            return CreateErrorResult(template.Identifier);
        }
    }

    private async Task<ResolvedTemplate> ResolveTemplateCoreAsync(
        BuiltInAgentTemplate template,
        List<IntegrationDto> integrations,
        List<Agent> deployedAgents,
        CancellationToken cancellationToken)
    {
        var alreadyDeployed = CheckAlreadyDeployed(template, deployedAgents);
        if (alreadyDeployed is not null)
            return alreadyDeployed;

        var matchedProviders = FindMatchedProviders(template, integrations);
        if (matchedProviders.Count == 0)
            return CreateUnavailableResult(template);

        var resolvedToolActions = await ResolveToolActionsAsync(template, matchedProviders, cancellationToken);
        if (resolvedToolActions is null)
            return CreateErrorResult(template.Identifier);

        var providerLabels = ResolveProviderLabels(template, matchedProviders);
        var resolvedGuide = SubstituteGuidePlaceholders(template.GuideTemplate, providerLabels);

        return new ResolvedTemplate(
            template.Identifier,
            TemplateAvailabilityStatus.Available,
            UnavailabilityReason: null,
            ExistingAgentId: null,
            resolvedToolActions,
            providerLabels,
            resolvedGuide);
    }

    private static ResolvedTemplate? CheckAlreadyDeployed(BuiltInAgentTemplate template, List<Agent> deployedAgents)
    {
        var existing = deployedAgents.FirstOrDefault(a => a.TemplateIdentifier == template.Identifier);
        if (existing is null)
            return null;

        return new ResolvedTemplate(
            template.Identifier,
            TemplateAvailabilityStatus.AlreadyDeployed,
            UnavailabilityReason: null,
            ExistingAgentId: existing.Id,
            ResolvedToolActions: new List<ResolvedToolAction>(),
            ProviderLabels: new List<ProviderLabel>(),
            ResolvedGuide: null);
    }

    private static List<ProviderType> FindMatchedProviders(BuiltInAgentTemplate template, List<IntegrationDto> integrations)
    {
        var requiredType = template.RequiredIntegrationType.ToString();

        return integrations
            .Where(i => i.Types.Contains(requiredType))
            .Select(i => Enum.Parse<ProviderType>(i.Provider!, ignoreCase: true))
            .Where(p => template.ProviderToolMethodMap.ContainsKey(p))
            .Distinct()
            .ToList();
    }

    private async Task<List<ResolvedToolAction>?> ResolveToolActionsAsync(
        BuiltInAgentTemplate template,
        List<ProviderType> matchedProviders,
        CancellationToken cancellationToken)
    {
        var requiredMethodNames = matchedProviders
            .Select(p => template.ProviderToolMethodMap[p])
            .ToList();

        var toolActions = await _toolActionDataAccess.GetByNamesAsync(requiredMethodNames, cancellationToken);

        if (toolActions.Count < requiredMethodNames.Count)
        {
            LogMissingToolActions(template, requiredMethodNames, toolActions);
            return null;
        }

        return BuildResolvedToolActions(template, matchedProviders, toolActions);
    }

    private void LogMissingToolActions(
        BuiltInAgentTemplate template,
        List<string> requiredMethodNames,
        List<ToolAction> toolActions)
    {
        var missing = requiredMethodNames.Except(toolActions.Select(ta => ta.Name));
        foreach (var methodName in missing)
            _logger.LogError("Tool action '{MethodName}' not found for template '{TemplateId}'", methodName, template.Identifier);
    }

    private static List<ResolvedToolAction> BuildResolvedToolActions(
        BuiltInAgentTemplate template,
        List<ProviderType> matchedProviders,
        List<ToolAction> toolActions)
    {
        return matchedProviders.Select(provider =>
        {
            var methodName = template.ProviderToolMethodMap[provider];
            var toolAction = toolActions.First(ta => ta.Name == methodName);
            return new ResolvedToolAction(toolAction.Id, methodName, provider);
        }).ToList();
    }

    private static List<ProviderLabel> ResolveProviderLabels(BuiltInAgentTemplate template, List<ProviderType> matchedProviders)
    {
        return matchedProviders
            .Select(p => new ProviderLabel(p, template.ProviderLabelMap[p]))
            .ToList();
    }

    private static string? SubstituteGuidePlaceholders(string? guideTemplate, List<ProviderLabel> labels)
    {
        if (guideTemplate is null)
            return null;

        var combinedLabel = string.Join(" / ", labels.Select(l => l.Label));
        return guideTemplate.Replace("{providerLabel}", combinedLabel);
    }

    private static ResolvedTemplate CreateUnavailableResult(BuiltInAgentTemplate template)
    {
        var typeName = FormatIntegrationTypeName(template.RequiredIntegrationType);
        return new ResolvedTemplate(
            template.Identifier,
            TemplateAvailabilityStatus.Unavailable,
            $"Requires a {typeName} integration. Configure one in Settings → Integrations.",
            ExistingAgentId: null,
            ResolvedToolActions: new List<ResolvedToolAction>(),
            ProviderLabels: new List<ProviderLabel>(),
            ResolvedGuide: null);
    }

    private static ResolvedTemplate CreateErrorResult(string templateId)
    {
        return new ResolvedTemplate(
            templateId,
            TemplateAvailabilityStatus.Error,
            UnavailabilityReason: null,
            ExistingAgentId: null,
            ResolvedToolActions: new List<ResolvedToolAction>(),
            ProviderLabels: new List<ProviderLabel>(),
            ResolvedGuide: null);
    }

    public async Task ValidatePrerequisitesAsync(
        Guid workspaceId,
        BuiltInAgentTemplate template,
        CancellationToken cancellationToken = default)
    {
        await EnsureRequiredIntegrationActiveAsync(workspaceId, template.RequiredIntegrationType, cancellationToken);
        await EnsureTemplateNotDeployedAsync(workspaceId, template.Identifier, cancellationToken);
    }

    private async Task EnsureRequiredIntegrationActiveAsync(
        Guid workspaceId,
        IntegrationType requiredType,
        CancellationToken cancellationToken)
    {
        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var hasRequired = integrations.Any(i => i.Types.Contains(requiredType));

        if (!hasRequired)
            throw new IntegrationRequiredException(
                "A Code Source integration (GitHub or GitLab) is required to deploy this template.");
    }

    private async Task EnsureTemplateNotDeployedAsync(
        Guid workspaceId,
        string templateId,
        CancellationToken cancellationToken)
    {
        var deployedAgents = await _agentDataAccess.GetTemplateAgentsByWorkspaceIdAsync(workspaceId, cancellationToken);
        var alreadyDeployed = deployedAgents.Any(a => a.TemplateIdentifier == templateId);

        if (alreadyDeployed)
            throw new TemplateAlreadyDeployedException(templateId);
    }

    private static string FormatIntegrationTypeName(IntegrationType type)
    {
        return type.ToString().Replace("_", " ").ToLowerInvariant()
            .Split(' ')
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..])
            .Aggregate((a, b) => $"{a} {b}");
    }
}
