using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="TicketCommentService"/>.
/// </summary>
public class TicketCommentServiceTests
{
    private readonly ITicketDataAccess _ticketDataAccess = Substitute.For<ITicketDataAccess>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IUserDataAccess _userDataAccess = Substitute.For<IUserDataAccess>();
    private readonly ITicketProviderFactory _ticketProviderFactory = Substitute.For<ITicketProviderFactory>();
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly ITicketIdParsingService _ticketIdParsingService = Substitute.For<ITicketIdParsingService>();
    private readonly TicketCommentService _sut;

    public TicketCommentServiceTests()
    {
        _sut = new TicketCommentService(
            _ticketDataAccess,
            _integrationDataAccess,
            _userDataAccess,
            _ticketProviderFactory,
            _workspaceAuthorizationService,
            _ticketIdParsingService);
    }

    [Fact]
    public async Task AddCommentAsync_EmptyContent_ThrowsArgumentException()
    {
        var request = new AddCommentRequest("");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddCommentAsync("some-id", Guid.NewGuid(), request));
        Assert.Contains("Comment content cannot be empty", ex.Message);
    }

    [Fact]
    public async Task AddCommentAsync_InternalTicket_RoutesToInternal()
    {
        var ticketId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test comment");
        _ticketIdParsingService.Parse(ticketId).Returns(new TicketIdParseResult(TicketIdType.Internal, Guid.Parse(ticketId), null, null));
        // Since AddCommentToInternalTicketAsync is not virtual, test by checking the result of AddCommentAsync
        var ticket = new TicketBuilder().WithId(Guid.Parse(ticketId)).Build();
        var user = new UserBuilder().WithId(userId).WithName("Test User").Build();
        _ticketDataAccess.GetTicketByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ticket);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, ticket.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ticketDataAccess.AddCommentAsync(Arg.Any<TicketComment>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var result = await _sut.AddCommentAsync(ticketId, userId, request);
        Assert.Equal("Test User", result.Author);
        Assert.Equal("Test comment", result.Content);
        Assert.False(string.IsNullOrWhiteSpace(result.Id));
    }

    [Fact]
    public async Task AddCommentAsync_ExternalTicket_RoutesToExternal()
    {
        var integrationId = Guid.NewGuid();
        var externalTicketId = "EXT-123";
        var ticketId = $"{integrationId}:{externalTicketId}";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test comment");
        _ticketIdParsingService.Parse(ticketId).Returns(new TicketIdParseResult(TicketIdType.External, null, integrationId, externalTicketId));
        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        var user = new UserBuilder().WithId(userId).WithName("Test User").Build();
        var provider = Substitute.For<ITicketProvider>();
        var expectedDto = new CommentDto("cid", "Test User", "Test comment");
        provider.AddCommentAsync(integration, externalTicketId, request.Content, user.Name, Arg.Any<CancellationToken>()).Returns(expectedDto);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ticketProviderFactory.CreateProvider(integration.Provider).Returns(provider);
        var result = await _sut.AddCommentAsync(ticketId, userId, request);
        Assert.Equal(expectedDto.Id, result.Id);
        Assert.Equal(expectedDto.Author, result.Author);
        Assert.Equal(expectedDto.Content, result.Content);
    }

    [Fact]
    public async Task AddCommentAsync_InvalidTicketId_ThrowsArgumentException()
    {
        var ticketId = "bad-id";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test comment");
        _ticketIdParsingService.Parse(ticketId).Returns(new TicketIdParseResult((TicketIdType)99, null, null, null));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddCommentAsync(ticketId, userId, request));
        Assert.Contains("Invalid ticket ID format", ex.Message);
    }

    [Fact]
    public async Task AddCommentToInternalTicketAsync_InvalidGuid_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddCommentToInternalTicketAsync("not-a-guid", userId, request, CancellationToken.None));
        Assert.Contains("Invalid ticket ID format", ex.Message);
    }

    [Fact]
    public async Task AddCommentToInternalTicketAsync_TicketNotFound_ThrowsTicketNotFoundException()
    {
        var ticketId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        _ticketDataAccess.GetTicketByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Ticket?)null);
        await Assert.ThrowsAsync<TicketNotFoundException>(() => _sut.AddCommentToInternalTicketAsync(ticketId, userId, request, CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentToInternalTicketAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        var ticketId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        _ticketDataAccess.GetTicketByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new TicketBuilder().WithId(Guid.Parse(ticketId)).Build());
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((Orchestra.Domain.Entities.User?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AddCommentToInternalTicketAsync(ticketId, userId, request, CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentToInternalTicketAsync_Success_ReturnsCommentDto()
    {
        var ticketId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var ticket = new TicketBuilder().WithId(Guid.Parse(ticketId)).Build();
        var user = new UserBuilder().WithId(userId).WithName("Test User").Build();
        _ticketDataAccess.GetTicketByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ticket);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, ticket.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ticketDataAccess.AddCommentAsync(Arg.Any<TicketComment>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var result = await _sut.AddCommentToInternalTicketAsync(ticketId, userId, request, CancellationToken.None);
        Assert.Equal("Test User", result.Author);
        Assert.Equal("Test", result.Content);
        Assert.False(string.IsNullOrWhiteSpace(result.Id));
    }

    [Fact]
    public async Task AddCommentToExternalTicketAsync_InvalidCompositeId_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddCommentToExternalTicketAsync("bad-format", userId, request, CancellationToken.None));
        Assert.Contains("Invalid composite ID format", ex.Message);
    }

    [Fact]
    public async Task AddCommentToExternalTicketAsync_IntegrationNotFound_ThrowsTicketNotFoundException()
    {
        var integrationId = Guid.NewGuid();
        var ticketId = $"{integrationId}:EXT-1";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns((Orchestra.Domain.Entities.Integration?)null);
        await Assert.ThrowsAsync<TicketNotFoundException>(() => _sut.AddCommentToExternalTicketAsync(ticketId, userId, request, CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentToExternalTicketAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        var integrationId = Guid.NewGuid();
        var ticketId = $"{integrationId}:EXT-1";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((Orchestra.Domain.Entities.User?)null);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AddCommentToExternalTicketAsync(ticketId, userId, request, CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentToExternalTicketAsync_ProviderNotFound_ThrowsInvalidOperationException()
    {
        var integrationId = Guid.NewGuid();
        var ticketId = $"{integrationId}:EXT-1";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new UserBuilder().WithId(userId).Build());
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ticketProviderFactory.CreateProvider(integration.Provider).Returns((ITicketProvider?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AddCommentToExternalTicketAsync(ticketId, userId, request, CancellationToken.None));
    }

    [Fact]
    public async Task AddCommentToExternalTicketAsync_ProviderThrowsHttpRequestException_ThrowsInvalidOperationException()
    {
        var integrationId = Guid.NewGuid();
        var ticketId = $"{integrationId}:EXT-1";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        var user = new UserBuilder().WithId(userId).WithName("Test User").Build();
        var provider = Substitute.For<ITicketProvider>();
        provider.AddCommentAsync(integration, "EXT-1", request.Content, user.Name, Arg.Any<CancellationToken>())
            .Returns<Task<CommentDto>>(x => throw new System.Net.Http.HttpRequestException("network error"));
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ticketProviderFactory.CreateProvider(integration.Provider).Returns(provider);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AddCommentToExternalTicketAsync(ticketId, userId, request, CancellationToken.None));
        Assert.Contains("Failed to add comment to external ticket", ex.Message);
    }

    [Fact]
    public async Task AddCommentToExternalTicketAsync_Success_ReturnsCommentDto()
    {
        var integrationId = Guid.NewGuid();
        var ticketId = $"{integrationId}:EXT-1";
        var userId = Guid.NewGuid();
        var request = new AddCommentRequest("Test");
        var integration = new IntegrationBuilder().WithId(integrationId).Build();
        var user = new UserBuilder().WithId(userId).WithName("Test User").Build();
        var provider = Substitute.For<ITicketProvider>();
        var expectedDto = new CommentDto("cid", "Test User", "Test");
        provider.AddCommentAsync(integration, "EXT-1", request.Content, user.Name, Arg.Any<CancellationToken>())
            .Returns(expectedDto);
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ticketProviderFactory.CreateProvider(integration.Provider).Returns(provider);
        var result = await _sut.AddCommentToExternalTicketAsync(ticketId, userId, request, CancellationToken.None);
        Assert.Equal(expectedDto.Id, result.Id);
        Assert.Equal(expectedDto.Author, result.Author);
        Assert.Equal(expectedDto.Content, result.Content);
    }
}
