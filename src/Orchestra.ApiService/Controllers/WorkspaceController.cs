using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceController"/> class.
    /// </summary>
    /// <param name="workspaceService">The workspace service.</param>
    public WorkspaceController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
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

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
        return Guid.Parse(userIdClaim);
    }
}