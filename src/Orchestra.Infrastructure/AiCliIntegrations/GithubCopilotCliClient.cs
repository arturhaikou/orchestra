using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public sealed class GithubCopilotCliClient : IAiCliClient
{
    private readonly CopilotClient _copilotClient;
    private readonly string? _modelId;
    private readonly string? _reasoningEffort;

    private GithubCopilotCliClient(CopilotClient copilotClient, string? modelId, string? reasoningEffort)
    {
        _copilotClient = copilotClient;
        _modelId = modelId;
        _reasoningEffort = reasoningEffort;
    }

    public static async Task<GithubCopilotCliClient> CreateAsync(
        string? githubToken,
        bool useLoggedInUser,
        string workingDirectory,
        string? modelId = null,
        string? cliPath = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default)
    {
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            GitHubToken = useLoggedInUser ? null : githubToken,
            UseLoggedInUser = useLoggedInUser,
            Cwd = workingDirectory,
            CliPath = cliPath
        });

        await copilotClient.StartAsync(cancellationToken);
        return new GithubCopilotCliClient(copilotClient, modelId, reasoningEffort);
    }

    public static async Task<GithubCopilotCliClient> CreateReadOnlyAsync(
        string? githubToken,
        bool useLoggedInUser,
        string workingDirectory,
        string? modelId = null,
        string? cliPath = null,
        string? reasoningEffort = null,
        CancellationToken cancellationToken = default)
    {
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            GitHubToken = useLoggedInUser ? null : githubToken,
            UseLoggedInUser = useLoggedInUser,
            Cwd = workingDirectory,
            CliArgs = ["--allow-tool=read"],
            CliPath = cliPath,
        });

        await copilotClient.StartAsync(cancellationToken);
        return new GithubCopilotCliClient(copilotClient, modelId, reasoningEffort);
    }

    public AIAgent AsAgent(string? instructions = null, string? name = null)
    {
        if (_modelId is null)
            return _copilotClient.AsAIAgent(instructions: instructions, name: name);

        var config = new SessionConfig { Model = _modelId, OnPermissionRequest = PermissionHandler.ApproveAll };

        if (_reasoningEffort is not null)
            config.ReasoningEffort = _reasoningEffort;

        if (instructions is not null)
            config.SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = instructions
            };

        return _copilotClient.AsAIAgent(sessionConfig: config, name: name);
    }

    /// <summary>
    /// Returns an AIAgent restricted to read-only tools.
    /// The restriction is enforced at the CLI process level via <c>--allow-tool=read</c>
    /// set during <see cref="CreateReadOnlyAsync"/>. Calling this on a client created
    /// with <see cref="CreateAsync"/> will not enforce any tool restrictions.
    /// </summary>
    public AIAgent AsReadOnlyAgent(string? instructions = null, string? name = null) => AsAgent(instructions, name);

    public async ValueTask DisposeAsync()
        => await _copilotClient.DisposeAsync();
}

