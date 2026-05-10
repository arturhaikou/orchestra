using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.McpServers.DTOs;
using Orchestra.Application.Integrations.Services;
using Orchestra.Application.McpServers.Interfaces;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/mcp-servers")]
[Authorize]
public class McpServerController : ControllerBase
{
    private readonly IMcpServerService _mcpServerService;
    private readonly IMcpServerCommandService _commandService;
    private readonly IMcpServerQueryService _queryService;
    private readonly IMcpServerToolFetcher _toolFetcher;
    private readonly IMcpServerConnectionService _connectionService;
    private readonly ILogger<McpServerController> _logger;

    public McpServerController(
        IMcpServerService mcpServerService,
        IMcpServerCommandService commandService,
        IMcpServerQueryService queryService,
        IMcpServerToolFetcher toolFetcher,
        IMcpServerConnectionService connectionService,
        ILogger<McpServerController> logger)
    {
        _mcpServerService = mcpServerService;
        _commandService = commandService;
        _queryService = queryService;
        _toolFetcher = toolFetcher;
        _connectionService = connectionService;
        _logger = logger;
    }

    [HttpPost("connect")]
    [ProducesResponseType(typeof(ConnectMcpServerResponseDto), 200)]
    [ProducesResponseType(typeof(McpServerErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(McpServerErrorResponse), 403)]
    [ProducesResponseType(typeof(McpServerErrorResponse), 422)]
    public async Task<IActionResult> Connect(
        [FromBody] ConnectMcpServerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var result = await _connectionService.ConnectAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new McpServerErrorResponse(ex.Message));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex,
                "Unauthorized connect attempt for workspace {WorkspaceId}",
                request.WorkspaceId);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpGet("check-name")]
    [ProducesResponseType(typeof(CheckNameUniqueResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CheckNameUnique(
        [FromQuery] Guid workspaceId,
        [FromQuery] string name,
        [FromQuery] Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty || string.IsNullOrWhiteSpace(name))
            return BadRequest(new McpServerErrorResponse("workspaceId and name are required."));

        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
            return BadRequest(new McpServerErrorResponse("Name must not be empty."));

        try
        {
            var userId = GetUserIdFromClaims();
            var isUnique = await _mcpServerService.IsNameUniqueAsync(
                userId, workspaceId, trimmedName, excludeId, cancellationToken);

            return Ok(new CheckNameUniqueResponse(isUnique));
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex,
                "User attempted name check for workspace {WorkspaceId} without authorization",
                workspaceId);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<McpServerListItemDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return BadRequest(new McpServerErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            var servers = await _queryService.GetListAsync(userId, workspaceId, cancellationToken);
            return Ok(servers);
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized list attempt for workspace {WorkspaceId}", workspaceId);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetMcpServerByIdResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return BadRequest(new McpServerErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            var server = await _queryService.GetByIdAsync(userId, workspaceId, id, cancellationToken);
            return Ok(server);
        }
        catch (ArgumentException)
        {
            return NotFound(new McpServerErrorResponse($"MCP server '{id}' was not found."));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized get attempt for server {ServerId}", id);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(McpServerListItemDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Create(
        [FromBody] SaveMcpServerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var server = await _commandService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetById),
                new { id = server.Id, workspaceId = request.WorkspaceId }, server);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new McpServerErrorResponse(ex.Message));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized create attempt for workspace {WorkspaceId}", request.WorkspaceId);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(McpServerListItemDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] PatchMcpServerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var server = await _commandService.UpdateAsync(userId, id, request, cancellationToken);
            return Ok(server);
        }
        catch (ArgumentException)
        {
            return NotFound(new McpServerErrorResponse($"MCP server '{id}' was not found."));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new McpServerErrorResponse(ex.Message));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized update attempt for server {ServerId}", id);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(DeleteMcpServerResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return BadRequest(new McpServerErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            var result = await _commandService.DeleteAsync(userId, id, workspaceId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new McpServerErrorResponse($"MCP server '{id}' was not found."));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized delete attempt for server {ServerId}", id);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    [HttpGet("{id:guid}/tools")]
    [ProducesResponseType(typeof(McpServerToolsResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTools(
        Guid id,
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return BadRequest(new McpServerErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            var result = await _toolFetcher.FetchToolsAsync(userId, workspaceId, id, cancellationToken);
            return Ok(MapToResponseDto(result));
        }
        catch (ArgumentException)
        {
            return NotFound(new McpServerErrorResponse($"MCP server '{id}' was not found."));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized tool fetch attempt for server {ServerId}", id);
            return StatusCode(403, new McpServerErrorResponse("Access denied."));
        }
    }

    private static McpServerToolsResponseDto MapToResponseDto(McpToolFetchResult result) =>
        result switch
        {
            McpToolFetchResult.Success s => new McpServerToolsResponseDto(
                IsSuccess: true,
                Tools: s.Tools.Select(t => new McpToolItemDto(t.Name, t.Description, t.DangerLevel.ToString())).ToList(),
                ErrorType: null,
                ErrorMessage: null),

            McpToolFetchResult.Empty => new McpServerToolsResponseDto(
                IsSuccess: false,
                Tools: null,
                ErrorType: "Empty",
                ErrorMessage: null),

            McpToolFetchResult.Unreachable u => new McpServerToolsResponseDto(
                IsSuccess: false,
                Tools: null,
                ErrorType: "Unreachable",
                ErrorMessage: u.Message),

            McpToolFetchResult.AuthFailed => new McpServerToolsResponseDto(
                IsSuccess: false,
                Tools: null,
                ErrorType: "AuthFailed",
                ErrorMessage: null),

            _ => new McpServerToolsResponseDto(
                IsSuccess: false,
                Tools: null,
                ErrorType: "Unreachable",
                ErrorMessage: "Unknown error occurred.")
        };

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
        return Guid.Parse(userIdClaim);
    }
}

public record CheckNameUniqueResponse(bool IsUnique);

public record McpServerErrorResponse(string Error);
