using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.Models;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Agents.Middleware;
using System.IO;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Concrete implementation of IChatAgentRunner that consolidates shared agent
/// assembly and execution logic formerly duplicated in AgentRuntimeService
/// and ToolRetrieverService.
/// </summary>
public class ChatAgentRunner : IChatAgentRunner
{
    private readonly IAgentSkillDataAccess _agentSkillDataAccess;
    private readonly IAgentSkillFolderDataAccess _agentSkillFolderDataAccess;
    private readonly IConversationSnapshotRepository _snapshotRepository;
    private readonly ILogger<ChatAgentRunner> _logger;

    public ChatAgentRunner(
        IAgentSkillDataAccess agentSkillDataAccess,
        IAgentSkillFolderDataAccess agentSkillFolderDataAccess,
        IConversationSnapshotRepository snapshotRepository,
        ILogger<ChatAgentRunner> logger)
    {
        _agentSkillDataAccess = agentSkillDataAccess;
        _agentSkillFolderDataAccess = agentSkillFolderDataAccess;
        _snapshotRepository = snapshotRepository;
        _logger = logger;
    }

    public async Task<string?> RunAsync(
        IChatClient chatClient,
        Agent agentEntity,
        IList<AIFunction> tools,
        string message,
        IReadOnlyList<AgentImageRef>? images,
        JobTrackingContext? jobTracking,
        object? session = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Load skills and build ChatClientAgentOptions
        var skills = await _agentSkillDataAccess.GetSkillsByAgentIdAsync(agentEntity.Id, cancellationToken);
        var skillFolders = await _agentSkillFolderDataAccess.GetFoldersByAgentIdAsync(agentEntity.Id, cancellationToken);

        var chatOptions = new ChatOptions
        {
            Instructions = agentEntity.CustomInstructions ?? agentEntity.ProjectPrinciples,
            Tools = tools.Cast<AITool>().ToList()
        };

        var agentOptions = new ChatClientAgentOptions
        {
            Name = agentEntity.Name,
            ChatOptions = chatOptions
        };

        // Step 2: Attach skills as context providers if any exist
        if (skills.Count > 0 || skillFolders.Count > 0)
        {
#pragma warning disable MAAI001
            var inlineSkills = skills
                .Select(s => new AgentInlineSkill(
                    new AgentSkillFrontmatter(s.Name, s.Description),
                    s.Instructions))
                .ToArray();

            var builder = new AgentSkillsProviderBuilder().UseSkills(inlineSkills);

            foreach (var folder in skillFolders)
            {
                if (Directory.Exists(folder.FolderPath))
                    builder.UseFileSkill(folder.FolderPath);
                else
                    _logger.LogWarning("Skill folder path '{FolderPath}' not found for agent {AgentId}; skipping.", folder.FolderPath, agentEntity.Id);
            }

            agentOptions.AIContextProviders = [builder.Build()];
#pragma warning restore MAAI001

            _logger.LogDebug(
                "Attached {SkillCount} inline skills and {FolderCount} skill folders to agent {AgentName} ({AgentId})",
                skills.Count, skillFolders.Count, agentEntity.Name, agentEntity.Id);
        }

        // Step 3: Wrap chat client with tracking middleware if job tracking is enabled
        IChatClient effectiveChatClient = chatClient;
        if (jobTracking is not null)
        {
            var chatMiddleware = new JobChatClientMiddlewareHandler(
                jobTracking.StepWriter,
                jobTracking.JobId,
                jobTracking.WorkspaceId);

            effectiveChatClient = chatClient
                .AsBuilder()
                .Use(
                    getResponseFunc: (msgs, opts, inner, ct) => chatMiddleware.HandleAsync(msgs, opts, inner, ct),
                    getStreamingResponseFunc: null)
                .Build();
        }

        // Step 4: Create agent from wrapped chat client
        var agent = effectiveChatClient.AsAIAgent(agentOptions);

        // Step 5: If job tracking is enabled, wrap agent with run and function-calling middleware
        if (jobTracking is not null)
        {
            var runMiddleware = new JobRunMiddlewareHandler(
                jobTracking.StepWriter,
                jobTracking.JobId,
                jobTracking.WorkspaceId);

            var funcMiddleware = new JobFunctionCallingMiddlewareHandler(
                jobTracking.StepWriter,
                jobTracking.JobId,
                jobTracking.WorkspaceId,
                jobTracking.JobCancellationToken);

            var agentWithMiddleware = agent
                .AsBuilder()
                .Use(
                    runFunc: (msgs, sessionParam, opts, inner, ct) =>
                        runMiddleware.HandleAsync(msgs, sessionParam, opts, inner, ct),
                    runStreamingFunc: null)
                .Use((a, ctx, next, ct) => funcMiddleware.HandleAsync(a, ctx, next, ct))
                .Build();

#pragma warning disable MAAI001
            var agentSession = session as Microsoft.Agents.AI.AgentSession;
            agentSession ??= await agent.CreateSessionAsync(cancellationToken);
#pragma warning restore MAAI001

            var response = await agentWithMiddleware.RunAsync(await BuildChatMessageAsync(message, images, cancellationToken), session: agentSession, cancellationToken: cancellationToken);

            if (jobTracking.SuspendedQuestionId is not null)
            {
#pragma warning disable MAAI001
                var serialized = await agent.SerializeSessionAsync(agentSession, cancellationToken: cancellationToken);
#pragma warning restore MAAI001

                var snapshot = AgentConversationSnapshot.Create(
                    jobTracking.JobId,
                    agentEntity.Id,
                    serialized.ToString());

                await _snapshotRepository.SaveAsync(snapshot, cancellationToken);
            }

            return response.Text;
        }
        else
        {
            var response = await agent.RunAsync(await BuildChatMessageAsync(message, images, cancellationToken), cancellationToken: cancellationToken);
            return response.Text;
        }
    }

    private async Task<ChatMessage> BuildChatMessageAsync(
        string message,
        IReadOnlyList<AgentImageRef>? images,
        CancellationToken cancellationToken)
    {
        if (images is null || images.Count == 0)
            return new ChatMessage(ChatRole.User, message);

        var contents = new List<AIContent> { new TextContent(message) };

        foreach (var img in images)
        {
            var content = await LoadImageContentAsync(img, cancellationToken);
            if (content is not null)
                contents.Add(content);
        }

        return new ChatMessage(ChatRole.User, contents);
    }

    private async Task<AIContent?> LoadImageContentAsync(
        AgentImageRef img,
        CancellationToken cancellationToken)
    {
        try
        {
            // Pre-fetched bytes (e.g. authenticated Jira attachment)
            if (img.Bytes is { Length: > 0 })
                return new DataContent(img.Bytes, img.MimeType);

            // Local file path — load from disk
            var localPath = img.Source.Replace("file:///", string.Empty).Replace("file://", string.Empty);
            if (!img.Source.StartsWith("http", StringComparison.OrdinalIgnoreCase) && File.Exists(localPath))
                return await DataContent.LoadFromAsync(localPath);

            // Public HTTPS URL — let the model fetch it directly
            if (img.Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return new UriContent(new Uri(img.Source), img.MimeType);

            _logger.LogWarning("Skipping image ref '{Source}': file not found or unsupported scheme", img.Source);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load image content for '{Source}'; skipping", img.Source);
            return null;
        }
    }
}
