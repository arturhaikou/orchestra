using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Domain.Exceptions;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(
        ITicketService ticketService,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            // Check if user has access to the workspace
            if (!await _workspaceAuthorizationService.IsMemberAsync(userId, request.WorkspaceId, cancellationToken))
            {
                return Forbid();
            }

            var ticket = await _ticketService.CreateTicketAsync(userId, request, cancellationToken);
            return Created("", ticket);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException)
        {
            return Forbid();
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

/// <summary>
/// Gets a single ticket by ID.
/// </summary>
/// <remarks>
/// Supports two ticket ID formats:
/// - **Internal tickets**: GUID format (e.g., "3fa85f64-5717-4562-b3fc-2c963f66afa6")
/// - **External tickets**: Composite format {integrationId}:{externalTicketId} (e.g., "d7b3c8a2-1234-5678-90ab-cdef01234567:PROJ-123")
/// 
/// External tickets are fetched live from their source system (Jira, Azure DevOps, etc.) and merged with 
/// any local assignments (agent/workflow). The composite ID format remains stable even if the ticket is 
/// "materialized" (stored locally after assignment).
/// 
/// **Note:** Do not use database GUIDs for materialized external tickets. Always use the composite ID format.
/// </remarks>
/// <param name="id">Ticket identifier - GUID for internal tickets or composite format for external tickets</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Ticket details with comments</returns>
/// <response code="200">Returns the ticket details</response>
/// <response code="400">Invalid ticket ID format</response>
/// <response code="401">User is not authenticated</response>
/// <response code="403">User does not have access to the ticket's workspace</response>
/// <response code="404">Ticket not found</response>
[HttpGet("{id}")]
[ProducesResponseType(typeof(TicketDto), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
[ProducesResponseType(typeof(ErrorResponse), 401)]
[ProducesResponseType(typeof(ErrorResponse), 403)]
[ProducesResponseType(typeof(ErrorResponse), 404)]
[ProducesResponseType(typeof(ErrorResponse), 500)]
public async Task<IActionResult> GetTicketById(
    string id,
    CancellationToken cancellationToken)
{
    try
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid user token"));
        }

        var ticket = await _ticketService.GetTicketByIdAsync(id, userId, cancellationToken);
        return Ok(ticket);
    }
    catch (TicketNotFoundException ex)
    {
        return NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedTicketAccessException)
    {
        return StatusCode(403, new ErrorResponse("You do not have access to this ticket"));
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new ErrorResponse(ex.Message));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving ticket {TicketId}", id);
        return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
    }
}

/// <summary>
/// Retrieves a paginated list of tickets for the specified workspace.
/// Merges internal database tickets with external provider tickets.
/// </summary>
/// <remarks>
/// This endpoint fetches tickets from both internal storage and external integration providers (e.g., Jira).
/// External tickets are merged with internal tickets based on (IntegrationId, ExternalTicketId).
/// Provider failures degrade gracefully, returning internal tickets only with logged warnings.
/// Requires workspace membership authorization.
/// </remarks>
/// <param name="workspaceId">The workspace ID to retrieve tickets from (required)</param>
/// <param name="pageToken">Continuation token for cursor-based pagination (optional)</param>
/// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Paginated list of tickets with internal and external tickets merged</returns>
/// <response code="200">Successfully retrieved ticket list</response>
/// <response code="400">Invalid parameters (empty workspaceId or invalid pageSize)</response>
/// <response code="401">Missing or invalid JWT token</response>
/// <response code="403">User is not a member of the workspace</response>
/// <response code="500">Unexpected server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedTicketsResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<ActionResult<PaginatedTicketsResponse>> GetTickets(
        [FromQuery] Guid workspaceId,
        [FromQuery] string? pageToken = null,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            if (workspaceId == Guid.Empty)
            {
                return BadRequest(new ErrorResponse("Workspace ID is required"));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponse("Page size must be between 1 and 100"));
            }

            // Check user workspace membership
            if (!await _workspaceAuthorizationService.IsMemberAsync(userId, workspaceId, cancellationToken))
            {
                return Forbid();
            }

            var result = await _ticketService.GetTicketsAsync(
                workspaceId,
                userId,
                pageToken,
                pageSize,
                cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedTicketAccessException)
        {
            return Forbid();
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

    [HttpGet("statuses")]
    [ProducesResponseType(typeof(List<TicketStatusDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAllStatuses(CancellationToken cancellationToken)
    {
        try
        {
            var statuses = await _ticketService.GetAllStatusesAsync(cancellationToken);
            return Ok(statuses);
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpGet("priorities")]
    [ProducesResponseType(typeof(List<TicketPriorityDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAllPriorities(CancellationToken cancellationToken)
    {
        try
        {
            var priorities = await _ticketService.GetAllPrioritiesAsync(cancellationToken);
            return Ok(priorities);
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

/// <summary>
/// Updates a ticket's assignments and metadata (PATCH /v1/tickets/{id}).
/// External tickets can only update assignments (agent/workflow).
/// Internal tickets can update status, priority, and assignments.
/// Materializes external tickets on first assignment.
/// </summary>
/// <param name="id">Internal ticket GUID or composite format {integrationId}:{externalTicketId}</param>
/// <param name="request">Update request with nullable fields for partial updates</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated ticket details</returns>
/// <response code="200">Ticket updated successfully</response>
/// <response code="400">Invalid request or attempted to update status/priority on external ticket</response>
/// <response code="401">Unauthorized - missing or invalid JWT token</response>
/// <response code="403">Forbidden - user lacks access to ticket's workspace</response>
/// <response code="404">Ticket not found</response>
[HttpPatch("{id}")]
[ProducesResponseType(typeof(TicketDto), 200)]
[ProducesResponseType(typeof(ErrorResponse), 400)]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ErrorResponse), 401)]
[ProducesResponseType(typeof(ErrorResponse), 403)]
[ProducesResponseType(typeof(ErrorResponse), 404)]
[ProducesResponseType(typeof(ErrorResponse), 500)]
public async Task<IActionResult> UpdateTicket(
    string id,
    [FromBody] UpdateTicketRequest request,
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

        // Call service method
        var updatedTicket = await _ticketService.UpdateTicketAsync(
            id,
            userId,
            request,
            cancellationToken);

        return Ok(updatedTicket);
    }
    catch (InvalidWorkspaceAssignmentException ex)
    {
        _logger.LogWarning(ex,
            "Invalid workspace assignment for ticket {TicketId}",
            id);
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid Assignment",
            Detail = ex.Message
        });
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning(ex,
            "Bad request while updating ticket {TicketId}",
            id);
        return BadRequest(new ErrorResponse(ex.Message));
    }
    catch (InvalidTicketOperationException ex)
    {
        _logger.LogWarning(ex,
            "Invalid operation attempted on ticket {TicketId}",
            id);
        return BadRequest(new ErrorResponse(ex.Message));
    }
    catch (TicketNotFoundException ex)
    {
        _logger.LogWarning(ex,
            "Ticket {TicketId} not found for update",
            id);
        return NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedTicketAccessException ex)
    {
        _logger.LogWarning(ex,
            "Unauthorized access to ticket {TicketId}",
            id);
        return StatusCode(403, new ErrorResponse(ex.Message));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Unexpected error updating ticket {TicketId}",
            id);
        return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
    }
}

    /// <summary>
    /// Converts an internal ticket to an external tracker ticket.
    /// </summary>
    /// <remarks>
    /// Creates a new issue in the external tracker system (Jira, Azure DevOps, etc.) 
    /// using the ticket's title and description, then updates the internal ticket 
    /// record to reference the external issue.
    /// 
    /// **Requirements:**
    /// - Ticket must be internal (IsInternal = true)
    /// - Integration must be a TRACKER type and active
    /// - User must have access to the workspace
    /// - Integration and ticket must belong to the same workspace
    /// 
    /// **After conversion:**
    /// - Ticket becomes read-only for status/priority (managed by external system)
    /// - Assignments (agent/workflow) can still be updated
    /// - Ticket ID changes to composite format: {integrationId}:{externalTicketId}
    /// </remarks>
    /// <param name="id">Internal ticket GUID</param>
    /// <param name="request">Conversion request with integration ID and issue type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Ticket successfully converted, returns updated ticket with external reference</response>
    /// <response code="400">Invalid request (already external, inactive integration, wrong workspace)</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have access to the ticket's workspace</response>
    /// <response code="404">Ticket or integration not found</response>
    [HttpPost("{id}/convert")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConvertToExternal(
        string id,
        [FromBody] ConvertTicketRequest request,
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

            // Call service method
            var convertedTicket = await _ticketService.ConvertToExternalAsync(
                id,
                userId,
                request.IntegrationId,
                request.IssueTypeName,
                cancellationToken);

            _logger.LogInformation(
                "Successfully converted ticket {TicketId} to external for user {UserId}",
                id, userId);

            return Ok(convertedTicket);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex,
                "Invalid operation while converting ticket {TicketId}",
                id);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex,
                "Bad request while converting ticket {TicketId}",
                id);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "Ticket {TicketId} not found for conversion",
                id);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "Integration not found while converting ticket {TicketId}",
                id);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogWarning(ex,
                "Unauthorized access to ticket {TicketId}",
                id);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "External API error while converting ticket {TicketId}",
                id);
            return StatusCode(500, new ErrorResponse($"Failed to create external ticket: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error converting ticket {TicketId}",
                id);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Deletes an internal ticket.
    /// </summary>
    /// <remarks>
    /// **IMPORTANT:** Only internal tickets can be deleted. External tickets (from Jira, Azure DevOps, etc.)
    /// must be deleted in their source system. Attempting to delete an external ticket will return 400 Bad Request.
    /// 
    /// Deletion will cascade to all associated comments.
    /// </remarks>
    /// <param name="id">Internal ticket GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="204">Ticket successfully deleted</response>
    /// <response code="400">Attempting to delete external ticket or invalid ticket ID format</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have access to the ticket's workspace</response>
    /// <response code="404">Ticket not found</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTicket(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract user ID from JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid user ID claim in JWT token");
                return Unauthorized(new ProblemDetails
                {
                    Title = "Invalid authentication token",
                    Status = StatusCodes.Status401Unauthorized
                });
            }
            
            // Call service to delete ticket
            await _ticketService.DeleteTicketAsync(id, userId, cancellationToken);
            
            return NoContent();
        }
        catch (InvalidTicketOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid ticket deletion operation for ticket {TicketId}", id);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Operation",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogWarning(ex, "Ticket {TicketId} not found for deletion", id);
            return NotFound(new ProblemDetails
            {
                Title = "Ticket Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to ticket {TicketId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Forbidden",
                Detail = ex.Message,
                Status = StatusCodes.Status403Forbidden
            });
        }
    }

    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(CommentDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 502)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> AddComment(
        string id,
        [FromBody] AddCommentRequest request,
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

            // Call service (author will be fetched from database using userId)
            var comment = await _ticketService.AddCommentAsync(
                id,
                userId,
                request,
                cancellationToken);

            // Return 201 Created with location header
            return Created($"/v1/tickets/{id}", comment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (TicketNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedTicketAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to add comment"))
        {
            // Provider API failure - return 502 Bad Gateway
            return StatusCode(502, new ErrorResponse($"External provider error: {ex.Message}"));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Generates an AI-powered summary for a ticket by combining its description and comments.
    /// </summary>
    /// <remarks>
    /// The summary is generated on-demand and not persisted to the database.
    /// Supports both internal tickets (GUID format) and external tickets (composite format).
    /// Requires workspace membership authorization.
    /// </remarks>
    /// <param name="id">Ticket identifier - GUID for internal tickets or composite format for external tickets</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ticket details with the Summary field populated</returns>
    /// <response code="200">Successfully generated summary</response>
    /// <response code="400">Invalid ticket ID format</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have access to the ticket's workspace</response>
    /// <response code="404">Ticket not found</response>
    /// <response code="500">AI summarization service error or unexpected error</response>
    [HttpPost("{id}/summarize")]
    [ProducesResponseType(typeof(TicketDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GenerateSummary(
        string id,
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

            // Call service to generate summary
            var ticketWithSummary = await _ticketService.GenerateSummaryAsync(
                id,
                userId,
                cancellationToken);

            return Ok(ticketWithSummary);
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogWarning(ex, "Ticket {TicketId} not found for summarization", id);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to ticket {TicketId}", id);
            return StatusCode(403, new ErrorResponse("You do not have access to this ticket"));
        }
        catch (SummarizationException ex)
        {
            _logger.LogError(ex, "Summarization failed for ticket {TicketId}", id);
            return StatusCode(500, new ErrorResponse("Failed to generate summary: " + ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating summary for ticket {TicketId}", id);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }
}