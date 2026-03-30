using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using System.Text;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Builds execution context prompts for AI agents from ticket data.
/// </summary>
public class AgentContextBuilder : IAgentContextBuilder
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ITicketProviderFactory _ticketProviderFactory;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;

    public AgentContextBuilder(
        ITicketDataAccess ticketDataAccess,
        IIntegrationDataAccess integrationDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        IAgentToolActionDataAccess agentToolActionDataAccess)
    {
        _ticketDataAccess = ticketDataAccess;
        _integrationDataAccess = integrationDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _agentToolActionDataAccess = agentToolActionDataAccess;
    }

    public async Task<string> BuildContextPromptAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        string result = string.Empty;
        if (ticket.IsInternal)
        {
            return await BuildInternalTicketPromptAsync(ticket, cancellationToken);
        }
        else
        {
            return await BuildExternalTicketPromptAsync(ticket, cancellationToken);
        }
    }

    private async Task<string> BuildInternalTicketPromptAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        var comments = await _ticketDataAccess.GetCommentsByTicketIdAsync(
            ticket.Id,
            cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("[Ticket Context]");
        sb.AppendLine($"- Ticket ID: {ticket.Id}");
        sb.AppendLine($"- Workspace ID: {ticket.WorkspaceId}");
        sb.AppendLine($"- Title: {ticket.Title}");
        sb.AppendLine($"- Description: {ticket.Description}");
        sb.AppendLine();
        sb.AppendLine("[Comment History]");

        if (comments.Any())
        {
            foreach (var comment in comments)
            {
                sb.AppendLine($"• {comment.Author} ({comment.CreatedAt:yyyy-MM-dd HH:mm} UTC): {comment.Content}");
            }
        }
        else
        {
            sb.AppendLine("No comments yet.");
        }

        return sb.ToString();
    }

    private async Task<string> BuildExternalTicketPromptAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        if (!ticket.IntegrationId.HasValue || string.IsNullOrEmpty(ticket.ExternalTicketId))
        {
            throw new InvalidOperationException(
                $"External ticket {ticket.Id} is missing IntegrationId or ExternalTicketId");
        }

        var integration = await _integrationDataAccess.GetByIdAsync(
            ticket.IntegrationId.Value,
            cancellationToken);

        if (integration == null)
        {
            throw new InvalidOperationException(
                $"Integration {ticket.IntegrationId.Value} not found");
        }

        var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
        if (provider == null)
        {
            throw new InvalidOperationException(
                $"No provider available for {integration.Provider}");
        }

        var externalTicket = await provider.GetTicketByIdAsync(
            integration,
            ticket.ExternalTicketId,
            cancellationToken);

        if (externalTicket == null)
        {
            throw new InvalidOperationException(
                $"External ticket {ticket.ExternalTicketId} not found in {integration.Provider}");
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Ticket Context]");
        sb.AppendLine($"- Ticket ID: {ticket.Id}");
        sb.AppendLine($"- Workspace ID: {ticket.WorkspaceId}");
        sb.AppendLine($"- External Ticket ID: {externalTicket.ExternalTicketId}");
        sb.AppendLine($"- External URL: {externalTicket.ExternalUrl}");
        sb.AppendLine($"- Title: {externalTicket.Title}");
        sb.AppendLine($"- Description: {externalTicket.Description}");
        sb.AppendLine($"- Status: {externalTicket.StatusName}");
        sb.AppendLine($"- Priority: {externalTicket.PriorityName}");
        sb.AppendLine();
        sb.AppendLine("[Comment History]");

        if (externalTicket.Comments.Any())
        {
            foreach (var comment in externalTicket.Comments)
            {
                var timestamp = comment.Timestamp.HasValue ? $" ({comment.Timestamp.Value:yyyy-MM-dd HH:mm} UTC)" : "";
                sb.AppendLine($"• {comment.Author}{timestamp}: {comment.Content}");
            }
        }
        else
        {
            sb.AppendLine("No comments yet.");
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public async Task<string> BuildAgentContextWithIntegrationsAsync(
        Ticket ticket,
        Agent agent,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Build base ticket context
        var contextPrompt = await BuildContextPromptAsync(ticket, cancellationToken);

        // Phase 2: Determine which external provider types the agent's tools require.
        // INTERNAL tools are excluded — they never need an integrationId.
        var externalProviderTypes = await _agentToolActionDataAccess
            .GetExternalProviderTypesByAgentIdAsync(agent.Id, cancellationToken);

        if (externalProviderTypes.Count > 0)
        {
            // Load active integration summaries scoped to this workspace + provider types.
            // Credential-safe projection: only Id, Name, Provider are fetched.
            var integrationSummaries = await _integrationDataAccess
                .GetActiveByWorkspaceAndProvidersAsync(
                    ticket.WorkspaceId,
                    externalProviderTypes,
                    cancellationToken);

            contextPrompt = EnrichContextWithIntegrations(contextPrompt, integrationSummaries);
        }

        // Phase 3: Append [Project Principles] block for review agents.
        // No-op for non-review agents (ProjectPrinciples is null).
        if (!string.IsNullOrWhiteSpace(agent.ProjectPrinciples))
        {
            var sb = new StringBuilder(contextPrompt);
            sb.AppendLine();
            sb.AppendLine("[Project Principles]");
            sb.Append(agent.ProjectPrinciples);
            contextPrompt = sb.ToString();
        }

        return contextPrompt;
    }

    /// <summary>
    /// Appends a structured integration context block to the context prompt.
    /// When <paramref name="integrationSummaries"/> is null or empty, the original
    /// context prompt is returned unchanged — no section is injected.
    /// </summary>
    private static string EnrichContextWithIntegrations(
        string contextPrompt,
        IReadOnlyList<Orchestra.Application.Integrations.DTOs.IntegrationSummaryDto>? integrationSummaries)
    {
        if (integrationSummaries == null || integrationSummaries.Count == 0)
            return contextPrompt;

        var sb = new StringBuilder(contextPrompt);
        sb.AppendLine();
        sb.AppendLine("[Available Integrations]");

        foreach (var summary in integrationSummaries)
        {
            sb.AppendLine($"- ID: {summary.Id} | Name: {summary.Name} | Provider: {summary.Provider}");
        }

        return sb.ToString();
    }
}
