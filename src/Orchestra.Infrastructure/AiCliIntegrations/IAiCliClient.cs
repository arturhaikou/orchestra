using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public interface IAiCliClient : IAsyncDisposable
{
    AIAgent AsAgent(string? instructions, string name, IReadOnlyList<string>? skillDirectories = null, IReadOnlyList<string>? skillNames = null);

    AIAgent AsReadOnlyAgent(string? instructions, string name, IReadOnlyList<string>? skillDirectories = null, IReadOnlyList<string>? skillNames = null);

    Task<string> RunWithTrackingAsync(
        string prompt,
        string? instructions,
        string name,
        IJobStepWriter stepWriter,
        Guid jobId,
        Guid workspaceId,
        IReadOnlyList<AIFunction>? customTools = null,
        IReadOnlyList<string>? skillDirectories = null,
        IReadOnlyList<string>? skillNames = null,
        CancellationToken cancellationToken = default);
}
