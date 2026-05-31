using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents;

public class AgentSessionRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentSessionRecoveryService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RecoverAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);

        await using var scope = scopeFactory.CreateAsyncScope();
        var jobDataAccess = scope.ServiceProvider.GetRequiredService<IJobDataAccess>();
        var questionRepository = scope.ServiceProvider.GetRequiredService<IAgentQuestionRepository>();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IAgentRuntimeService>();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionEngine>();

        var waitingJobs = await jobDataAccess.GetByStatusAsync(
            JobStatus.WaitingForInput, CancellationToken.None);

        foreach (var job in waitingJobs)
        {
            try
            {
                var answeredQuestion = (await questionRepository
                    .GetPendingByWorkspaceAsync(job.WorkspaceId, CancellationToken.None))
                    .FirstOrDefault(q => q.JobId == job.Id && q.Status == QuestionStatus.Answered);

                if (answeredQuestion is null)
                    continue;

                logger.LogInformation(
                    "Recovering job {JobId} with answered question {QuestionId}.",
                    job.Id, answeredQuestion.Id);

                await runtimeService.ExecuteRestoredAgentAsync(
                    job.Id,
                    answeredQuestion.Id,
                    answeredQuestion.AnswersJson!,
                    CancellationToken.None);

                var finishedJob = await jobDataAccess.GetByIdAsync(job.Id, CancellationToken.None);
                if (finishedJob?.WorkflowExecutionId.HasValue == true)
                {
                    if (finishedJob.Status == JobStatus.Completed)
                        await workflowEngine.HandleJobCompletedAsync(job.Id, finishedJob.FinalResponse, CancellationToken.None);
                    else if (finishedJob.Status == JobStatus.WaitingForInput)
                        await workflowEngine.HandleJobWaitingForInputAsync(job.Id, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to recover job {JobId} on startup.", job.Id);
            }
        }
    }
}
