using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Tickets.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;
using Orchestra.Application.Tests.Fixtures;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets
{
    public class TicketMaterializationServiceTests : ServiceTestFixture<TicketMaterializationService>
    {
        private readonly ITicketDataAccess _ticketDataAccess;
        private readonly TicketMaterializationService _service;

        public TicketMaterializationServiceTests()
        {
            _ticketDataAccess = Substitute.For<ITicketDataAccess>();
            _service = new TicketMaterializationService(_ticketDataAccess, Logger);
        }

        [Fact]
        public async Task MapExternalPriorityToInternalAsync_MapsToNearestPriority()
        {
            // Arrange
            var priorities = new List<TicketPriority>
            {
                new TicketPriority { Id = Guid.NewGuid(), Name = "Low", Value = 1, Color = "#00FF00" },
                new TicketPriority { Id = Guid.NewGuid(), Name = "Medium", Value = 5, Color = "#FFFF00" },
                new TicketPriority { Id = Guid.NewGuid(), Name = "High", Value = 10, Color = "#FF0000" }
            };
            _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(priorities);

            // Act
            var result = await _service.MapExternalPriorityToInternalAsync(6, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Medium", result.Name);
        }

        [Fact]
        public async Task MapExternalPriorityToInternalAsync_ThrowsIfNoPriorities()
        {
            // Arrange
            _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns((List<TicketPriority>)null!);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.MapExternalPriorityToInternalAsync(1, CancellationToken.None));
        }

        [Fact]
        public async Task MaterializeFromExternalAsync_CreatesTicketWithMappedPriority()
        {
            // Arrange
            var priority = new TicketPriority { Id = Guid.NewGuid(), Name = "High", Value = 10, Color = "#FF0000" };
            _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns(new List<TicketPriority> { priority });

            var integrationId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var externalTicketId = "EXT-123";
            var externalTicket = new ExternalTicketDto(
                integrationId,
                externalTicketId,
                "Title",
                "Description",
                "To Do",
                "#000000",
                "High",
                "#FF0000",
                10,
                "http://external.url",
                new List<CommentDto>()
            );
            Guid? assignedAgentId = Guid.NewGuid();
            Guid? assignedWorkflowId = Guid.NewGuid();

            // Act
            var ticket = await _service.MaterializeFromExternalAsync(
                integrationId,
                externalTicketId,
                workspaceId,
                externalTicket,
                assignedAgentId,
                assignedWorkflowId,
                CancellationToken.None);

            // Assert
            Assert.NotNull(ticket);
            Assert.Equal(workspaceId, ticket.WorkspaceId);
            Assert.Equal(integrationId, ticket.IntegrationId);
            Assert.Equal(externalTicketId, ticket.ExternalTicketId);
            Assert.Equal("Title", ticket.Title);
            Assert.Equal("Description", ticket.Description);
            Assert.Equal(priority.Id, ticket.PriorityId);
            Assert.Equal(assignedAgentId, ticket.AssignedAgentId);
            Assert.Equal(assignedWorkflowId, ticket.AssignedWorkflowId);
            Assert.False(ticket.IsInternal); // Materialized tickets are external
        }

        [Fact]
        public async Task MaterializeFromExternalAsync_ThrowsIfNoPriorities()
        {
            // Arrange
            _ticketDataAccess.GetAllPrioritiesAsync(Arg.Any<CancellationToken>()).Returns((List<TicketPriority>)null!);

            var integrationId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var externalTicketId = "EXT-123";
            var externalTicket = new ExternalTicketDto(
                integrationId,
                externalTicketId,
                "Title",
                "Description",
                "To Do",
                "#000000",
                "Low",
                "#00FF00",
                1,
                "http://external.url",
                new List<CommentDto>()
            );

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.MaterializeFromExternalAsync(
                    integrationId,
                    externalTicketId,
                    workspaceId,
                    externalTicket,
                    null,
                    null,
                    CancellationToken.None));
        }
    }
}
