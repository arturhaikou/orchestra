using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Services;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="TicketAssignmentValidationService"/>.
/// </summary>
public class TicketAssignmentValidationServiceTests
{
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IWorkflowDefinitionRepository _workflowRepository = Substitute.For<IWorkflowDefinitionRepository>();
    private readonly TicketAssignmentValidationService _sut;

    public TicketAssignmentValidationServiceTests()
    {
        _sut = new TicketAssignmentValidationService(_agentDataAccess, _workflowRepository);
    }

    [Fact]
    public async Task ValidateAndGetAgentWorkspaceAsync_NullAgentId_ReturnsNull()
    {
        // Act
        var result = await _sut.ValidateAndGetAgentWorkspaceAsync(null);
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndGetAgentWorkspaceAsync_AgentExists_ReturnsWorkspaceId()
    {
        // Arrange
        var agent = new AgentBuilder().Build();
        _agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);
        // Act
        var result = await _sut.ValidateAndGetAgentWorkspaceAsync(agent.Id);
        // Assert
        Assert.Equal(agent.WorkspaceId, result);
    }

    [Fact]
    public async Task ValidateAndGetAgentWorkspaceAsync_AgentNotFound_ThrowsArgumentException()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns((Agent?)null);
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.ValidateAndGetAgentWorkspaceAsync(agentId));
        Assert.Contains(agentId.ToString(), ex.Message);
    }

    [Fact]
    public async Task ValidateAndGetWorkflowWorkspaceAsync_NullWorkflowId_ReturnsNull()
    {
        // Act
        var result = await _sut.ValidateAndGetWorkflowWorkspaceAsync(null);
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndGetWorkflowWorkspaceAsync_WorkflowIdProvided_ReturnsNull()
    {
        // Act
        var result = await _sut.ValidateAndGetWorkflowWorkspaceAsync(Guid.NewGuid());
        // Assert
        Assert.Null(result); // Not implemented yet
    }
}
