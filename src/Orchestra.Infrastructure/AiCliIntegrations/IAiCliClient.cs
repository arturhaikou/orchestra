using Microsoft.Agents.AI;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public interface IAiCliClient : IAsyncDisposable
{
    AIAgent AsAgent(string? instructions = null, string? name = null);

    AIAgent AsReadOnlyAgent(string? instructions = null, string? name = null);
}
