using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/workflow-definitions")]
[Authorize]
public class WorkflowDefinitionsController : ControllerBase
{
    private readonly IWorkflowDefinitionService _workflowDefinitionService;
    private readonly IWorkflowSystemToolRegistry _systemToolRegistry;
    private readonly ILogger<WorkflowDefinitionsController> _logger;

    public WorkflowDefinitionsController(
        IWorkflowDefinitionService workflowDefinitionService,
        IWorkflowSystemToolRegistry systemToolRegistry,
        ILogger<WorkflowDefinitionsController> logger)
    {
        _workflowDefinitionService = workflowDefinitionService;
        _systemToolRegistry = systemToolRegistry;
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

    [HttpGet("system-tools")]
    [ProducesResponseType(typeof(List<string>), 200)]
    public IActionResult GetSystemTools()
    {
        return Ok(_systemToolRegistry.AvailableTools);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<WorkflowDefinitionDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 403)]
    public async Task<IActionResult> GetByWorkspace(
        [FromQuery] Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var validationResult = ValidateAndExtractUserId(out var userId);
        if (validationResult != null) return validationResult;

        try
        {
            var workflows = await _workflowDefinitionService.GetByWorkspaceAsync(userId, workspaceId, cancellationToken);
            return Ok(workflows);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflows for workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new ErrorResponse("An error occurred retrieving workflows."));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var validationResult = ValidateAndExtractUserId(out var userId);
        if (validationResult != null) return validationResult;

        try
        {
            var workflow = await _workflowDefinitionService.GetByIdAsync(userId, id, cancellationToken);
            if (workflow is null) return NotFound(new ErrorResponse($"Workflow {id} not found."));
            return Ok(workflow);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow {WorkflowId}", id);
            return StatusCode(500, new ErrorResponse("An error occurred retrieving the workflow."));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), 201)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = ValidateAndExtractUserId(out var userId);
        if (validationResult != null) return validationResult;

        try
        {
            var workflow = await _workflowDefinitionService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, workflow);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            return StatusCode(500, new ErrorResponse("An error occurred creating the workflow."));
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = ValidateAndExtractUserId(out var userId);
        if (validationResult != null) return validationResult;

        try
        {
            var workflow = await _workflowDefinitionService.UpdateAsync(userId, id, request, cancellationToken);
            return Ok(workflow);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow {WorkflowId}", id);
            return StatusCode(500, new ErrorResponse("An error occurred updating the workflow."));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var validationResult = ValidateAndExtractUserId(out var userId);
        if (validationResult != null) return validationResult;

        try
        {
            await _workflowDefinitionService.DeleteAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow {WorkflowId}", id);
            return StatusCode(500, new ErrorResponse("An error occurred deleting the workflow."));
        }
    }
}
