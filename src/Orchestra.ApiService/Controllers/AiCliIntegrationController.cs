using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.AiCliIntegrations.DTOs;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Common.Exceptions;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/ai-cli-integrations")]
[Authorize]
public class AiCliIntegrationController : ControllerBase
{
    private readonly IAiCliIntegrationCommandService _commandService;
    private readonly IAiCliIntegrationQueryService _queryService;
    private readonly ICopilotModelDiscoveryService _modelDiscoveryService;
    private readonly ILogger<AiCliIntegrationController> _logger;

    public AiCliIntegrationController(
        IAiCliIntegrationCommandService commandService,
        IAiCliIntegrationQueryService queryService,
        ICopilotModelDiscoveryService modelDiscoveryService,
        ILogger<AiCliIntegrationController> logger)
    {
        _commandService = commandService;
        _queryService = queryService;
        _modelDiscoveryService = modelDiscoveryService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AiCliIntegrationDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty)
            return BadRequest(new AiCliIntegrationErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            var integrations = await _queryService.GetListAsync(userId, workspaceId, cancellationToken);
            return Ok(integrations);
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized list attempt for workspace {WorkspaceId}", workspaceId);
            return StatusCode(403, new AiCliIntegrationErrorResponse("Access denied."));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AiCliIntegrationDto), 200)]
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
            return BadRequest(new AiCliIntegrationErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            var integration = await _queryService.GetByIdAsync(userId, workspaceId, id, cancellationToken);
            return Ok(integration);
        }
        catch (ArgumentException)
        {
            return NotFound(new AiCliIntegrationErrorResponse($"AI CLI integration '{id}' was not found."));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized get attempt for integration {IntegrationId}", id);
            return StatusCode(403, new AiCliIntegrationErrorResponse("Access denied."));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(AiCliIntegrationDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAiCliIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var integration = await _commandService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetById),
                new { id = integration.Id, workspaceId = request.WorkspaceId }, integration);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new AiCliIntegrationErrorResponse(ex.Message));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized create attempt for workspace {WorkspaceId}", request.WorkspaceId);
            return StatusCode(403, new AiCliIntegrationErrorResponse("Access denied."));
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AiCliIntegrationDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAiCliIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserIdFromClaims();
            var integration = await _commandService.UpdateAsync(userId, id, request, cancellationToken);
            return Ok(integration);
        }
        catch (ArgumentException)
        {
            return NotFound(new AiCliIntegrationErrorResponse($"AI CLI integration '{id}' was not found."));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new AiCliIntegrationErrorResponse(ex.Message));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized update attempt for integration {IntegrationId}", id);
            return StatusCode(403, new AiCliIntegrationErrorResponse("Access denied."));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
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
            return BadRequest(new AiCliIntegrationErrorResponse("workspaceId is required."));

        try
        {
            var userId = GetUserIdFromClaims();
            await _commandService.DeleteAsync(userId, id, workspaceId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound(new AiCliIntegrationErrorResponse($"AI CLI integration '{id}' was not found."));
        }
        catch (WorkspaceAccessDeniedException ex)
        {
            _logger.LogWarning(ex, "Unauthorized delete attempt for integration {IntegrationId}", id);
            return StatusCode(403, new AiCliIntegrationErrorResponse("Access denied."));
        }
    }

    [HttpPost("models")]
    [ProducesResponseType(typeof(List<string>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> DiscoverModels(
        [FromBody] DiscoverCopilotModelsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var models = await _modelDiscoveryService.DiscoverModelsAsync(
                request.Credential,
                request.UseLoggedInUser,
                request.WorkingDirectory,
                request.CliPath,
                cancellationToken);

            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover Copilot models");
            return BadRequest(new AiCliIntegrationErrorResponse("Failed to discover models. Check your credentials and working directory."));
        }
    }

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token claims.");
        return Guid.Parse(userIdClaim);
    }
}

public record AiCliIntegrationErrorResponse(string Error);
public record DiscoverCopilotModelsRequest(string? Credential, bool UseLoggedInUser, string WorkingDirectory, string? CliPath = null);
