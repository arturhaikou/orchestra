using Bogus;

namespace Orchestra.Application.Tests.Builders;

/// <summary>
/// Fluent builder for creating Ticket test entities with sensible defaults.
/// Supports both internal and external tickets.
/// </summary>
public class TicketBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _workspaceId = Guid.NewGuid();
    private string _title = new Faker().Lorem.Sentence();
    private string _description = new Faker().Lorem.Paragraph();
    private Guid? _priorityId = Guid.NewGuid();
    private Guid? _statusId = Guid.NewGuid();
    private bool _isInternal = true;
    private Guid? _integrationId;
    private string? _externalTicketId;
    private Guid? _assignedAgentId;
    private Guid? _assignedWorkflowId;

    /// <summary>
    /// Sets the ticket ID.
    /// </summary>
    public TicketBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the workspace ID.
    /// </summary>
    public TicketBuilder WithWorkspaceId(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    /// <summary>
    /// Sets the ticket title.
    /// </summary>
    public TicketBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the ticket description.
    /// </summary>
    public TicketBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the priority ID.
    /// </summary>
    public TicketBuilder WithPriorityId(Guid? priorityId)
    {
        _priorityId = priorityId;
        return this;
    }

    /// <summary>
    /// Sets the status ID.
    /// </summary>
    public TicketBuilder WithStatusId(Guid? statusId)
    {
        _statusId = statusId;
        return this;
    }

    /// <summary>
    /// Sets whether the ticket is internal or external.
    /// </summary>
    public TicketBuilder AsInternal(bool isInternal = true)
    {
        _isInternal = isInternal;
        return this;
    }

    /// <summary>
    /// Sets the ticket as an external ticket with integration reference.
    /// </summary>
    public TicketBuilder AsExternal(Guid integrationId, string externalTicketId)
    {
        _isInternal = false;
        _integrationId = integrationId;
        _externalTicketId = externalTicketId;
        _priorityId = null;
        _statusId = null;
        return this;
    }

    /// <summary>
    /// Sets the assigned agent.
    /// </summary>
    public TicketBuilder WithAssignedAgent(Guid agentId)
    {
        _assignedAgentId = agentId;
        return this;
    }

    /// <summary>
    /// Sets the assigned workflow.
    /// </summary>
    public TicketBuilder WithAssignedWorkflow(Guid workflowId)
    {
        _assignedWorkflowId = workflowId;
        return this;
    }

    /// <summary>
    /// Builds the Ticket entity for internal tickets.
    /// </summary>
    public Ticket Build()
    {
        if (_isInternal)
        {
            // For internal tickets, ensure we have priority and status
            if (_priorityId == null || _statusId == null)
            {
                _priorityId ??= Guid.NewGuid();
                _statusId ??= Guid.NewGuid();
            }

            return Ticket.Create(
                _workspaceId,
                _title,
                _description,
                _priorityId,
                _statusId,
                _isInternal);
        }

        // For external tickets
        if (_integrationId == null || _externalTicketId == null)
            throw new InvalidOperationException("External tickets require IntegrationId and ExternalTicketId.");

        return Ticket.MaterializeFromExternal(
            _workspaceId,
            _integrationId.Value,
            _externalTicketId,
            _title,
            _description,
            _statusId,
            _priorityId,
            _assignedAgentId,
            _assignedWorkflowId);
    }

    /// <summary>
    /// Creates an internal ticket with typical configuration.
    /// </summary>
    public static Ticket InternalTicket()
    {
        return new TicketBuilder()
            .AsInternal(true)
            .Build();
    }

    /// <summary>
    /// Creates an external ticket with integration reference.
    /// </summary>
    public static Ticket ExternalTicket()
    {
        return new TicketBuilder()
            .AsExternal(Guid.NewGuid(), "PROJ-123")
            .Build();
    }

    /// <summary>
    /// Creates an assigned ticket.
    /// </summary>
    public static Ticket AssignedTicket()
    {
        return new TicketBuilder()
            .WithAssignedAgent(Guid.NewGuid())
            .Build();
    }

    /// <summary>
    /// Creates an unassigned ticket.
    /// </summary>
    public static Ticket UnassignedTicket()
    {
        return new TicketBuilder()
            .Build();
    }
}
