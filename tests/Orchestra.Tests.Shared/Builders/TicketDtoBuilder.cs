using System;
using System.Collections.Generic;
using Orchestra.Application.Tickets.DTOs;

namespace Orchestra.Tests.Shared.Builders
{
    public class TicketDtoBuilder
    {
        private string _id = Guid.NewGuid().ToString();
        private Guid _workspaceId = Guid.NewGuid();
        private string _title = "Test Title";
        private string _description = "Test Description";
        private TicketStatusDto? _status = null;
        private TicketPriorityDto? _priority = null;
        private bool _internal = true;
        private Guid? _integrationId = null;
        private string? _externalTicketId = null;
        private string? _externalUrl = null;
        private string _source = "INTERNAL";
        private Guid? _assignedAgentId = null;
        private Guid? _assignedWorkflowId = null;
        private List<CommentDto> _comments = new();
        private int? _satisfaction = null;
        private string? _summary = null;

        public TicketDtoBuilder WithId(string id) { _id = id; return this; }
        public TicketDtoBuilder WithWorkspaceId(Guid workspaceId) { _workspaceId = workspaceId; return this; }
        public TicketDtoBuilder WithTitle(string title) { _title = title; return this; }
        public TicketDtoBuilder WithDescription(string description) { _description = description; return this; }
        public TicketDtoBuilder WithStatus(TicketStatusDto? status) { _status = status; return this; }
        public TicketDtoBuilder WithPriority(TicketPriorityDto? priority) { _priority = priority; return this; }
        public TicketDtoBuilder AsInternal(bool isInternal = true) { _internal = isInternal; return this; }
        public TicketDtoBuilder AsExternal(Guid integrationId, string externalTicketId, string source = "JIRA")
        {
            _internal = false;
            _integrationId = integrationId;
            _externalTicketId = externalTicketId;
            _source = source;
            return this;
        }
        public TicketDtoBuilder WithIntegrationId(Guid? integrationId) { _integrationId = integrationId; return this; }
        public TicketDtoBuilder WithExternalTicketId(string? externalTicketId) { _externalTicketId = externalTicketId; return this; }
        public TicketDtoBuilder WithExternalUrl(string? externalUrl) { _externalUrl = externalUrl; return this; }
        public TicketDtoBuilder WithSource(string source) { _source = source; return this; }
        public TicketDtoBuilder WithAssignedAgentId(Guid? agentId) { _assignedAgentId = agentId; return this; }
        public TicketDtoBuilder WithAssignedWorkflowId(Guid? workflowId) { _assignedWorkflowId = workflowId; return this; }
        public TicketDtoBuilder WithComments(List<CommentDto> comments) { _comments = comments; return this; }
        public TicketDtoBuilder AddComment(CommentDto comment) { _comments.Add(comment); return this; }
        public TicketDtoBuilder WithSatisfaction(int? satisfaction) { _satisfaction = satisfaction; return this; }
        public TicketDtoBuilder WithSummary(string? summary) { _summary = summary; return this; }

        public TicketDto Build() => new TicketDto(
            _id,
            _workspaceId,
            _title,
            _description,
            _status,
            _priority,
            _internal,
            _integrationId,
            _externalTicketId,
            _externalUrl,
            _source,
            _assignedAgentId,
            _assignedWorkflowId,
            _comments,
            _satisfaction,
            _summary
        );
    }
}
