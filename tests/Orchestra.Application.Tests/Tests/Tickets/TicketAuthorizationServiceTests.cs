using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="TicketAuthorizationService"/>.
/// </summary>
public class TicketAuthorizationServiceTests
{
    private readonly IWorkspaceAuthorizationService _workspaceAuthService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly TicketAuthorizationService _sut;

    public TicketAuthorizationServiceTests()
    {
        _sut = new TicketAuthorizationService(_workspaceAuthService);
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_UserIsMember_DoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ticket = TicketBuilder.InternalTicket();
        _workspaceAuthService.IsMemberAsync(userId, ticket.WorkspaceId, Arg.Any<CancellationToken>()).Returns(true);
        // Act & Assert
        await _sut.EnsureTicketAccessAsync(userId, ticket);
    }

    [Fact]
    public async Task EnsureTicketAccessAsync_UserNotMember_ThrowsUnauthorizedTicketAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ticket = TicketBuilder.InternalTicket();
        _workspaceAuthService.IsMemberAsync(userId, ticket.WorkspaceId, Arg.Any<CancellationToken>()).Returns(false);
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedTicketAccessException>(() => _sut.EnsureTicketAccessAsync(userId, ticket));
        Assert.Contains(userId.ToString(), ex.Message);
        Assert.Contains(ticket.Id.ToString(), ex.Message);
    }

    [Fact]
    public async Task EnsureExternalTicketAccessAsync_UserIsMember_DoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var integration = IntegrationBuilder.JiraCloudIntegration();
        _workspaceAuthService.IsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(true);
        // Act & Assert
        await _sut.EnsureExternalTicketAccessAsync(userId, integration);
    }

    [Fact]
    public async Task EnsureExternalTicketAccessAsync_UserNotMember_ThrowsUnauthorizedTicketAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var integration = IntegrationBuilder.JiraCloudIntegration();
        _workspaceAuthService.IsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>()).Returns(false);
        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedTicketAccessException>(() => _sut.EnsureExternalTicketAccessAsync(userId, integration));
        Assert.Contains(userId.ToString(), ex.Message);
        Assert.Contains($"integration:{integration.Id}", ex.Message);
    }

    [Fact]
    public async Task EnsureWorkspaceAccessAsync_DelegatesToEnsureUserIsMemberAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        // Act
        await _sut.EnsureWorkspaceAccessAsync(userId, workspaceId);
        // Assert
        await _workspaceAuthService.Received(1).EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>());
    }
}
