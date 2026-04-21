using System;
using Orchestra.Domain.Entities;
using Xunit;

namespace Orchestra.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="Ticket"/> domain entity — MaxLength domain validation guards.
/// Covers FR-010 BDD Scenarios 1–5.
/// </summary>
public class TicketEntityTests
{
    private static readonly Guid ValidWorkspaceId = Guid.NewGuid();
    private static readonly Guid ValidPriorityId = Guid.NewGuid();
    private static readonly Guid ValidStatusId = Guid.NewGuid();

    // ──────────────────────────────────────────────────────────────────────────
    // Ticket.Create — Title length guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ThrowsArgumentException_WhenTitleExceeds500Characters()
    {
        // Arrange — Scenario 1: title of 501 characters
        var oversizedTitle = new string('A', 501);

        // Act
        var act = () => Ticket.Create(
            ValidWorkspaceId,
            oversizedTitle,
            description: null,
            ValidPriorityId,
            ValidStatusId,
            isInternal: true);

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public void Create_ThrowsArgumentException_WhenDescriptionExceeds10000Characters()
    {
        // Arrange — Scenario 2: description of 10001 characters
        var oversizedDescription = new string('D', 10001);

        // Act
        var act = () => Ticket.Create(
            ValidWorkspaceId,
            title: "Valid Title",
            description: oversizedDescription,
            ValidPriorityId,
            ValidStatusId,
            isInternal: true);

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("10000", ex.Message);
    }

    [Fact]
    public void Create_Succeeds_WhenTitleIs500CharactersAndDescriptionIs10000Characters()
    {
        // Arrange — Scenario 4: exactly at the boundary — must not throw
        var maxTitle = new string('A', 500);
        var maxDescription = new string('D', 10000);

        // Act
        var ticket = Ticket.Create(
            ValidWorkspaceId,
            maxTitle,
            maxDescription,
            ValidPriorityId,
            ValidStatusId,
            isInternal: true);

        // Assert
        Assert.NotNull(ticket);
    }

    [Fact]
    public void Create_Succeeds_WhenDescriptionIsNull()
    {
        // Arrange — Scenario 5: no description provided
        // Act
        var ticket = Ticket.Create(
            ValidWorkspaceId,
            title: "Valid Title",
            description: null,
            ValidPriorityId,
            ValidStatusId,
            isInternal: true);

        // Assert
        Assert.NotNull(ticket);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Ticket.UpdateDescription — length guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateDescription_ThrowsArgumentException_WhenDescriptionExceeds10000Characters()
    {
        // Arrange — Scenario 3: description update of 10001 characters
        var ticket = Ticket.Create(
            ValidWorkspaceId,
            title: "Valid Title",
            description: "Initial description",
            ValidPriorityId,
            ValidStatusId,
            isInternal: true);

        var oversizedDescription = new string('D', 10001);

        // Act
        var act = () => ticket.UpdateDescription(oversizedDescription);

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("10000", ex.Message);
    }

    [Fact]
    public void UpdateDescription_Succeeds_WhenDescriptionIsExactly10000Characters()
    {
        // Arrange — boundary: exactly 10000 characters should not throw
        var ticket = Ticket.Create(
            ValidWorkspaceId,
            title: "Valid Title",
            description: "Initial description",
            ValidPriorityId,
            ValidStatusId,
            isInternal: true);

        var maxDescription = new string('D', 10000);

        // Act — must not throw
        ticket.UpdateDescription(maxDescription);

        // Assert
        Assert.Equal(maxDescription, ticket.Description);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Ticket.MaterializeFromExternal — length guards
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MaterializeFromExternal_ThrowsArgumentException_WhenTitleExceeds500Characters()
    {
        // Arrange
        var oversizedTitle = new string('A', 501);

        // Act
        var act = () => Ticket.MaterializeFromExternal(
            ValidWorkspaceId,
            integrationId: Guid.NewGuid(),
            externalTicketId: "PROJ-123",
            title: oversizedTitle,
            description: "Valid description");

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public void MaterializeFromExternal_ThrowsArgumentException_WhenDescriptionExceeds10000Characters()
    {
        // Arrange
        var oversizedDescription = new string('D', 10001);

        // Act
        var act = () => Ticket.MaterializeFromExternal(
            ValidWorkspaceId,
            integrationId: Guid.NewGuid(),
            externalTicketId: "PROJ-123",
            title: "Valid Title",
            description: oversizedDescription);

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("10000", ex.Message);
    }
}
