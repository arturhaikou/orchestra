using Microsoft.Extensions.AI;
using Orchestra.Application.Agents.Models;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Application.Agents.Services;

/// <summary>
/// Service for assembling and executing a chat-based agent with consolidated middleware handling,
/// skills context, and tool integration. Encapsulates the shared agent construction logic
/// to eliminate duplication between direct agent execution and sub-agent invocation.
/// </summary>
public interface IChatAgentRunner
{
    /// <summary>
    /// Assembles a chat agent with the provided tools and optional job tracking,
    /// applies middleware (chat and run-level) if job tracking is present,
    /// and executes it with the given message.
    /// </summary>
    /// <param name="chatClient">The resolved chat client for the agent's model</param>
    /// <param name="agentEntity">The agent domain entity with configuration (name, instructions, etc.)</param>
    /// <param name="tools">The AIFunction tools to attach to the agent</param>
    /// <param name="message">The input message to run the agent with</param>
    /// <param name="images">Optional image references to include as multimodal content in the agent's input message</param>
    /// <param name="jobTracking">Optional job tracking context; if null, runs without tracking middleware</param>
    /// <param name="session">Optional session object for restoring agent conversation state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The agent's response text, or null if the agent produced no text response</returns>
    Task<string?> RunAsync(
        IChatClient chatClient,
        Agent agentEntity,
        IList<AIFunction> tools,
        string message,
        IReadOnlyList<AgentImageRef>? images,
        JobTrackingContext? jobTracking,
        object? session = null,
        CancellationToken cancellationToken = default);
}
