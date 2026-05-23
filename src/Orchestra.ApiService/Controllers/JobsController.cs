using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Application.Jobs.Services;
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

    public JobsController(
        IJobService jobService,
        IWorkspaceAuthorizationService workspaceAuthorizationService)
    {
        _jobService = jobService;
        _workspaceAuthorizationService = workspaceAuthorizationService;
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
}
