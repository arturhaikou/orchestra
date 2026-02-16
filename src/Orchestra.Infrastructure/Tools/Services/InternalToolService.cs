using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Tickets.DTOs;

namespace Orchestra.Infrastructure.Tools.Services;

public class InternalToolService : IInternalToolService
{
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid MediumPriorityId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly ITicketService _ticketService;
    private readonly IWorkspaceDataAccess _workspaceDataAccess;
    private readonly ILogger<InternalToolService> _logger;

    public InternalToolService(
        ITicketService ticketService,
        IWorkspaceDataAccess workspaceDataAccess,
        ILogger<InternalToolService> logger)
    {
        _ticketService = ticketService;
        _workspaceDataAccess = workspaceDataAccess;
        _logger = logger;
    }

    public async Task<object> CreateTicketAsync(
        string workspaceId,
        string title,
        string description)
    {
        try
        {
            _logger.LogInformation(
                "Creating internal ticket in workspace {WorkspaceId}: Title='{Title}'",
                workspaceId,
                title);

            // Validate and parse workspaceId
            if (!Guid.TryParse(workspaceId, out var workspaceGuid) || workspaceGuid == Guid.Empty)
            {
                _logger.LogWarning("CreateTicketAsync called with invalid workspaceId: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Get workspace owner
            var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceGuid);
            if (workspace == null)
            {
                _logger.LogWarning("CreateTicketAsync called for non-existent workspace: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Workspace not found: {workspaceId}",
                    errorCode = "WORKSPACE_NOT_FOUND"
                };
            }
            var userGuid = workspace.OwnerId;

            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.LogWarning("CreateTicketAsync called with empty title");
                return new
                {
                    success = false,
                    error = "Title is required",
                    errorCode = "INVALID_TITLE"
                };
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                _logger.LogWarning("CreateTicketAsync called with empty description");
                return new
                {
                    success = false,
                    error = "Description is required",
                    errorCode = "INVALID_DESCRIPTION"
                };
            }

            // Create ticket request
            var request = new CreateTicketRequest(
                WorkspaceId: workspaceGuid,
                Title: title,
                Description: description,
                StatusId: ToDoStatusId,
                PriorityId: MediumPriorityId,
                Internal: true);

            // Call ticket service to create ticket
            var ticketDto = await _ticketService.CreateTicketAsync(userGuid, request);

            _logger.LogInformation(
                "Successfully created internal ticket {TicketId} for workspace owner {UserId} in workspace {WorkspaceId}",
                ticketDto.Id,
                userGuid,
                workspaceId);

            return new
            {
                success = true,
                data = new
                {
                    ticketId = ticketDto.Id,
                    title = ticketDto.Title,
                    description = ticketDto.Description,
                    status = ticketDto.Status,
                    priority = ticketDto.Priority,
                    workspaceId = ticketDto.WorkspaceId
                },
                message = $"Successfully created internal ticket {ticketDto.Id}"
            };
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogError(ex,
                "Workspace owner is not authorized to create tickets in workspace {WorkspaceId}",
                workspaceId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "UNAUTHORIZED_ACCESS"
            };
        }
        catch (WorkspaceNotFoundException ex)
        {
            _logger.LogError(ex,
                "Workspace {WorkspaceId} not found",
                workspaceId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "WORKSPACE_NOT_FOUND"
            };
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex,
                "Validation error creating internal ticket in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "VALIDATION_ERROR"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument creating internal ticket in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_ARGUMENT"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error creating internal ticket in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> GetTicketAsync(
        string workspaceId,
        string ticketId)
    {
        try
        {
            _logger.LogInformation(
                "Retrieving internal ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            // Validate and parse workspaceId
            if (!Guid.TryParse(workspaceId, out var workspaceGuid) || workspaceGuid == Guid.Empty)
            {
                _logger.LogWarning("GetTicketAsync called with invalid workspaceId: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Get workspace owner
            var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceGuid);
            if (workspace == null)
            {
                _logger.LogWarning("GetTicketAsync called for non-existent workspace: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Workspace not found: {workspaceId}",
                    errorCode = "WORKSPACE_NOT_FOUND"
                };
            }
            var userGuid = workspace.OwnerId;

            if (string.IsNullOrWhiteSpace(ticketId))
            {
                _logger.LogWarning("GetTicketAsync called with empty ticketId");
                return new
                {
                    success = false,
                    error = "Ticket ID is required",
                    errorCode = "INVALID_TICKET_ID"
                };
            }

            // Call ticket service to get ticket
            var ticketDto = await _ticketService.GetTicketByIdAsync(ticketId, userGuid);

            _logger.LogInformation(
                "Successfully retrieved internal ticket {TicketId} for workspace owner {UserId} in workspace {WorkspaceId}",
                ticketId,
                userGuid,
                workspaceId);

            return new
            {
                success = true,
                data = new
                {
                    ticketId = ticketDto.Id,
                    title = ticketDto.Title,
                    description = ticketDto.Description,
                    status = ticketDto.Status,
                    priority = ticketDto.Priority,
                    assignedAgentId = ticketDto.AssignedAgentId,
                    assignedWorkflowId = ticketDto.AssignedWorkflowId,
                    workspaceId = ticketDto.WorkspaceId,
                    isInternal = ticketDto.Internal,
                    integrationId = ticketDto.IntegrationId,
                    externalTicketId = ticketDto.ExternalTicketId,
                    externalUrl = ticketDto.ExternalUrl,
                    source = ticketDto.Source,
                    satisfaction = ticketDto.Satisfaction,
                    summary = ticketDto.Summary
                },
                message = $"Successfully retrieved ticket {ticketId}"
            };
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogError(ex,
                "Ticket {TicketId} not found",
                ticketId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "TICKET_NOT_FOUND"
            };
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogError(ex,
                "Workspace owner is not authorized to access ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "UNAUTHORIZED_ACCESS"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument retrieving ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_ARGUMENT"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error retrieving ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> UpdateTicketAsync(
        string workspaceId,
        string ticketId,
        string? assignedAgentId = null,
        string? assignedWorkflowId = null)
    {
        try
        {
            _logger.LogInformation(
                "Updating internal ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            // Validate and parse workspaceId
            if (!Guid.TryParse(workspaceId, out var workspaceGuid) || workspaceGuid == Guid.Empty)
            {
                _logger.LogWarning("UpdateTicketAsync called with invalid workspaceId: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Get workspace owner
            var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceGuid);
            if (workspace == null)
            {
                _logger.LogWarning("UpdateTicketAsync called for non-existent workspace: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Workspace not found: {workspaceId}",
                    errorCode = "WORKSPACE_NOT_FOUND"
                };
            }
            var userGuid = workspace.OwnerId;

            if (string.IsNullOrWhiteSpace(ticketId))
            {
                _logger.LogWarning("UpdateTicketAsync called with empty ticketId");
                return new
                {
                    success = false,
                    error = "Ticket ID is required",
                    errorCode = "INVALID_TICKET_ID"
                };
            }

            // Validate at least one field is provided
            if (string.IsNullOrWhiteSpace(assignedAgentId) && 
                string.IsNullOrWhiteSpace(assignedWorkflowId))
            {
                _logger.LogWarning(
                    "UpdateTicketAsync called with no fields to update for ticket {TicketId}",
                    ticketId);
                return new
                {
                    success = false,
                    error = "At least one field must be provided for update: statusId, priorityId, assignedAgentId, or assignedWorkflowId",
                    errorCode = "NO_FIELDS_TO_UPDATE"
                };
            }

            Guid? agentGuid = null;
            if (!string.IsNullOrWhiteSpace(assignedAgentId))
            {
                if (!Guid.TryParse(assignedAgentId, out var parsed))
                {
                    return new
                    {
                        success = false,
                        error = $"Invalid GUID format for assignedAgentId: {assignedAgentId}",
                        errorCode = "INVALID_AGENT_ID"
                    };
                }
                agentGuid = parsed;
            }

            Guid? workflowGuid = null;
            if (!string.IsNullOrWhiteSpace(assignedWorkflowId))
            {
                if (!Guid.TryParse(assignedWorkflowId, out var parsed))
                {
                    return new
                    {
                        success = false,
                        error = $"Invalid GUID format for assignedWorkflowId: {assignedWorkflowId}",
                        errorCode = "INVALID_WORKFLOW_ID"
                    };
                }
                workflowGuid = parsed;
            }

            // Create update request
            var request = new UpdateTicketRequest(
                StatusId: null,
                PriorityId: null,
                AssignedAgentId: agentGuid,
                AssignedWorkflowId: workflowGuid,
                Description: null);

            // Call ticket service to update ticket
            var ticketDto = await _ticketService.UpdateTicketAsync(ticketId, userGuid, request);

            _logger.LogInformation(
                "Successfully updated internal ticket {TicketId} for workspace owner {UserId} in workspace {WorkspaceId}",
                ticketId,
                userGuid,
                workspaceId);

            return new
            {
                success = true,
                data = new
                {
                    ticketId = ticketDto.Id,
                    title = ticketDto.Title,
                    status = ticketDto.Status,
                    priority = ticketDto.Priority,
                    assignedAgentId = ticketDto.AssignedAgentId,
                    assignedWorkflowId = ticketDto.AssignedWorkflowId
                },
                message = $"Successfully updated internal ticket {ticketId}"
            };
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogError(ex,
                "Ticket {TicketId} not found",
                ticketId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "TICKET_NOT_FOUND"
            };
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogError(ex,
                "Workspace owner is not authorized to update ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "UNAUTHORIZED_ACCESS"
            };
        }
        catch (InvalidTicketOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation updating ticket {TicketId}: {ErrorMessage}",
                ticketId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_OPERATION"
            };
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex,
                "Validation error updating ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "VALIDATION_ERROR"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument updating ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_ARGUMENT"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error updating ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> DeleteTicketAsync(
        string workspaceId,
        string ticketId)
    {
        try
        {
            _logger.LogInformation(
                "Deleting internal ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            // Validate and parse workspaceId
            if (!Guid.TryParse(workspaceId, out var workspaceGuid) || workspaceGuid == Guid.Empty)
            {
                _logger.LogWarning("DeleteTicketAsync called with invalid workspaceId: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Get workspace owner
            var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceGuid);
            if (workspace == null)
            {
                _logger.LogWarning("DeleteTicketAsync called for non-existent workspace: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Workspace not found: {workspaceId}",
                    errorCode = "WORKSPACE_NOT_FOUND"
                };
            }
            var userGuid = workspace.OwnerId;

            if (string.IsNullOrWhiteSpace(ticketId))
            {
                _logger.LogWarning("DeleteTicketAsync called with empty ticketId");
                return new
                {
                    success = false,
                    error = "Ticket ID is required",
                    errorCode = "INVALID_TICKET_ID"
                };
            }

            // Call ticket service to delete ticket
            await _ticketService.DeleteTicketAsync(ticketId, userGuid);

            _logger.LogInformation(
                "Successfully deleted internal ticket {TicketId} for workspace owner {UserId} in workspace {WorkspaceId}",
                ticketId,
                userGuid,
                workspaceId);

            return new
            {
                success = true,
                ticketId = ticketId,
                message = $"Successfully deleted internal ticket {ticketId}"
            };
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogError(ex,
                "Ticket {TicketId} not found",
                ticketId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "TICKET_NOT_FOUND"
            };
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogError(ex,
                "Workspace owner is not authorized to delete ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "UNAUTHORIZED_ACCESS"
            };
        }
        catch (InvalidTicketOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation deleting ticket {TicketId}: {ErrorMessage}",
                ticketId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument deleting ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_ARGUMENT"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error deleting ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> AssignAgentAsync(
        string workspaceId,
        string ticketId,
        string agentId)
    {
        try
        {
            _logger.LogInformation(
                "Assigning agent {AgentId} to ticket {TicketId} in workspace {WorkspaceId}",
                agentId,
                ticketId,
                workspaceId);

            // Validate and parse workspaceId
            if (!Guid.TryParse(workspaceId, out var workspaceGuid) || workspaceGuid == Guid.Empty)
            {
                _logger.LogWarning("AssignAgentAsync called with invalid workspaceId: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Get workspace owner
            var workspace = await _workspaceDataAccess.GetByIdAsync(workspaceGuid);
            if (workspace == null)
            {
                _logger.LogWarning("AssignAgentAsync called for non-existent workspace: {WorkspaceId}", workspaceId);
                return new
                {
                    success = false,
                    error = $"Workspace not found: {workspaceId}",
                    errorCode = "WORKSPACE_NOT_FOUND"
                };
            }
            var userGuid = workspace.OwnerId;

            if (string.IsNullOrWhiteSpace(ticketId))
            {
                _logger.LogWarning("AssignAgentAsync called with empty ticketId");
                return new
                {
                    success = false,
                    error = "Ticket ID is required",
                    errorCode = "INVALID_TICKET_ID"
                };
            }

            // Validate and parse agentId
            if (!Guid.TryParse(agentId, out var agentGuid) || agentGuid == Guid.Empty)
            {
                _logger.LogWarning("AssignAgentAsync called with invalid agentId: {AgentId}", agentId);
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for agentId: {agentId}",
                    errorCode = "INVALID_AGENT_ID"
                };
            }

            // Create update request with only agent assignment
            var request = new UpdateTicketRequest(
                StatusId: null,
                PriorityId: null,
                AssignedAgentId: agentGuid,
                AssignedWorkflowId: null,
                Description: null);

            // Call ticket service to update ticket
            var ticketDto = await _ticketService.UpdateTicketAsync(ticketId, userGuid, request);

            _logger.LogInformation(
                "Successfully assigned agent {AgentId} to ticket {TicketId} for workspace owner {UserId} in workspace {WorkspaceId}",
                agentId,
                ticketId,
                userGuid,
                workspaceId);

            return new
            {
                success = true,
                data = new
                {
                    ticketId = ticketDto.Id,
                    title = ticketDto.Title,
                    assignedAgentId = ticketDto.AssignedAgentId
                },
                message = $"Successfully assigned agent {agentId} to ticket {ticketId}"
            };
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogError(ex,
                "Ticket {TicketId} not found",
                ticketId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "TICKET_NOT_FOUND"
            };
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogError(ex,
                "Workspace owner is not authorized to assign agent to ticket {TicketId} in workspace {WorkspaceId}",
                ticketId,
                workspaceId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "UNAUTHORIZED_ACCESS"
            };
        }
        catch (AgentNotFoundException ex)
        {
            _logger.LogError(ex,
                "Agent {AgentId} not found",
                agentId);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "AGENT_NOT_FOUND"
            };
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex,
                "Validation error assigning agent {AgentId} to ticket {TicketId}: {ErrorMessage}",
                agentId,
                ticketId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "VALIDATION_ERROR"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument assigning agent {AgentId} to ticket {TicketId}: {ErrorMessage}",
                agentId,
                ticketId,
                ex.Message);

            return new
            {
                success = false,
                error = ex.Message,
                errorCode = "INVALID_ARGUMENT"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error assigning agent {AgentId} to ticket {TicketId} in workspace {WorkspaceId}: {ErrorMessage}",
                agentId,
                ticketId,
                workspaceId,
                ex.Message);

            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }
}