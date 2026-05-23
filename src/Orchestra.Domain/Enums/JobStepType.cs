namespace Orchestra.Domain.Enums;

public enum JobStepType
{
    AgentStarted = 0,
    ThinkingMessage = 1,
    ToolCallStarted = 2,
    ToolCallCompleted = 3,
    AgentCompleted = 4,
    AgentFailed = 5,
    SubAgentCallStarted = 6,
    SubAgentCallCompleted = 7
}
