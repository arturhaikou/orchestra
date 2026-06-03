using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Application.Jobs.DTOs;

public class JobTrackingContext(
    IJobStepWriter stepWriter,
    Guid jobId,
    Guid workspaceId,
    Guid? workflowExecutionId = null)
{
    public IJobStepWriter StepWriter { get; } = stepWriter;
    public Guid JobId { get; } = jobId;
    public Guid WorkspaceId { get; } = workspaceId;
    public Guid? WorkflowExecutionId { get; } = workflowExecutionId;

    /// <summary>
    /// Set by AskQuestionsFunction when the agent asks a question.
    /// ChatAgentRunner reads this after RunAsync returns to trigger session serialization.
    /// AgentRuntimeService reads this to decide whether to suspend the job.
    /// </summary>
    public Guid? SuspendedQuestionId { get; set; }
}
