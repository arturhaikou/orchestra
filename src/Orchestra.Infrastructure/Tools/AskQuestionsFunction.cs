using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Tools;

public static class AskQuestionsFunction
{
    public static AIFunction Create(
        JobTrackingContext jobTracking,
        Guid agentId,
        IAgentQuestionRepository questionRepository,
        INotificationService notificationService,
        ILogger logger)
    {
        return AIFunctionFactory.Create(
            async (
                [Description(
                    "Array of questions to ask the user. Each entry: " +
                    "'question' (required string), 'hint' (optional string), " +
                    "'type' ('Text'|'Radio'|'Checkbox'), " +
                    "'options' (string array, required for Radio/Checkbox), " +
                    "'allowCustom' (bool, adds free-text Other… option to Radio/Checkbox).")]
                QuestionItem[] questions,
                CancellationToken cancellationToken) =>
            {
                var questionsJson = JsonSerializer.Serialize(questions);

                var agentQuestion = AgentQuestion.Create(
                    jobId: jobTracking.JobId,
                    workspaceId: jobTracking.WorkspaceId,
                    agentId: agentId,
                    questionsJson: questionsJson);

                await questionRepository.SaveAsync(agentQuestion, cancellationToken);

                jobTracking.SuspendedQuestionId = agentQuestion.Id;

                await notificationService.NotifyAgentQuestionAskedAsync(
                    jobTracking.WorkspaceId, agentQuestion.Id, cancellationToken);

                logger.LogInformation(
                    "Agent asked {Count} question(s) for job {JobId}. QuestionId={QuestionId}",
                    questions.Length, jobTracking.JobId, agentQuestion.Id);

                return $"WAITING_FOR_USER_INPUT:{agentQuestion.Id}";
            },
            name: "ask_questions",
            description:
                "Ask the user one or more questions before continuing. Pass all questions in a single call. " +
                "Use type='Text' for free-form answers, 'Radio' for single-choice, 'Checkbox' for multi-choice. " +
                "Set allowCustom=true when the user might need an option not in the list. " +
                "After calling this tool, output the returned string verbatim and stop.");
    }
}
