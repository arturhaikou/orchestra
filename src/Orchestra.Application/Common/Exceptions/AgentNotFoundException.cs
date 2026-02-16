namespace Orchestra.Application.Common.Exceptions;

public class AgentNotFoundException : Exception
{
    public Guid AgentId { get; }

    public AgentNotFoundException(Guid agentId)
        : base($"Agent with ID '{agentId}' was not found.")
    {
        AgentId = agentId;
    }

    public AgentNotFoundException(Guid agentId, Exception innerException)
        : base($"Agent with ID '{agentId}' was not found.", innerException)
    {
        AgentId = agentId;
    }
}