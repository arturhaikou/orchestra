using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Service for validating tool assignments against workspace integration constraints.
/// </summary>
public class ToolValidationService : IToolValidationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ToolValidationService> _logger;

    public ToolValidationService(
        AppDbContext context,
        ILogger<ToolValidationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ValidateToolActionsForWorkspaceAsync(
        Guid workspaceId,
        IEnumerable<Guid> toolActionIds,
        CancellationToken cancellationToken = default)
    {
        var toolActionIdList = toolActionIds.ToList();
        
        if (!toolActionIdList.Any())
        {
            return; // No tools to validate
        }

        _logger.LogDebug(
            "Validating {Count} tool action(s) for workspace {WorkspaceId}",
            toolActionIdList.Count,
            workspaceId);

        // Get connected integration provider types for this workspace
        var connectedProviderTypes = await _context.Integrations
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId && i.IsActive)
            .Select(i => i.Provider)
            .Distinct()
            .ToListAsync(cancellationToken);

        // INTERNAL tools are always allowed regardless of integrations
        var allowedProviderTypes = new List<ProviderType>(connectedProviderTypes)
        {
            ProviderType.INTERNAL
        };

        _logger.LogDebug(
            "Workspace {WorkspaceId} has allowed provider types: {ProviderTypes}",
            workspaceId,
            string.Join(", ", allowedProviderTypes));

        // Get tool actions with their categories
        var toolActionsWithCategories = await _context.ToolActions
            .AsNoTracking()
            .Where(ta => toolActionIdList.Contains(ta.Id))
            .Join(
                _context.ToolCategories,
                ta => ta.ToolCategoryId,
                tc => tc.Id,
                (ta, tc) => new
                {
                    ToolActionId = ta.Id,
                    ToolActionName = ta.Name,
                    CategoryName = tc.Name,
                    ProviderType = tc.ProviderType
                })
            .ToListAsync(cancellationToken);

        // Check if all requested tool actions were found
        var foundToolActionIds = toolActionsWithCategories.Select(t => t.ToolActionId).ToHashSet();
        var missingToolActionIds = toolActionIdList.Where(id => !foundToolActionIds.Contains(id)).ToList();

        if (missingToolActionIds.Any())
        {
            _logger.LogWarning(
                "Tool action IDs not found: {MissingIds}",
                string.Join(", ", missingToolActionIds));
            
            throw new InvalidToolAssignmentException(
                workspaceId,
                $"Tool action IDs not found: {string.Join(", ", missingToolActionIds)}");
        }

        // Validate each tool action's provider type
        var invalidTools = toolActionsWithCategories
            .Where(t => !allowedProviderTypes.Contains(t.ProviderType))
            .ToList();

        if (invalidTools.Any())
        {
            var invalidToolNames = invalidTools
                .Select(t => $"{t.CategoryName}.{t.ToolActionName} ({t.ProviderType})")
                .ToList();

            _logger.LogWarning(
                "Invalid tool assignment attempted for workspace {WorkspaceId}: {InvalidTools}",
                workspaceId,
                string.Join(", ", invalidToolNames));

            throw new InvalidToolAssignmentException(workspaceId, invalidToolNames);
        }

        _logger.LogDebug(
            "All {Count} tool action(s) validated successfully for workspace {WorkspaceId}",
            toolActionIdList.Count,
            workspaceId);
    }
}
