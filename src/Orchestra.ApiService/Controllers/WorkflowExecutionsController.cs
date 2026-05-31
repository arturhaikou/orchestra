using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/workflow-executions")]
[Authorize]
public class WorkflowExecutionsController : ControllerBase
{
    private readonly IWorkflowExecutionService _executionService;
    private readonly ILogger<WorkflowExecutionsController> _logger;

    public WorkflowExecutionsController(
        IWorkflowExecutionService executionService,
        ILogger<WorkflowExecutionsController> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowExecutionDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var execution = await _executionService.GetByIdAsync(id, cancellationToken);
            if (execution is null) return NotFound(new ErrorResponse($"Workflow execution {id} not found."));
            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow execution {ExecutionId}", id);
            return StatusCode(500, new ErrorResponse("An error occurred retrieving the workflow execution."));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<WorkflowExecutionDto>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> GetByTicket(
        [FromQuery] Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var executions = await _executionService.GetByTicketIdAsync(ticketId, cancellationToken);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow executions for ticket {TicketId}", ticketId);
            return StatusCode(500, new ErrorResponse("An error occurred retrieving workflow executions."));
        }
    }
}
