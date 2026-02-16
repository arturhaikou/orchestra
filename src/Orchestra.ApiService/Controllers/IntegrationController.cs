using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.Auth.DTOs;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

/// <summary>
/// Controller for managing workspace integrations.
/// </summary>
[ApiController]
[Route("v1/integrations")]
[Authorize]
public class IntegrationController : ControllerBase
{
    private readonly IIntegrationService _integrationService;
    private readonly ILogger<IntegrationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationController"/> class.
    /// </summary>
    /// <param name="integrationService">The integration service.</param>
    /// <param name="logger">The logger instance.</param>
    public IntegrationController(
        IIntegrationService integrationService,
        ILogger<IntegrationController> logger)
    {
        _integrationService = integrationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all integrations for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to filter integrations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of workspace integrations.</returns>
    /// <response code="200">Returns the list of integrations.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User not a member of the workspace.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<IntegrationDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetWorkspaceIntegrations(
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var integrations = await _integrationService.GetWorkspaceIntegrationsAsync(
                userId, 
                workspaceId, 
                cancellationToken);
            
            return Ok(integrations);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access workspace {WorkspaceId} without authorization", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Creates a new integration for a workspace.
    /// </summary>
    /// <param name="request">The create integration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created integration.</returns>
    /// <response code="201">Integration created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User not a member of the workspace.</response>
    /// <response code="409">Integration name already exists in workspace.</response>
    [HttpPost]
    [ProducesResponseType(typeof(IntegrationDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    public async Task<IActionResult> CreateIntegration(
        [FromBody] CreateIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var integration = await _integrationService.CreateIntegrationAsync(
                userId, 
                request, 
                cancellationToken);
            
            return CreatedAtAction(
                nameof(CreateIntegration), 
                new { id = integration.Id }, 
                integration);
        }
        catch (DuplicateIntegrationException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Updates an existing integration.
    /// </summary>
    /// <param name="id">The integration ID to update.</param>
    /// <param name="request">The update integration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated integration.</returns>
    /// <response code="200">Integration updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User not a member of the workspace.</response>
    /// <response code="404">Integration not found.</response>
    /// <response code="409">Integration name already exists in workspace.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(IntegrationDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    public async Task<IActionResult> UpdateIntegration(
        Guid id,
        [FromBody] UpdateIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var integration = await _integrationService.UpdateIntegrationAsync(
                userId,
                id,
                request,
                cancellationToken);
            
            return Ok(integration);
        }
        catch (IntegrationNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (DuplicateIntegrationException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Deletes an integration (soft delete).
    /// </summary>
    /// <param name="id">The integration ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content if successful.</returns>
    /// <response code="204">Integration deleted successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">User not a member of the workspace.</response>
    /// <response code="404">Integration not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> DeleteIntegration(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            await _integrationService.DeleteIntegrationAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (IntegrationNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Validates that a connection to an integration provider is successful.
    /// </summary>
    /// <param name="request">The connection validation request with provider credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK if connection is valid.</returns>
    /// <response code="200">Connection validated successfully.</response>
    /// <response code="400">Invalid request data or connection failed.</response>
    /// <response code="401">User not authenticated.</response>
    [AllowAnonymous]
    [HttpPost("validate-connection")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> ValidateConnection(
        [FromBody] ValidateIntegrationConnectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _integrationService.ValidateConnectionAsync(request, cancellationToken);
            return Ok(new { success = true, message = "Connection validated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during connection validation");
            return BadRequest(new ErrorResponse("Failed to validate connection. Please check your credentials and try again."));
        }
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
        return Guid.Parse(userIdClaim);
    }
}
