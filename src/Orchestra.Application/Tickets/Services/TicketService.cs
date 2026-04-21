using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Microsoft.Extensions.Logging;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Orchestrator service for ticket operations.
/// Delegates to specialized services:
/// - ITicketQueryService for read operations
/// - ITicketCommandService for write operations
/// - IExternalTicketFetchingService for multi-provider pagination
/// - ITicketMaterializationService for external ticket materialization
/// </summary>
public class TicketService : ITicketService
{
    private readonly ITicketQueryService _queryService;
    private readonly ITicketCommandService _commandService;
    private readonly ITicketCommentService _commentService;
    private readonly ILogger<TicketService> _logger;
    private readonly ITicketEnrichmentService _enrichmentService;
    private readonly IWorkspaceDataAccess _workspaceDataAccess;
    private readonly IWorkspaceAIProviderRepository _aiProviderRepository;

    public TicketService(
        ITicketQueryService queryService,
        ITicketCommandService commandService,
        ITicketCommentService commentService,
        ITicketEnrichmentService enrichmentService,
        IWorkspaceDataAccess workspaceDataAccess,
        IWorkspaceAIProviderRepository aiProviderRepository,
        ILogger<TicketService> logger)
    {
        _queryService = queryService;
        _commandService = commandService;
        _commentService = commentService;
        _enrichmentService = enrichmentService;
        _workspaceDataAccess = workspaceDataAccess;
        _aiProviderRepository = aiProviderRepository;
        _logger = logger;
    }

    public async Task<TicketDto> CreateTicketAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken = default)
        => await _commandService.CreateTicketAsync(userId, request, cancellationToken);

    public async Task<PaginatedTicketsResponse> GetTicketsAsync(
        Guid workspaceId,
        Guid userId,
        string? pageToken = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        => await _queryService.GetTicketsAsync(workspaceId, userId, pageToken, pageSize, cancellationToken);

    public async Task<TicketDto> GetTicketByIdAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _queryService.GetTicketByIdAsync(ticketId, userId, cancellationToken);

    public async Task<List<TicketStatusDto>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
        => await _queryService.GetAllStatusesAsync(cancellationToken);

    public async Task<List<TicketPriorityDto>> GetAllPrioritiesAsync(CancellationToken cancellationToken = default)
        => await _queryService.GetAllPrioritiesAsync(cancellationToken);

    public async Task<TicketDto> UpdateTicketAsync(
        string ticketId,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
        => await _commandService.UpdateTicketAsync(ticketId, userId, request, cancellationToken);

    public async Task<TicketDto> ConvertToExternalAsync(
        string ticketId,
        Guid userId,
        Guid integrationId,
        string issueTypeName,
        CancellationToken cancellationToken = default)
        => await _commandService.ConvertToExternalAsync(ticketId, userId, integrationId, issueTypeName, cancellationToken);

    public async Task DeleteTicketAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _commandService.DeleteTicketAsync(ticketId, userId, cancellationToken);

    public async Task<CommentDto> AddCommentAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _commentService.AddCommentAsync(ticketId, userId, request, cancellationToken);
    }

    public async Task<TicketSummarizationResponse> GenerateSummaryAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch the ticket (handles both internal and external tickets)
        // This call validates authentication and workspace membership before anything else.
        var ticketDto = await _queryService.GetTicketByIdAsync(ticketId, userId, cancellationToken);

        // 2. Look up the workspace to check the AI Summarization flag
        var workspace = await _workspaceDataAccess.GetByIdAsync(ticketDto.WorkspaceId, cancellationToken);

        // Defensive check: workspace should exist if ticket exists (referential integrity).
        // If null, log warning and return feature-disabled response as a safe fallback.
        if (workspace == null)
        {
            _logger.LogWarning(
                "Workspace {WorkspaceId} not found for ticket {TicketId}, returning feature-disabled response.",
                ticketDto.WorkspaceId, ticketId);
            return new TicketSummarizationResponse(
                null,
                true,
                "Summarization is not enabled for this workspace. Go to workspace settings to enable it.");
        }

        // 3. Check if AI Summarization is enabled for this workspace
        if (!workspace.IsAiSummarizationEnabled)
        {
            return new TicketSummarizationResponse(
                null,
                true,
                "Summarization is not enabled for this workspace. Go to workspace settings to enable it.");
        }

        // 4a. Resolve effective model: feature-specific model → provider default → fail fast.
        // AiSummarizationModelId is the workspace-level override; when absent, fall back to the
        // DefaultModelId stored on AIProviderConfiguration (the sole authoritative source).
        var aiConfig = await _aiProviderRepository.GetByWorkspaceIdAsync(workspace.Id, cancellationToken);
        var effectiveModelId = workspace.AiSummarizationModelId ?? aiConfig?.DefaultModelId
            ?? throw new InvalidOperationException(
                $"No AI summarization model configured for workspace {workspace.Id}. "
                + "Set AiSummarizationModelId or configure a default model in the AI provider settings.");

        var content = _enrichmentService.BuildSummaryContent(ticketDto);

        // 5. Generate summary — modelId is baked into the chat client at construction.
        string summary = await _enrichmentService.GenerateSummaryAsync(
            content, ticketDto.WorkspaceId, effectiveModelId, cancellationToken);

        // 6. Return success response with summary populated
        var summarizedTicket = ticketDto with { Summary = summary };
        return new TicketSummarizationResponse(summarizedTicket, false, null);
    }
}