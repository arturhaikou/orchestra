using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.AiCliIntegrations;

public sealed class GithubCopilotCliClient : IAiCliClient
{
    private static readonly IList<string> FullToolSet = ["read", "write", "shell"];
    private static readonly IList<string> ReadOnlyToolSet = ["read"];

    private readonly CopilotClient _copilotClient;
    private readonly string? _modelId;
    private readonly string? _reasoningEffort;
    private readonly IList<string> _allowedTools;
    private readonly ILogger? _logger;

    private GithubCopilotCliClient(CopilotClient copilotClient, string? modelId, string? reasoningEffort, IList<string> allowedTools, ILogger? logger = null)
    {
        _copilotClient = copilotClient;
        _modelId = modelId;
        _reasoningEffort = reasoningEffort;
        _allowedTools = allowedTools;
        _logger = logger;
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
            CliArgs = ["--autopilot"],
            CliPath = cliPath
        });

        await copilotClient.StartAsync(cancellationToken);
        return new GithubCopilotCliClient(copilotClient, modelId, reasoningEffort, FullToolSet);
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
            CliArgs = ["--autopilot"],
            CliPath = cliPath
        });

        await copilotClient.StartAsync(cancellationToken);
        return new GithubCopilotCliClient(copilotClient, modelId, reasoningEffort, ReadOnlyToolSet);
    }

    public AIAgent AsAgent(string? instructions, string name, IReadOnlyList<string>? skillDirectories = null, IReadOnlyList<string>? skillNames = null)
    {
        var config = BuildSessionConfig(instructions, name, _allowedTools, skillDirectories: skillDirectories, skillNames: skillNames);
        return _copilotClient.AsAIAgent(sessionConfig: config, name: name);
    }

    public AIAgent AsReadOnlyAgent(string? instructions, string name, IReadOnlyList<string>? skillDirectories = null, IReadOnlyList<string>? skillNames = null)
    {
        var config = BuildSessionConfig(instructions, name, ReadOnlyToolSet, skillDirectories: skillDirectories, skillNames: skillNames);
        return _copilotClient.AsAIAgent(sessionConfig: config, name: name);
    }

    /// <summary>
    /// Executes a prompt on a new CLI session, writing job step notifications for tool calls
    /// and assistant messages as they arrive via session events.
    /// </summary>
    public async Task<string> RunWithTrackingAsync(
        string prompt,
        string? instructions,
        string name,
        IJobStepWriter stepWriter,
        Guid jobId,
        Guid workspaceId,
        IReadOnlyList<AIFunction>? customTools = null,
        IReadOnlyList<string>? skillDirectories = null,
        IReadOnlyList<string>? skillNames = null,
        CancellationToken cancellationToken = default)
    {
        var config = BuildSessionConfig(instructions, name, _allowedTools, customTools, skillDirectories, skillNames);
        var toolStartTimes = new ConcurrentDictionary<string, (string ToolName, Stopwatch Timer)>();

        await using var session = await _copilotClient.CreateSessionAsync(config, cancellationToken);

        using var _ = session.On(evt => HandleSessionEvent(evt, stepWriter, jobId, workspaceId, toolStartTimes, _logger, cancellationToken));

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(15),
            cancellationToken: cancellationToken);

        return response?.Data.Content;
    }

    private SessionConfig BuildSessionConfig(string? instructions, string name, IList<string> tools, IReadOnlyList<AIFunction>? customTools = null, IReadOnlyList<string>? skillDirectories = null, IReadOnlyList<string>? skillNames = null)
    {
        var agentConfig = new CustomAgentConfig
        {
            Name = name,
            Prompt = instructions ?? string.Empty,
            Tools = tools
        };

        if (skillNames?.Count > 0)
            agentConfig.Skills = skillNames.ToList();

        var config = new SessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
            CustomAgents = [agentConfig],
            Agent = name
        };

        if (skillDirectories?.Count > 0)
            config.SkillDirectories = skillDirectories.ToList();

        if (_modelId is not null)
            config.Model = _modelId;

        if (_reasoningEffort is not null)
            config.ReasoningEffort = _reasoningEffort;

        if (customTools?.Count > 0)
        {
            config.Tools = customTools.ToList();
            foreach (var toolName in customTools.Select(t => t.Name).ToList()){
                agentConfig.Tools.Add(toolName);
            }
        }

        return config;
    }

    private static void HandleSessionEvent(
        SessionEvent evt,
        IJobStepWriter stepWriter,
        Guid jobId,
        Guid workspaceId,
        ConcurrentDictionary<string, (string ToolName, Stopwatch Timer)> toolStartTimes,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        switch (evt)
        {
            case ToolExecutionStartEvent startEvt:
                HandleToolStart(startEvt, stepWriter, jobId, workspaceId, toolStartTimes, logger);
                break;

            case ToolExecutionCompleteEvent completeEvt:
                HandleToolComplete(completeEvt, stepWriter, jobId, workspaceId, toolStartTimes, logger);
                break;

            case AssistantMessageEvent messageEvt when messageEvt.Data?.Content is { Length: > 0 } content:
                FireAndForget(stepWriter.WriteAsync(jobId, workspaceId, JobStepType.ThinkingMessage, content: content), logger);
                break;
        }
    }

    private static void HandleToolStart(
        ToolExecutionStartEvent startEvt,
        IJobStepWriter stepWriter,
        Guid jobId,
        Guid workspaceId,
        ConcurrentDictionary<string, (string ToolName, Stopwatch Timer)> toolStartTimes,
        ILogger? logger)
    {
        var toolName = startEvt.Data?.ToolName ?? "unknown";
        var toolCallId = startEvt.Data?.ToolCallId ?? string.Empty;
        var argsJson = SerializeArguments(startEvt.Data?.Arguments);

        if (!string.IsNullOrEmpty(toolCallId))
            toolStartTimes[toolCallId] = (toolName, Stopwatch.StartNew());

        FireAndForget(stepWriter.WriteAsync(jobId, workspaceId, JobStepType.ToolCallStarted,
            content: argsJson, toolName: toolName, isJson: true), logger);
    }

    private static void HandleToolComplete(
        ToolExecutionCompleteEvent completeEvt,
        IJobStepWriter stepWriter,
        Guid jobId,
        Guid workspaceId,
        ConcurrentDictionary<string, (string ToolName, Stopwatch Timer)> toolStartTimes,
        ILogger? logger)
    {
        var toolCallId = completeEvt.Data?.ToolCallId ?? string.Empty;
        toolStartTimes.TryRemove(toolCallId, out var startEntry);

        var toolName = startEntry.ToolName ?? "unknown";
        var durationMs = startEntry.Timer?.ElapsedMilliseconds;
        var resultJson = SerializeResult(completeEvt.Data?.Result);
        var isError = completeEvt.Data?.Success == false;

        FireAndForget(stepWriter.WriteAsync(jobId, workspaceId, JobStepType.ToolCallCompleted,
            content: resultJson, toolName: toolName, isJson: true,
            durationMs: durationMs, isError: isError), logger);
    }

    private static string? SerializeArguments(object? arguments)
    {
        if (arguments is null) return null;
        try { return JsonSerializer.Serialize(arguments); }
        catch { return arguments.ToString(); }
    }

    private static string? SerializeResult(object? result)
    {
        if (result is null) return null;
        try { return JsonSerializer.Serialize(result); }
        catch { return result.ToString(); }
    }

    private static void FireAndForget(Task task, ILogger? logger) =>
        task.ContinueWith(
            t => logger?.LogWarning(t.Exception, "Failed to write job step"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    public async ValueTask DisposeAsync()
        => await _copilotClient.DisposeAsync();
}

