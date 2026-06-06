using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Enums;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IWorkflowExecutionEngine _workflowExecutionEngine;

    public JobsController(
        IJobService jobService,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IWorkflowExecutionEngine workflowExecutionEngine)
    {
        _jobService = jobService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _workflowExecutionEngine = workflowExecutionEngine;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] Guid workspaceId,
        [FromQuery] JobStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        if (!await _workspaceAuthorizationService.IsMemberAsync(userId, workspaceId, cancellationToken))
            return Forbid();

        var query = new GetJobsQuery(status, page, pageSize);
        var result = await _jobService.GetJobsAsync(workspaceId, query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        if (!await _workspaceAuthorizationService.IsMemberAsync(userId, job.WorkspaceId, cancellationToken))
            return Forbid();

        return Ok(job);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelJob(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var job = await _jobService.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        if (!await _workspaceAuthorizationService.IsMemberAsync(userId, job.WorkspaceId, cancellationToken))
            return Forbid();

        bool cancelled;
        if (job.WorkflowExecutionId.HasValue && job.ParentJobId is null)
            cancelled = await _workflowExecutionEngine.CancelWorkflowAsync(job.WorkflowExecutionId.Value, cancellationToken);
        else
            cancelled = await _jobService.CancelJobAsync(id, cancellationToken);

        if (!cancelled)
            return Conflict(new { message = "Job is already in a terminal state." });

        return NoContent();
    }
}
