using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Enums;
using StackExchange.Redis;

namespace Orchestra.Infrastructure.Agents;

public class JobResumeCoordinator(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    ILogger<JobResumeCoordinator> logger) : IHostedService
{
    private ISubscriber? _subscriber;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = redis.GetSubscriber();
        await _subscriber.SubscribeAsync(
            RedisChannel.Literal("job-resume"),
            HandleMessageAsync);

        logger.LogInformation("JobResumeCoordinator subscribed to Redis channel 'job-resume'.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
            await _subscriber.UnsubscribeAsync(RedisChannel.Literal("job-resume"));
    }

    private async void HandleMessageAsync(RedisChannel channel, RedisValue message)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ResumePayload>(message.ToString());
            if (payload is null) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var runtimeService = scope.ServiceProvider.GetRequiredService<IAgentRuntimeService>();
            var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionEngine>();

            await workflowEngine.HandleJobResumedAsync(payload.JobId, CancellationToken.None);

            await runtimeService.ExecuteRestoredAgentAsync(
                payload.JobId,
                payload.QuestionId,
                payload.AnswersJson,
                CancellationToken.None);

            var jobDataAccess = scope.ServiceProvider.GetRequiredService<IJobDataAccess>();
            var finishedJob = await jobDataAccess.GetByIdAsync(payload.JobId, CancellationToken.None);
            if (finishedJob?.WorkflowExecutionId.HasValue == true)
            {
                if (finishedJob.Status == JobStatus.Completed)
                    await workflowEngine.HandleJobCompletedAsync(payload.JobId, finishedJob.FinalResponse, CancellationToken.None);
                else if (finishedJob.Status == JobStatus.WaitingForInput)
                    await workflowEngine.HandleJobWaitingForInputAsync(payload.JobId, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resume job from Redis message: {Message}", message);
        }
    }

    private sealed record ResumePayload(Guid JobId, Guid QuestionId, string AnswersJson);
}
