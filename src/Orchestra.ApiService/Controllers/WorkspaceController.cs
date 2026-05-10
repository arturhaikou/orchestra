using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Domain.Enums;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

/// <summary>
/// Controller for managing workspaces.
/// </summary>
[ApiController]
[Route("v1/workspaces")]
[Authorize]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IWorkspaceProviderService _workspaceProviderService;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly ILogger<WorkspaceController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceController"/> class.
    /// </summary>
    /// <param name="workspaceService">The workspace service.</param>
    /// <param name="workspaceProviderService">The workspace provider service.</param>
    /// <param name="workspaceAuthorizationService">The workspace authorization service.</param>
    /// <param name="logger">The logger.</param>
    public WorkspaceController(
        IWorkspaceService workspaceService,
        IWorkspaceProviderService workspaceProviderService,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        ILogger<WorkspaceController> logger)
    {
        _workspaceService = workspaceService;
        _workspaceProviderService = workspaceProviderService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    /// <param name="request">The create workspace request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created workspace.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            var workspace = await _workspaceService.CreateWorkspaceAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(CreateWorkspace), new { id = workspace.Id }, workspace);
        }
        catch (InvalidAIModelIdentifierException ex)
        {
            // Build error response detailing which models are invalid
            var errorMessage = BuildValidationErrorResponse(ex.InvalidModelsByFeature);
            return BadRequest(new ErrorResponse(errorMessage));
        }
        catch (InvalidOperationException ex)
        {
            // AI provider misconfiguration
            return StatusCode(500, new ErrorResponse($"AI provider configuration error: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            // AI provider is unreachable
            return StatusCode(500, new ErrorResponse($"Failed to validate models with AI provider: {ex.Message}"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Retrieves all workspaces where the authenticated user is a member.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of workspaces ordered alphabetically by name.</returns>
    /// <response code="200">Returns the list of workspaces.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(WorkspaceDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserWorkspaces(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId, cancellationToken);
        return Ok(workspaces);
    }

    /// <summary>
    /// Updates a workspace name. Only the workspace owner can perform this operation.
    /// </summary>
    /// <param name="id">The workspace ID.</param>
    /// <param name="request">The update request with new workspace name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated workspace.</returns>
    /// <response code="200">Workspace updated successfully.</response>
    /// <response code="400">Invalid workspace name.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the workspace owner.</response>
    /// <response code="404">Workspace not found.</response>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(WorkspaceDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<ActionResult<WorkspaceDto>> UpdateWorkspace(
        Guid id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user ID from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            var result = await _workspaceService.UpdateWorkspaceAsync(
                userId,
                id,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidAIModelIdentifierException ex)
        {
            // Build error response detailing which models are invalid
            var errorMessage = BuildValidationErrorResponse(ex.InvalidModelsByFeature);
            return BadRequest(new ErrorResponse(errorMessage));
        }
        catch (InvalidOperationException ex)
        {
            // AI provider misconfiguration
            return StatusCode(500, new ErrorResponse($"AI provider configuration error: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            // AI provider is unreachable
            return StatusCode(500, new ErrorResponse($"Failed to validate models with AI provider: {ex.Message}"));
        }
        catch (WorkspaceNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Deletes a workspace. Only the workspace owner can delete.
    /// </summary>
    /// <param name="id">The workspace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content if successful.</returns>
    /// <response code="204">Workspace deleted successfully.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">User is not the workspace owner.</response>
    /// <response code="404">Workspace not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> DeleteWorkspace(Guid id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid user token"));
        }

        try
        {
            await _workspaceService.DeleteWorkspaceAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (WorkspaceNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Returns the AI model names currently available from the workspace's configured provider.
    /// </summary>
    /// <param name="workspaceId">The workspace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A flat list of model name strings.</returns>
    /// <response code="200">Returns the list of available model names.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">The workspace does not exist or the authenticated user is not a member.</response>
    /// <response code="409">The workspace has no AI provider configured.</response>
    /// <response code="502">The configured AI provider could not be reached or returned an error.</response>
    [HttpGet("{workspaceId}/provider/models")]
    [ProducesResponseType(typeof(AIModelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetProviderModels(
        [FromRoute] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            // Membership check: non-members receive 404 (not 403) to prevent
            // workspace-existence disclosure — consistent with the workspace module policy.
            await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
                userId, workspaceId, cancellationToken);

            var models = await _workspaceProviderService.GetAvailableModelsAsync(
                workspaceId, cancellationToken);

            return Ok(new AIModelsResponse(models));
        }
        catch (UnauthorizedWorkspaceAccessException)
        {
            // Return 404 — not 403 — to prevent workspace-existence disclosure.
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // Workspace exists and user is a member, but no provider is configured.
            return Conflict(new { error = "This workspace has no AI provider configured." });
        }
        catch (AIProviderCommunicationException)
        {
            // Provider unreachable, auth failure, TLS error, or non-success response.
            // The raw exception message (which could contain diagnostic detail) must NOT
            // be forwarded to the caller — use only the generic sanitised message below.
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "The configured AI provider could not be reached or returned an error. Verify your workspace provider configuration." });
        }
    }

    /// <summary>
    /// Probes the workspace's stored AI provider credentials and returns a structured validation result.
    /// </summary>
    /// <param name="workspaceId">The workspace whose provider configuration should be validated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>200 OK</c> with a <see cref="ProviderValidationResult"/> payload on both connectivity
    /// success and connectivity failure. The <c>isValid</c> field distinguishes the two cases.
    /// </returns>
    /// <response code="200">
    /// Validation probe completed. Check <c>isValid</c> in the response body to determine
    /// whether the provider is reachable.
    /// </response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">
    /// The workspace does not exist, the authenticated user is not a member,
    /// or the workspace has no stored AI provider configuration.
    /// </response>
    [HttpPost("{workspaceId}/provider/validate")]
    [ProducesResponseType(typeof(ProviderValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateProvider(
        [FromRoute] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            // Membership check: non-members receive 404 — not 403 — to prevent
            // workspace-existence disclosure, consistent with all workspace-scoped endpoints.
            await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
                userId, workspaceId, cancellationToken);

            var result = await _workspaceProviderService.ValidateProviderAsync(
                workspaceId, cancellationToken);

            // null signals "no AIProviderConfiguration stored" — map to 404 Not Found.
            if (result is null)
            {
                return NotFound();
            }

            // Return 200 OK for both isValid: true and isValid: false.
            // A non-200 would indicate the probe itself failed; here the probe completed.
            return Ok(result);
        }
        catch (UnauthorizedWorkspaceAccessException)
        {
            // Return 404 — not 403 — to prevent workspace-existence disclosure.
            return NotFound();
        }
    }

    /// <summary>
    /// Replaces the AI provider configuration for the specified workspace.
    /// Validates the incoming credentials against the live provider before any data is persisted.
    /// </summary>
    /// <param name="id">The workspace ID.</param>
    /// <param name="request">The reconfiguration request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>204 No Content</c> on success.</returns>
    /// <response code="204">Provider reconfigured successfully.</response>
    /// <response code="400">Request body is missing required fields or contains contradictory fields.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="403">The authenticated user is a workspace member but not the owner.</response>
    /// <response code="404">The workspace does not exist or the authenticated user is not a member.</response>
    /// <response code="422">
    /// The live credential probe failed, or the supplied defaultModelId is not in the model list.
    /// </response>
    [HttpPut("{id}/provider")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReconfigureProvider(
        [FromRoute] Guid id,
        [FromBody] ReconfigureProviderRequest request,
        CancellationToken cancellationToken)
    {
        // ── 1. Extract user ID from JWT ───────────────────────────────────────
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // ── 2. Validate request body ──────────────────────────────────────────
        // ProviderType must be present and parseable.
        if (string.IsNullOrWhiteSpace(request.ProviderType) ||
            !Enum.TryParse<AIProviderType>(request.ProviderType, ignoreCase: true, out var providerType))
        {
            return BadRequest(new ErrorResponse(
                "providerType is required. Accepted values: 'AzureOpenAI', 'Ollama'."));
        }

        // defaultModelId is always required.
        if (string.IsNullOrWhiteSpace(request.DefaultModelId))
        {
            return BadRequest(new ErrorResponse("defaultModelId is required."));
        }

        // Provider-type-specific field consistency checks.
        if (providerType == AIProviderType.AzureOpenAI)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint))
                return BadRequest(new ErrorResponse("endpoint is required for AzureOpenAI provider."));

            if (string.IsNullOrWhiteSpace(request.ApiKey))
                return BadRequest(new ErrorResponse("apiKey is required for AzureOpenAI provider."));
        }
        else if (providerType == AIProviderType.Ollama)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint))
                return BadRequest(new ErrorResponse("endpoint is required for Ollama provider."));

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
                return BadRequest(new ErrorResponse(
                    "apiKey must be absent or null when providerType is Ollama."));
        }

        try
        {
            // ── 3. Two-stage authorization ────────────────────────────────────
            // Stage 1: non-members → 404 (workspace existence not disclosed).
            // Stage 2: members who are not owners → 403.
            await _workspaceAuthorizationService.EnsureUserIsOwnerAsync(
                userId, id, cancellationToken);

            // ── 4. Delegate to service ────────────────────────────────────────
            await _workspaceProviderService.ReconfigureProviderAsync(
                id,
                providerType,
                request.Endpoint,
                request.ApiKey,
                request.DefaultModelId!,
                cancellationToken);

            // ── 5. Success ────────────────────────────────────────────────────
            return NoContent();
        }
        catch (UnauthorizedWorkspaceAccessException)
        {
            // Non-member — return 404 to prevent workspace-existence disclosure.
            return NotFound();
        }
        catch (WorkspaceForbiddenException)
        {
            // Member but not owner.
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse("You do not have permission to reconfigure the provider for this workspace."));
        }
        catch (ProviderReconfigurationException ex)
        {
            // Credential probe failed or defaultModelId not in model list.
            // ex.Message is already sanitised — it contains no credential values.
            return UnprocessableEntity(new ErrorResponse(ex.Message));
        }
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
        return Guid.Parse(userIdClaim);
    }

    /// <summary>
    /// Builds a human-readable error message from invalid AI model identifiers.
    /// </summary>
    /// <param name="invalidModelsByFeature">Dictionary mapping feature names to invalid model IDs</param>
    /// <returns>Formatted error message</returns>
    private string BuildValidationErrorResponse(IReadOnlyDictionary<string, string> invalidModelsByFeature)
    {
        var violations = string.Join(
            " | ",
            invalidModelsByFeature.Select(kvp => $"The model '{kvp.Value}' specified for {kvp.Key} is not available."));

        return violations;
    }
}