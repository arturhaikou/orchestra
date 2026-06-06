using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.Services;
using Orchestra.Domain.Enums;
using StackExchange.Redis;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Authorize]
[Route("v1")]
public class AgentQuestionsController(
    IAgentQuestionRepository questionRepository,
    IConnectionMultiplexer redis,
    INotificationService notificationService,
    IJobService jobService) : ControllerBase
{
    [HttpGet("agent-questions/global-pending")]
    public async Task<IActionResult> GetGlobalPendingAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var questions = await questionRepository.GetGlobalPendingByUserAsync(userId, cancellationToken);
        var camelCase = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return Ok(questions.Select(q => new
        {
            q.WorkspaceId,
            q.WorkspaceName,
            q.QuestionId,
            q.TicketId,
            q.TicketTitle,
            q.AgentName,
            QuestionsJson = JsonSerializer.Serialize(
                JsonSerializer.Deserialize<List<QuestionItem>>(q.QuestionsJson), camelCase),
            q.CreatedAt
        }));
    }

    [HttpGet("workspaces/{workspaceId:guid}/agent-questions")]
    public async Task<IActionResult> GetPendingAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var questions = await questionRepository
            .GetPendingByWorkspaceAsync(workspaceId, cancellationToken);

        return Ok(questions.Select(q => new
        {
            q.Id,
            q.JobId,
            q.AgentId,
            q.WorkspaceId,
            Questions = JsonSerializer.Deserialize<List<QuestionItem>>(q.QuestionsJson),
            q.Status,
            q.CreatedAt
        }));
    }

    [HttpGet("agent-questions/{questionId:guid}")]
    public async Task<IActionResult> GetByIdAsync(
        Guid questionId,
        CancellationToken cancellationToken)
    {
        var question = await questionRepository.GetByIdAsync(questionId, cancellationToken);
        if (question is null)
            return NotFound();

        return Ok(new
        {
            question.Id,
            question.JobId,
            question.AgentId,
            question.WorkspaceId,
            Questions = JsonSerializer.Deserialize<List<QuestionItem>>(question.QuestionsJson),
            question.Status,
            question.CreatedAt
        });
    }

    [HttpPost("agent-questions/{questionId:guid}/answer")]
    public async Task<IActionResult> AnswerAsync(
        Guid questionId,
        [FromBody] AnswerRequest request,
        CancellationToken cancellationToken)
    {
        var question = await questionRepository.GetByIdAsync(questionId, cancellationToken);
        if (question is null)
            return NotFound();

        if (question.Status != QuestionStatus.Pending)
            return Conflict("Question has already been answered.");

        var job = await jobService.GetJobAsync(question.JobId, cancellationToken);
        if (job is null || job.Status is JobStatus.Cancelled or JobStatus.Completed or JobStatus.Failed)
            return Conflict("Job is no longer active.");

        var answersJson = JsonSerializer.Serialize(request.Answers);
        await questionRepository.SaveAnswerAsync(questionId, answersJson, cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            JobId = question.JobId,
            QuestionId = questionId,
            AnswersJson = answersJson
        });

        await redis.GetSubscriber()
            .PublishAsync(RedisChannel.Literal("job-resume"), payload);

        await notificationService.NotifyAgentQuestionAnsweredAsync(
            question.WorkspaceId, questionId, cancellationToken);

        return NoContent();
    }

    public sealed record AnswerRequest(Dictionary<string, JsonElement> Answers);
}
