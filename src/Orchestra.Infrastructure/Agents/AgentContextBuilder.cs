using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Agents.Models;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
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
    private readonly IAgentSubAgentDataAccess _agentSubAgentDataAccess;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IAiCliIntegrationDataAccess _cliIntegrationDataAccess;
    private readonly ITicketImageExtractor _imageExtractor;
    private readonly JiraImageFetcher _jiraImageFetcher;

    public AgentContextBuilder(
        ITicketDataAccess ticketDataAccess,
        IIntegrationDataAccess integrationDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        IAgentToolActionDataAccess agentToolActionDataAccess,
        IAgentSubAgentDataAccess agentSubAgentDataAccess,
        IAgentDataAccess agentDataAccess,
        IAiCliIntegrationDataAccess cliIntegrationDataAccess,
        ITicketImageExtractor imageExtractor,
        JiraImageFetcher jiraImageFetcher)
    {
        _ticketDataAccess = ticketDataAccess;
        _integrationDataAccess = integrationDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _agentToolActionDataAccess = agentToolActionDataAccess;
        _agentSubAgentDataAccess = agentSubAgentDataAccess;
        _agentDataAccess = agentDataAccess;
        _cliIntegrationDataAccess = cliIntegrationDataAccess;
        _imageExtractor = imageExtractor;
        _jiraImageFetcher = jiraImageFetcher;
    }

    public async Task<AgentContextInput> BuildContextPromptAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        if (ticket.IsInternal)
            return await BuildInternalTicketPromptAsync(ticket, cancellationToken);
        else
            return await BuildExternalTicketPromptAsync(ticket, cancellationToken);
    }

    private async Task<AgentContextInput> BuildInternalTicketPromptAsync(
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
        sb.AppendLine($"- Description:\n\n{ticket.Description}");
        sb.AppendLine();
        sb.AppendLine("[Comment History]");

        if (comments.Any())
        {
            foreach (var comment in comments)
                sb.AppendLine($"• {comment.Author} ({comment.CreatedAt:yyyy-MM-dd HH:mm} UTC): {comment.Content}");
        }
        else
        {
            sb.AppendLine("No comments yet.");
        }

        // Extract image refs from description and all comments
        var allMarkdown = new StringBuilder();
        allMarkdown.Append(ticket.Description ?? string.Empty);
        foreach (var comment in comments)
            allMarkdown.Append('\n').Append(comment.Content);

        var sources = _imageExtractor.Extract(allMarkdown.ToString());
        var imageRefs = sources
            .Select(s => new AgentImageRef(
                Source: s,
                MimeType: ResolveMimeTypeFromPath(s),
                FileName: Path.GetFileName(s.Replace("file:///", string.Empty).Replace("file://", string.Empty))))
            .ToList();

        AppendImagesSectionIfAny(sb, imageRefs);

        return new AgentContextInput(sb.ToString(), imageRefs);
    }

    private async Task<AgentContextInput> BuildExternalTicketPromptAsync(
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
        sb.AppendLine($"- Description:\n\n{externalTicket.Description}");
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

        // Extract image URLs from description and all comments, then fetch bytes in-memory
        var allMarkdown = new StringBuilder();
        allMarkdown.Append(externalTicket.Description);
        foreach (var comment in externalTicket.Comments)
            allMarkdown.Append('\n').Append(comment.Content);

        var imageUrls = _imageExtractor.Extract(allMarkdown.ToString());
        var imageRefs = await _jiraImageFetcher.FetchAsync(integration, imageUrls, cancellationToken);

        AppendImagesSectionIfAny(sb, imageRefs);

        return new AgentContextInput(sb.ToString(), imageRefs);
    }

    /// <inheritdoc/>
    public async Task<AgentContextInput> BuildAgentContextWithIntegrationsAsync(
        Ticket ticket,
        Agent agent,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Build base ticket context (text + images)
        var contextInput = await BuildContextPromptAsync(ticket, cancellationToken);
        var contextPrompt = contextInput.TextPrompt;
        var images = contextInput.Images;

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

        // Phase 3: Inject [Available CLI Integrations] for push_branch tool disambiguation.
        // Collects CLI integration IDs from the agent itself and any assigned sub-agents.
        var cliIntegrationIds = await CollectCliIntegrationIdsAsync(agent, cancellationToken);
        if (cliIntegrationIds.Count > 0)
        {
            var cliIntegrations = new List<Orchestra.Domain.Entities.AiCliIntegration>();
            foreach (var cliId in cliIntegrationIds)
            {
                var cli = await _cliIntegrationDataAccess.GetByIdAsync(cliId, cancellationToken);
                if (cli != null)
                    cliIntegrations.Add(cli);
            }
            contextPrompt = EnrichContextWithCliIntegrations(contextPrompt, cliIntegrations);
        }

        // Phase 4: Append [Project Principles] block for review agents.
        // No-op for non-review agents (ProjectPrinciples is null).
        if (!string.IsNullOrWhiteSpace(agent.ProjectPrinciples))
        {
            var sb = new StringBuilder(contextPrompt);
            sb.AppendLine();
            sb.AppendLine("[Project Principles]");
            sb.Append(agent.ProjectPrinciples);
            contextPrompt = sb.ToString();
        }

        return new AgentContextInput(contextPrompt, images);
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

    private static string EnrichContextWithCliIntegrations(
        string contextPrompt,
        IReadOnlyList<Orchestra.Domain.Entities.AiCliIntegration> cliIntegrations)
    {
        if (cliIntegrations.Count == 0)
            return contextPrompt;

        var sb = new StringBuilder(contextPrompt);
        sb.AppendLine();
        sb.AppendLine("[Available CLI Integrations]");
        foreach (var cli in cliIntegrations)
        {
            sb.AppendLine($"- ID: {cli.Id} | Name: {cli.Name} | WorkingDirectory: {cli.WorkingDirectory}");
        }
        return sb.ToString();
    }

    private async Task<List<Guid>> CollectCliIntegrationIdsAsync(
        Agent agent,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();

        if (agent.AiCliIntegrationId.HasValue)
            ids.Add(agent.AiCliIntegrationId.Value);

        var subAgentIds = await _agentSubAgentDataAccess
            .GetSubAgentIdsByParentAgentIdAsync(agent.Id, cancellationToken);

        foreach (var subAgentId in subAgentIds)
        {
            var subAgent = await _agentDataAccess.GetByIdAsync(subAgentId, cancellationToken);
            if (subAgent?.AiCliIntegrationId.HasValue == true)
                ids.Add(subAgent.AiCliIntegrationId.Value);
        }

        return ids;
    }

    /// <summary>
    /// Appends an [Images] section listing each image's source and file name so the agent
    /// can reference them when calling tools like AddCommentAsync or CreateIssueAsync.
    /// No-op when <paramref name="images"/> is empty.
    /// </summary>
    private static void AppendImagesSectionIfAny(StringBuilder sb, IReadOnlyList<AgentImageRef> images)
    {
        if (images.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("[Images]");
        foreach (var img in images)
            sb.AppendLine($"- Source: {img.Source} | FileName: {img.FileName}");
    }

    private static string ResolveMimeTypeFromPath(string source)
    {
        var ext = Path.GetExtension(source.Split('?')[0]).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };
    }
}
