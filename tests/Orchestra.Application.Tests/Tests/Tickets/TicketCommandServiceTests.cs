using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Application.Tests.Builders;
using Orchestra.Application.Tests.Fixtures;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="TicketCommandService"/>.
/// </summary>
public class TicketCommandServiceTests : ServiceTestFixture<TicketCommandService>
{
    private readonly ITicketDataAccess _ticketDataAccess = Substitute.For<ITicketDataAccess>();
    private readonly IWorkspaceDataAccess _workspaceDataAccess = Substitute.For<IWorkspaceDataAccess>();
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly ITicketProviderFactory _ticketProviderFactory = Substitute.For<ITicketProviderFactory>();
    private readonly ICredentialEncryptionService _credentialEncryptionService = Substitute.For<ICredentialEncryptionService>();
    private readonly ITicketIdParsingService _ticketIdParsingService = Substitute.For<ITicketIdParsingService>();
    private readonly ITicketAssignmentValidationService _ticketAssignmentValidationService = Substitute.For<ITicketAssignmentValidationService>();
    private readonly IUserDataAccess _userDataAccess = Substitute.For<IUserDataAccess>();
    private readonly ITicketMaterializationService _materializationService = Substitute.For<ITicketMaterializationService>();
    private readonly ITicketQueryService _queryService = Substitute.For<ITicketQueryService>();
    private readonly ILogger<TicketCommandService> _logger;
    private readonly TicketCommandService _sut;

    public TicketCommandServiceTests()
    {
        _logger = GetLoggerSubstitute<TicketCommandService>();
        _sut = new TicketCommandService(
            _ticketDataAccess,
            _workspaceDataAccess,
            _workspaceAuthorizationService,
            _integrationDataAccess,
            _ticketProviderFactory,
            _credentialEncryptionService,
            _ticketIdParsingService,
            _ticketAssignmentValidationService,
            _userDataAccess,
            _materializationService,
            _queryService,
            _logger);
    }

    [Fact]
    public async Task CreateTicketAsync_Throws_WhenWorkspaceNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTicketRequestBuilder().Build();
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _workspaceDataAccess.GetByIdAsync(request.WorkspaceId, Arg.Any<CancellationToken>()).Returns((Workspace?)null);

        // Act & Assert
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => _sut.CreateTicketAsync(userId, request));
    }

    [Fact]
    public async Task CreateTicketAsync_Throws_WhenStatusNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTicketRequestBuilder().Build();
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var workspace = Workspace.Create("Test Workspace", Guid.NewGuid());
        _workspaceDataAccess.GetByIdAsync(request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(workspace);
        _ticketDataAccess.GetStatusByIdAsync(request.StatusId, Arg.Any<CancellationToken>()).Returns((TicketStatus?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateTicketAsync(userId, request));
    }

    [Fact]
    public async Task CreateTicketAsync_Throws_WhenPriorityNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTicketRequestBuilder().Build();
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var workspace = Workspace.Create("Test Workspace", Guid.NewGuid());
        _workspaceDataAccess.GetByIdAsync(request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(workspace);
        _ticketDataAccess.GetStatusByIdAsync(request.StatusId, Arg.Any<CancellationToken>()).Returns(new TicketStatus());
        _ticketDataAccess.GetPriorityByIdAsync(request.PriorityId, Arg.Any<CancellationToken>()).Returns((TicketPriority?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateTicketAsync(userId, request));
    }

    [Fact]
    public async Task CreateTicketAsync_Success_ReturnsTicketDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTicketRequestBuilder().Build();
        var workspace = Workspace.Create("Test Workspace", Guid.NewGuid());
        // Set workspace Id to match request.WorkspaceId if needed
        typeof(Workspace).GetProperty("Id")?.SetValue(workspace, request.WorkspaceId);
        var status = new TicketStatus { Id = request.StatusId, Name = "Open", Color = "#fff" };
        var priority = new TicketPriority { Id = request.PriorityId, Name = "High", Color = "#f00", Value = 1 };
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _workspaceDataAccess.GetByIdAsync(request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(workspace);
        _ticketDataAccess.GetStatusByIdAsync(request.StatusId, Arg.Any<CancellationToken>()).Returns(status);
        _ticketDataAccess.GetPriorityByIdAsync(request.PriorityId, Arg.Any<CancellationToken>()).Returns(priority);
        _ticketDataAccess.AddTicketAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateTicketAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(request.WorkspaceId, result.WorkspaceId);
        Assert.Equal(status.Id, result.Status.Id);
        Assert.Equal(priority.Id, result.Priority.Id);
        // New: Internal tickets should have satisfaction 100
        Assert.True(request.Internal, "Test expects internal ticket");
        Assert.Equal(100, result.Satisfaction);
    }

    [Fact]
    public async Task UpdateTicketAsync_Throws_WhenTicketIdIsNullOrEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateTicketRequest(null, null, null, null, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateTicketAsync(null!, userId, request));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateTicketAsync("", userId, request));
    }

    [Fact]
    public async Task UpdateTicketAsync_Throws_WhenUserIdIsEmpty()
    {
        // Arrange
        var request = new UpdateTicketRequest(null, null, null, null, null);
        var ticketId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateTicketAsync(ticketId, Guid.Empty, request));
    }

    [Fact]
    public async Task UpdateTicketAsync_Throws_WhenNoFieldsProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ticketId = Guid.NewGuid().ToString();
        var request = new UpdateTicketRequest(null, null, null, null, null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateTicketAsync(ticketId, userId, request));
    }

    // --- Begin: ConvertToExternalAsync tests ---
    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenTicketIdNotGuid()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConvertToExternalAsync("not-a-guid", Guid.NewGuid(), Guid.NewGuid(), "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenTicketNotFound()
    {
        var ticketId = Guid.NewGuid().ToString();
        _ticketDataAccess.GetTicketByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Ticket?)null);
        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            _sut.ConvertToExternalAsync(ticketId, Guid.NewGuid(), Guid.NewGuid(), "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenTicketIsAlreadyExternal()
    {
        var ticketId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).AsExternal(integrationId, "EXT-1").Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), integrationId, "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenIntegrationNotFound()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _integrationDataAccess.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Integration?)null);
        await Assert.ThrowsAsync<IntegrationNotFoundException>(() =>
            _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), Guid.NewGuid(), "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenIntegrationTypeNotTracker()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).Build();
        var integration = new IntegrationBuilder().WithType(IntegrationType.KNOWLEDGE_BASE).Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _integrationDataAccess.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(integration);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), integration.Id, "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenIntegrationInactive()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).Build();
        var integration = new IntegrationBuilder().WithType(IntegrationType.TRACKER).AsConnected(false).Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _integrationDataAccess.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(integration);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), integration.Id, "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenIntegrationWorkspaceMismatch()
    {
        var ticketId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).Build();
        var integration = new IntegrationBuilder().WithType(IntegrationType.TRACKER).AsConnected(true).WithWorkspaceId(Guid.NewGuid()).Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _integrationDataAccess.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(integration);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), integration.Id, "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Throws_WhenProviderNotSupported()
    {
        var ticketId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).WithWorkspaceId(workspaceId).Build();
        var integration = new IntegrationBuilder().WithType(IntegrationType.TRACKER).AsConnected(true).WithWorkspaceId(workspaceId).WithProvider(ProviderType.JIRA).Build();
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _integrationDataAccess.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(integration);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns((ITicketProvider?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), integration.Id, "Bug", CancellationToken.None));
    }

    [Fact]
    public async Task ConvertToExternalAsync_Success_CreatesExternalTicketAndReturnsDto()
    {
        var ticketId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(ticketId).WithWorkspaceId(workspaceId).Build();
        var integration = new IntegrationBuilder().WithType(IntegrationType.TRACKER).AsConnected(true).WithWorkspaceId(workspaceId).WithProvider(ProviderType.JIRA).Build();
        var provider = Substitute.For<ITicketProvider>();
        var externalResult = new Orchestra.Application.Tickets.DTOs.ExternalTicketCreationResult("EXT-1", "http://external.url", "id-1");
        var expectedDto = new TicketDto(ticketId.ToString(), workspaceId, "title", "desc", null, null, false, integrationId, "EXT-1", "http://external.url", "EXTERNAL", null, null, new System.Collections.Generic.List<CommentDto>(), null, null);
        _ticketDataAccess.GetTicketByIdAsync(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _ticketProviderFactory.CreateProvider(ProviderType.JIRA).Returns(provider);
        provider.CreateIssueAsync(integration, ticket.Title, ticket.Description ?? string.Empty, "Bug", Arg.Any<CancellationToken>()).Returns(externalResult);
        _ticketDataAccess.UpdateTicketAsync(ticket, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _queryService.GetTicketByIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(expectedDto);
        var result = await _sut.ConvertToExternalAsync(ticketId.ToString(), Guid.NewGuid(), integrationId, "Bug", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(expectedDto.ExternalTicketId, result.ExternalTicketId);
        Assert.Equal(expectedDto.IntegrationId, result.IntegrationId);
    }

    // --- Begin: DeleteTicketAsync tests ---
    [Fact]
    public async Task DeleteTicketAsync_Throws_WhenNotInternal()
    {
        _ticketIdParsingService.Parse(Arg.Any<string>()).Returns(new TicketIdParseResult(TicketIdType.External, null, null, null));
        await Assert.ThrowsAsync<InvalidTicketOperationException>(() =>
            _sut.DeleteTicketAsync("external:1", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteTicketAsync_Throws_WhenTicketNotFound()
    {
        var guid = Guid.NewGuid();
        _ticketIdParsingService.Parse(guid.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, guid, null, null));
        _ticketDataAccess.GetTicketByIdAsync(guid, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            _sut.DeleteTicketAsync(guid.ToString(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteTicketAsync_Throws_WhenNotAuthorized()
    {
        var guid = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(guid).Build();
        _ticketIdParsingService.Parse(guid.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, guid, null, null));
        _ticketDataAccess.GetTicketByIdAsync(guid, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.IsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        await Assert.ThrowsAsync<UnauthorizedTicketAccessException>(() =>
            _sut.DeleteTicketAsync(guid.ToString(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteTicketAsync_Throws_WhenCannotDelete()
    {
        var guid = Guid.NewGuid();
        // Create a ticket that is external (cannot be deleted)
        var integrationId = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(guid).AsExternal(integrationId, "EXT-1").Build();
        _ticketIdParsingService.Parse(guid.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, guid, null, null));
        _ticketDataAccess.GetTicketByIdAsync(guid, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.IsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        await Assert.ThrowsAsync<InvalidTicketOperationException>(() =>
            _sut.DeleteTicketAsync(guid.ToString(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteTicketAsync_Success_DeletesTicket()
    {
        var guid = Guid.NewGuid();
        var ticket = new TicketBuilder().WithId(guid).Build();
        _ticketIdParsingService.Parse(guid.ToString()).Returns(new TicketIdParseResult(TicketIdType.Internal, guid, null, null));
        _ticketDataAccess.GetTicketByIdAsync(guid, Arg.Any<CancellationToken>()).Returns(ticket);
        _workspaceAuthorizationService.IsMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        // Internal tickets created by builder are deletable
        await _sut.DeleteTicketAsync(guid.ToString(), Guid.NewGuid(), CancellationToken.None);
        await _ticketDataAccess.Received(1).DeleteTicketAsync(guid, Arg.Any<CancellationToken>());
    }

    // --- Begin: CreateTicketAsync assignment branch test ---
    [Fact]
    public async Task CreateTicketAsync_WithAssignments_ValidatesAndUpdatesAssignments()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var request = new CreateTicketRequestBuilder()
            .WithWorkspaceId(workspaceId)
            .WithAssignedAgent(agentId)
            .WithAssignedWorkflow(workflowId)
            .Build();
        var workspace = Workspace.Create("Test Workspace", Guid.NewGuid());
        // Ensure the workspace returned matches the request.WorkspaceId
        typeof(Workspace).GetProperty("Id")!.SetValue(workspace, workspaceId);
        var status = new TicketStatus { Id = request.StatusId, Name = "Open", Color = "#fff" };
        var priority = new TicketPriority { Id = request.PriorityId, Name = "High", Color = "#f00", Value = 1 };
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _workspaceDataAccess.GetByIdAsync(request.WorkspaceId, Arg.Any<CancellationToken>()).Returns(workspace);
        _ticketDataAccess.GetStatusByIdAsync(request.StatusId, Arg.Any<CancellationToken>()).Returns(status);
        _ticketDataAccess.GetPriorityByIdAsync(request.PriorityId, Arg.Any<CancellationToken>()).Returns(priority);
        // Agent and workflow must belong to the same workspace as the ticket
        _ticketAssignmentValidationService.ValidateAndGetAgentWorkspaceAsync(request.AssignedAgentId, Arg.Any<CancellationToken>()).Returns(workspaceId);
        _ticketAssignmentValidationService.ValidateAndGetWorkflowWorkspaceAsync(request.AssignedWorkflowId, Arg.Any<CancellationToken>()).Returns(workspaceId);
        _ticketDataAccess.AddTicketAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var result = await _sut.CreateTicketAsync(userId, request);
        Assert.NotNull(result);
        Assert.Equal(request.AssignedAgentId, result.AssignedAgentId);
        Assert.Equal(request.AssignedWorkflowId, result.AssignedWorkflowId);
    }
}
