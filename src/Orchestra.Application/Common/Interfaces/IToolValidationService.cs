using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for validating tool assignments against workspace integration constraints.
/// Ensures agents only receive tools appropriate for their workspace's connected integrations.
/// </summary>
public interface IToolValidationService
{
    /// <summary>
    /// Validates that the specified tool action IDs are appropriate for the given workspace.
    /// Checks that each tool action belongs to a category whose ProviderType matches
    /// the workspace's connected integrations (or is INTERNAL).
    /// </summary>
    /// <param name="workspaceId">The workspace ID to validate against.</param>
    /// <param name="toolActionIds">The tool action IDs to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidToolAssignmentException">
    /// Thrown when one or more tool actions are not appropriate for the workspace's integrations.
    /// </exception>
    Task ValidateToolActionsForWorkspaceAsync(
        Guid workspaceId,
        IEnumerable<Guid> toolActionIds,
        CancellationToken cancellationToken = default);
}
