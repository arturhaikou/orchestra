using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Application.Skills.Services;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/workspaces/{workspaceId}/skill-folders")]
[Authorize]
public class SkillFoldersController : ControllerBase
{
    private readonly ISkillFolderService _skillFolderService;
    private readonly ILogger<SkillFoldersController> _logger;

    public SkillFoldersController(ISkillFolderService skillFolderService, ILogger<SkillFoldersController> logger)
    {
        _skillFolderService = skillFolderService;
        _logger = logger;
    }

    private IActionResult? ValidateAndExtractUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
        {
            userId = Guid.Empty;
            return Unauthorized(new ErrorResponse("Invalid user token"));
        }
        return null;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SkillFolderDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetSkillFolders(
        [FromRoute] Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var folders = await _skillFolderService.GetSkillFoldersAsync(userId, workspaceId, cancellationToken);
            return Ok(folders);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to skill folders in workspace {WorkspaceId}", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skill folders for workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpGet("{skillFolderId}")]
    [ProducesResponseType(typeof(SkillFolderDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetSkillFolderById(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillFolderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var folder = await _skillFolderService.GetSkillFolderByIdAsync(userId, workspaceId, skillFolderId, cancellationToken);
            if (folder is null)
                return NotFound(new ErrorResponse($"Skill folder '{skillFolderId}' not found in workspace '{workspaceId}'"));

            return Ok(folder);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to skill folder {SkillFolderId}", skillFolderId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skill folder {SkillFolderId}", skillFolderId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpGet("{skillFolderId}/skills")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscoveredSkillDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetSkillsInFolder(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillFolderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var skills = await _skillFolderService.GetAvailableSkillsAsync(userId, workspaceId, skillFolderId, cancellationToken);
            return Ok(skills);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to skills in folder {SkillFolderId}", skillFolderId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering skills in folder {SkillFolderId}", skillFolderId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(SkillFolderDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreateSkillFolder(
        [FromRoute] Guid workspaceId,
        [FromBody] CreateSkillFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var effectiveRequest = request with { WorkspaceId = workspaceId };
            var folder = await _skillFolderService.CreateSkillFolderAsync(userId, effectiveRequest, cancellationToken);
            return Created($"v1/workspaces/{workspaceId}/skill-folders/{folder.Id}", folder);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to create skill folder in workspace {WorkspaceId}", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when creating skill folder in workspace {WorkspaceId}", workspaceId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating skill folder in workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpPut("{skillFolderId}")]
    [ProducesResponseType(typeof(SkillFolderDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> UpdateSkillFolder(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillFolderId,
        [FromBody] UpdateSkillFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var folder = await _skillFolderService.UpdateSkillFolderAsync(userId, workspaceId, skillFolderId, request, cancellationToken);
            if (folder is null)
                return NotFound(new ErrorResponse($"Skill folder '{skillFolderId}' not found in workspace '{workspaceId}'"));

            return Ok(folder);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to update skill folder {SkillFolderId}", skillFolderId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when updating skill folder {SkillFolderId}", skillFolderId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating skill folder {SkillFolderId}", skillFolderId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpDelete("{skillFolderId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> DeleteSkillFolder(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillFolderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            await _skillFolderService.DeleteSkillFolderAsync(userId, workspaceId, skillFolderId, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to delete skill folder {SkillFolderId}", skillFolderId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting skill folder {SkillFolderId}", skillFolderId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }
}
