using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Skills.DTOs;
using Orchestra.Infrastructure.AiCliIntegrations;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// A singleton <see cref="AIAgent"/> proxy that dynamically resolves the real per-request
/// agent from the workspace and agent identifiers embedded in the HTTP route.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton so it can be passed to <c>MapAGUI</c> once at startup.
/// All scoped dependencies (<see cref="IAgentAGUIBuildService"/> and its transitive dependencies)
/// are resolved through a per-invocation <see cref="IServiceScope"/> to honour their lifetimes.
/// </para>
/// <para>
/// Because the AG-UI protocol sends the full conversation history with every request,
/// no session state needs to be preserved between calls. Sessions are therefore ephemeral
/// in-memory markers only.
/// </para>
/// </remarks>
public sealed class DynamicWorkspaceAgent : AIAgent
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;

    public override string? Name => "DynamicWorkspaceAgent";

    public DynamicWorkspaceAgent(
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new DynamicAgentSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Session serialization is not supported for DynamicWorkspaceAgent.");

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Session deserialization is not supported for DynamicWorkspaceAgent.");

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (context, scope) = await BuildContextWithScopeAsync(cancellationToken);
        await using (scope)
        {
            if (context.IsCliAgent)
            {
                var factory = scope.ServiceProvider.GetRequiredService<IAiCliClientFactory>();

                if (context.IsReadOnlyCli)
                {
                    await using var cliClient = await factory.CreateReadOnlyClientAsync(
                        context.AiCliIntegrationId!.Value, context.CliModel, context.CliReasoningEffort, cancellationToken);

                    var cliAgent = cliClient.AsReadOnlyAgent(context.Instructions, context.AgentName);
                    return await cliAgent.RunAsync(messages, options: options, cancellationToken: cancellationToken);
                }
                else
                {
                    await using var cliClient = await factory.CreateClientAsync(
                        context.AiCliIntegrationId!.Value, context.CliModel, context.CliReasoningEffort, cancellationToken);

                    var cliAgent = cliClient.AsAgent(context.Instructions, context.AgentName);
                    return await cliAgent.RunAsync(messages, options: options, cancellationToken: cancellationToken);
                }
            }

            var chatAgent = BuildChatAgent(context);
            return await chatAgent.RunAsync(messages, options: options, cancellationToken: cancellationToken);
        }
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (context, scope) = await BuildContextWithScopeAsync(cancellationToken);
        await using (scope)
        {
            IAsyncEnumerable<AgentResponseUpdate> stream;

            if (context.IsCliAgent)
            {
                var factory = scope.ServiceProvider.GetRequiredService<IAiCliClientFactory>();
                // IAiCliClient lifetime is managed inside the async iterator via a local wrapper.
                // We materialise the full stream before the scope closes so the subprocess stays alive.
                stream = StreamCliAgentAsync(factory, context, messages, options, cancellationToken);
            }
            else
            {
                var chatAgent = BuildChatAgent(context);
                stream = chatAgent.RunStreamingAsync(messages, options: options, cancellationToken: cancellationToken);
            }

            await foreach (var update in stream.ConfigureAwait(false))
                yield return update;
        }
    }

    private static AIAgent BuildChatAgent(AgentAGUIContext context)
    {
        var chatOptions = new ChatOptions
        {
            Instructions = context.Instructions,
            Tools = context.Tools.Cast<AITool>().ToList()
        };

        var agentOptions = new ChatClientAgentOptions
        {
            Name = context.AgentName,
            ChatOptions = chatOptions
        };

        if (context.Skills?.Count > 0 || context.SkillFolderPaths?.Count > 0)
        {
#pragma warning disable MAAI001
            var inlineSkills = (context.Skills ?? [])
                .Select(s => new AgentInlineSkill(new AgentSkillFrontmatter(s.Name, s.Description), s.Instructions))
                .ToArray();

            var builder = new AgentSkillsProviderBuilder().UseSkills(inlineSkills);

            foreach (var folderPath in context.SkillFolderPaths ?? [])
            {
                if (Directory.Exists(folderPath))
                    builder.UseFileSkill(folderPath);
            }

            agentOptions.AIContextProviders = [builder.Build()];
#pragma warning restore MAAI001
        }

        return context.ChatClient!.AsAIAgent(agentOptions);
    }

    public override object? GetService(Type serviceType, object? serviceKey = null) => null;

    private async Task<(AgentAGUIContext Context, AsyncServiceScope Scope)> BuildContextWithScopeAsync(
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context found.");

        var workspaceId = ParseRouteGuid(httpContext, "workspaceId");
        var agentId = ParseRouteGuid(httpContext, "agentId");
        var userId = ParseUserId(httpContext);

        var scope = _scopeFactory.CreateAsyncScope();
        var buildService = scope.ServiceProvider.GetRequiredService<IAgentAGUIBuildService>();

        var context = await buildService.BuildAGUIAgentContextAsync(workspaceId, agentId, userId, cancellationToken);
        if (context is null)
        {
            await scope.DisposeAsync();
            throw new InvalidOperationException($"Agent '{agentId}' was not found in workspace '{workspaceId}'.");
        }

        return (context, scope);
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> StreamCliAgentAsync(
        IAiCliClientFactory factory,
        AgentAGUIContext context,
        IEnumerable<ChatMessage> messages,
        AgentRunOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (context.IsReadOnlyCli)
        {
            await using var cliClient = await factory.CreateReadOnlyClientAsync(
                context.AiCliIntegrationId!.Value, context.CliModel, context.CliReasoningEffort, cancellationToken);

            var cliAgent = cliClient.AsReadOnlyAgent(context.Instructions, context.AgentName);

            await foreach (var update in cliAgent
                .RunStreamingAsync(messages, options: options, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                yield return update;
            }
        }
        else
        {
            await using var cliClient = await factory.CreateClientAsync(
                context.AiCliIntegrationId!.Value, context.CliModel, context.CliReasoningEffort, cancellationToken);

            var cliAgent = cliClient.AsAgent(context.Instructions, context.AgentName);

            await foreach (var update in cliAgent
                .RunStreamingAsync(messages, options: options, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                yield return update;
            }
        }
    }

    private static Guid ParseRouteGuid(HttpContext context, string routeKey)
    {
        var value = context.GetRouteValue(routeKey)?.ToString()
            ?? throw new InvalidOperationException($"Route value '{routeKey}' is missing from the HTTP context.");

        return Guid.TryParse(value, out var result)
            ? result
            : throw new InvalidOperationException($"Route value '{routeKey}' ('{value}') is not a valid GUID.");
    }

    private static Guid ParseUserId(HttpContext context)
    {
        var sub = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Authenticated user ID (NameIdentifier claim) not found in the JWT.");

        return Guid.TryParse(sub, out var result)
            ? result
            : throw new InvalidOperationException($"User ID claim value '{sub}' is not a valid GUID.");
    }

    /// <summary>Ephemeral session marker — no state is persisted across AG-UI requests.</summary>
    private sealed class DynamicAgentSession : AgentSession;
}
