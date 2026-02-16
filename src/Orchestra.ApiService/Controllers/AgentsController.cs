using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/agents")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IAgentService agentService,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _logger = logger;
    }

    private IActionResult ValidateAndExtractUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
        {
            userId = Guid.Empty;
            return Unauthorized(new ErrorResponse("Invalid user token"));
        }
        return null;
    }

    /// <summary>
    /// Get all agents in a workspace
    /// </summary>
    /// <param name="workspaceId">The workspace ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of agents</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<AgentDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAgentsByWorkspace(
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            if (workspaceId == Guid.Empty)
            {
                return BadRequest(new ErrorResponse("Workspace ID is required"));
            }

            var agents = await _agentService.GetAgentsByWorkspaceIdAsync(
                userId, workspaceId, cancellationToken);

            return Ok(agents);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access workspace {WorkspaceId} without authorization", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when getting agents for workspace {WorkspaceId}", workspaceId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agents for workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Get agent by ID
    /// </summary>
    /// <param name="id">The agent ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AgentDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAgentById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var agent = await _agentService.GetAgentByIdAsync(userId, id, cancellationToken);
            return Ok(agent);
        }
        catch (AgentNotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent {AgentId} not found", id);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access agent {AgentId} without authorization", id);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent {AgentId}", id);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Create a new agent
    /// </summary>
    /// <param name="request">Agent creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created agent</returns>
    [HttpPost]
    [ProducesResponseType(typeof(AgentDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreateAgent(
        [FromBody] CreateAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var agent = await _agentService.CreateAgentAsync(userId, request, cancellationToken);
            return Created("", agent);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to create agent in workspace {WorkspaceId} without authorization", request.WorkspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when creating agent in workspace {WorkspaceId}", request.WorkspaceId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent in workspace {WorkspaceId}", request.WorkspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Update an existing agent
    /// </summary>
    /// <param name="id">The agent ID</param>
    /// <param name="request">Agent update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated agent</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AgentDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> UpdateAgent(
        [FromRoute] Guid id,
        [FromBody] UpdateAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var agent = await _agentService.UpdateAgentAsync(userId, id, request, cancellationToken);
            return Ok(agent);
        }
        catch (AgentNotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent {AgentId} not found for update", id);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to update agent {AgentId} without authorization", id);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when updating agent {AgentId}", id);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent {AgentId}", id);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Delete an agent
    /// </summary>
    /// <param name="id">The agent ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> DeleteAgent(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            await _agentService.DeleteAgentAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (AgentNotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent {AgentId} not found for deletion", id);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to delete agent {AgentId} without authorization", id);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agent {AgentId}", id);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }
}