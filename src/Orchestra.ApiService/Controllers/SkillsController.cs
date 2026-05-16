using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Application.Skills.Services;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/workspaces/{workspaceId}/skills")]
[Authorize]
public class SkillsController : ControllerBase
{
    private readonly ISkillService _skillService;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(ISkillService skillService, ILogger<SkillsController> logger)
    {
        _skillService = skillService;
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

    /// <summary>
    /// Get all skills in a workspace
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SkillDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetSkills(
        [FromRoute] Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            if (workspaceId == Guid.Empty)
                return BadRequest(new ErrorResponse("Workspace ID is required"));

            var skills = await _skillService.GetSkillsAsync(userId, workspaceId, cancellationToken);
            return Ok(skills);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access skills in workspace {WorkspaceId} without authorization", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skills for workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Get a single skill by ID
    /// </summary>
    [HttpGet("{skillId}")]
    [ProducesResponseType(typeof(SkillDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetSkillById(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var skill = await _skillService.GetSkillByIdAsync(userId, workspaceId, skillId, cancellationToken);
            if (skill is null)
                return NotFound(new ErrorResponse($"Skill '{skillId}' not found in workspace '{workspaceId}'"));

            return Ok(skill);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to access skill {SkillId} without authorization", skillId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skill {SkillId}", skillId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Create a new skill in the workspace
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SkillDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreateSkill(
        [FromRoute] Guid workspaceId,
        [FromBody] CreateSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var effectiveRequest = request with { WorkspaceId = workspaceId };
            var skill = await _skillService.CreateSkillAsync(userId, effectiveRequest, cancellationToken);
            return Created($"v1/workspaces/{workspaceId}/skills/{skill.Id}", skill);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to create skill in workspace {WorkspaceId} without authorization", workspaceId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when creating skill in workspace {WorkspaceId}", workspaceId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating skill in workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Update an existing skill
    /// </summary>
    [HttpPut("{skillId}")]
    [ProducesResponseType(typeof(SkillDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> UpdateSkill(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillId,
        [FromBody] UpdateSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            var skill = await _skillService.UpdateSkillAsync(userId, workspaceId, skillId, request, cancellationToken);
            if (skill is null)
                return NotFound(new ErrorResponse($"Skill '{skillId}' not found in workspace '{workspaceId}'"));

            return Ok(skill);
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to update skill {SkillId} without authorization", skillId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when updating skill {SkillId}", skillId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating skill {SkillId}", skillId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    /// <summary>
    /// Delete a skill from the workspace
    /// </summary>
    [HttpDelete("{skillId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> DeleteSkill(
        [FromRoute] Guid workspaceId,
        [FromRoute] Guid skillId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateAndExtractUserId(out var userId);
            if (validationResult != null)
                return validationResult;

            await _skillService.DeleteSkillAsync(userId, workspaceId, skillId, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedWorkspaceAccessException ex)
        {
            _logger.LogWarning(ex, "User attempted to delete skill {SkillId} without authorization", skillId);
            return StatusCode(403, new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting skill {SkillId}", skillId);
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }
}
