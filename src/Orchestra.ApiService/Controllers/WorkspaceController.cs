using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Configuration;
using System.Security.Claims;
using Microsoft.Extensions.Options;

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
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IAIModelListService _aiModelListService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceController"/> class.
    /// </summary>
    /// <param name="workspaceService">The workspace service.</param>
    /// <param name="workspaceAuthorizationService">The workspace authorization service.</param>
    /// <param name="aiModelListService">The AI model list service.</param>
    public WorkspaceController(
        IWorkspaceService workspaceService,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IAIModelListService aiModelListService)
    {
        _workspaceService = workspaceService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _aiModelListService = aiModelListService;
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
    /// Retrieves the list of available AI models for a workspace.
    /// The authenticated user must be a member of the workspace to access this endpoint.
    /// </summary>
    /// <param name="workspaceId">The workspace ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available model names from the currently-configured AI provider.</returns>
    /// <response code="200">Returns the list of available AI models.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user is not a member of the workspace.</response>
    /// <response code="500">If an unexpected error occurs while fetching models.</response>
    [HttpGet("{workspaceId}/ai/models")]
    [ProducesResponseType(typeof(AIModelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWorkspaceAIModels(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user ID from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            // Validate that the user is a member of the workspace
            await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

            // Fetch the available models from the AI provider
            var models = await _aiModelListService.GetAvailableModelsAsync(cancellationToken);

            return Ok(new AIModelsResponse(models));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new ErrorResponse($"AI provider configuration error: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new ErrorResponse($"Failed to fetch models from AI provider: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred while fetching models"));
        }
    }

    /// <summary>
    /// Retrieves the list of available AI models from the platform's configured provider.
    /// No workspace context required — returns the same model list available to all users.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available model names from the currently-configured AI provider.</returns>
    /// <response code="200">Returns the list of available AI models.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="500">If an unexpected error occurs while fetching models.</response>
    [HttpGet("ai/models")]
    [ProducesResponseType(typeof(AIModelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPlatformAIModels(CancellationToken cancellationToken)
    {
        try
        {
            // No workspace-specific authorization required; authenticated user can always fetch the model list
            var models = await _aiModelListService.GetAvailableModelsAsync(cancellationToken);
            return Ok(new AIModelsResponse(models));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new ErrorResponse($"AI provider configuration error: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new ErrorResponse($"Failed to fetch models from AI provider: {ex.Message}"));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred while fetching models"));
        }
    }

    /// <summary>
    /// Retrieves the system startup-configured default AI model identifier.
    /// Used by the Create Workspace modal to pre-select the default model.
    /// </summary>
    /// <returns>An object containing the default model identifier.</returns>
    /// <response code="200">Returns the default model identifier.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("default-model")]
    [ProducesResponseType(typeof(DefaultModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult GetDefaultModel()
    {
        // IConfiguration is available via dependency injection in the controller
        // The model name is bound from AgentExecutionSettings during DI setup
        var modelName = HttpContext.RequestServices
            .GetRequiredService<IOptions<AgentExecutionSettings>>()
            .Value
            .ModelDeploymentName;

        return Ok(new DefaultModelResponse(modelName));
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