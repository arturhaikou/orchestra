using Microsoft.Agents.AI;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public interface IAiCliClient : IAsyncDisposable
{
    AIAgent AsAgent(string? instructions = null);

    AIAgent AsReadOnlyAgent(string? instructions = null);
}
