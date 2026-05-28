using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Tools.DTOs;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/tools")]
[Authorize]
public class ToolsController : ControllerBase
{
    private readonly IToolService _toolService;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IAgentToolAssignmentService _agentToolAssignmentService;
    private readonly IAgentOptionalToolService _agentOptionalToolService;
    private readonly ILogger<ToolsController> _logger;

    public ToolsController(
        IToolService toolService,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IAgentToolAssignmentService agentToolAssignmentService,
        IAgentOptionalToolService agentOptionalToolService,
        ILogger<ToolsController> logger)
    {
        _toolService = toolService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _agentToolAssignmentService = agentToolAssignmentService;
        _agentOptionalToolService = agentOptionalToolService;
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
    /// Get available tools filtered by workspace integrations.
    /// Returns hierarchical structure with categories containing actions.
    /// </summary>
    /// <param name="workspaceId">Required workspace identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tool categories with nested actions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Application.Tools.DTOs.ToolCategoryDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAvailableTools(
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserIdFromClaims();

            if (workspaceId == Guid.Empty)
            {
                return BadRequest(new ErrorResponse("Workspace ID is required"));
            }

            var tools = await _toolService.GetAvailableToolsAsync(userId, workspaceId, cancellationToken);
            return Ok(tools);
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access tools for workspace {WorkspaceId} without authorization", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for workspace {WorkspaceId}", workspaceId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available tools for workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred while retrieving tools"));
        }
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
        return Guid.Parse(userIdClaim);
    }

    /// <summary>
    /// Get tool actions assigned to a specific agent.
    /// Returns flat list of tool actions.
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of assigned tool actions</returns>
    [HttpGet]
    [Route("/v1/agents/{agentId}/tools")]
    [ProducesResponseType(typeof(List<Application.Tools.DTOs.ToolActionDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAgentToolActions(
        [FromRoute] Guid agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserIdFromClaims();

            var toolActions = await _toolService.GetAgentToolActionsAsync(userId, agentId, cancellationToken);
            return Ok(toolActions);
        }
        catch (Application.Common.Exceptions.AgentNotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent {AgentId} not found", agentId);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access agent {AgentId} tools without authorization", agentId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool actions for agent {AgentId}", agentId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred while retrieving agent tool actions"));
        }
    }

    /// <summary>
    /// Assign tool actions to an agent.
    /// Duplicates are ignored (upsert behavior).
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="request">Request containing list of tool action IDs to assign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>204 No Content on success</returns>
    [HttpPost]
    [Route("/v1/agents/{agentId}/tools")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> AssignToolActionsToAgent(
        [FromRoute] Guid agentId,
        [FromBody] Application.Tools.DTOs.AssignToolActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserIdFromClaims();

            await _toolService.AssignToolActionsToAgentAsync(userId, agentId, request.ToolActionIds, cancellationToken);
            return NoContent();
        }
        catch (Application.Common.Exceptions.AgentNotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent {AgentId} not found", agentId);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.ToolActionNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tool action {ToolActionId} not found", ex.ToolActionId);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to assign tools to agent {AgentId} without authorization", agentId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument for agent {AgentId}", agentId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning tool actions to agent {AgentId}", agentId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred while assigning tool actions"));
        }
    }

    /// <summary>
    /// Remove tool actions from an agent.
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="request">Request containing list of tool action IDs to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>204 No Content on success</returns>
    [HttpDelete]
    [Route("/v1/agents/{agentId}/tools")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> RemoveToolActionsFromAgent(
        [FromRoute] Guid agentId,
        [FromBody] Application.Tools.DTOs.AssignToolActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserIdFromClaims();

            await _toolService.RemoveToolActionsFromAgentAsync(userId, agentId, request.ToolActionIds, cancellationToken);
            return NoContent();
        }
        catch (Application.Common.Exceptions.AgentNotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent {AgentId} not found", agentId);
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to remove tools from agent {AgentId} without authorization", agentId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing tool actions from agent {AgentId}", agentId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred while removing tool actions"));
        }
    }

    [HttpPatch("{toolActionId}/enabled")]
    [ProducesResponseType(typeof(ToolActionDetailDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> ToggleToolActionEnabled(
        [FromRoute] Guid toolActionId,
        [FromBody] ToggleToolEnabledRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var result = await _toolService.ToggleToolActionEnabledAsync(userId, toolActionId, request.IsEnabled, cancellationToken);
            return Ok(result);
        }
        catch (ToolActionNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized toggle attempt for tool action {ToolActionId}", toolActionId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
    }

    [HttpPut]
    [Route("/v1/agents/{agentId}/tools")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> SaveAgentToolAssignments(
        [FromRoute] Guid agentId,
        [FromBody] SaveAgentToolsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdFromClaims();
        await _agentToolAssignmentService.SaveAssignmentsAsync(userId, agentId, request, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [Route("/v1/agents/{agentId}/mcp-tools")]
    [ProducesResponseType(typeof(AgentToolAssignmentsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAgentMcpToolAssignments(
        [FromRoute] Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdFromClaims();
        var result = await _agentToolAssignmentService.GetAssignmentsAsync(userId, agentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [Route("/v1/agents/{agentId}/optional-tools")]
    [ProducesResponseType(typeof(AgentOptionalToolSelectionsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAgentOptionalTools(
        [FromRoute] Guid agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var selectedMethodNames = await _agentOptionalToolService.GetCurrentSelectionsAsync(userId, agentId, cancellationToken);
            return Ok(new AgentOptionalToolSelectionsDto(selectedMethodNames));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized optional tools access for agent {AgentId}", agentId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optional tools for agent {AgentId}", agentId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpPut]
    [Route("/v1/agents/{agentId}/optional-tools")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> SaveAgentOptionalTools(
        [FromRoute] Guid agentId,
        [FromBody] SaveAgentOptionalToolsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            await _agentOptionalToolService.SaveSelectionsAsync(userId, agentId, request.MethodNames, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Application.Common.Exceptions.UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized optional tools save for agent {AgentId}", agentId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving optional tools for agent {AgentId}", agentId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }
}